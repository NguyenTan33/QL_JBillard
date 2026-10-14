using System.IO;
using System.Timers;
using Microsoft.EntityFrameworkCore;
using QL_JBillard.Data;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Helpers;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.Services.Implementations;

public class BackupAndSecurityService : IBackupAndSecurityService, IDisposable
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IAuditService _auditService;
    private System.Timers.Timer? _autoBackupTimer;
    private const string BackupKey = "JBillard@Encrypted#2026_Backup_SecPass!";

    public BackupAndSecurityService(IDbContextFactory<AppDbContext> contextFactory, IAuditService auditService)
    {
        _contextFactory = contextFactory;
        _auditService = auditService;
    }

    public DatabaseBackupLog CreateBackup(string triggerReason = "MANUAL", bool encrypt = true)
    {
        using var context = _contextFactory.CreateDbContext();
        string dbPath = context.DbPath;

        if (!File.Exists(dbPath))
        {
            throw new FileNotFoundException("Không tìm thấy file CSDL SQLite để sao lưu.");
        }

        string backupFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
        if (!Directory.Exists(backupFolder))
        {
            Directory.CreateDirectory(backupFolder);
        }

        string backupFileName = encrypt ? "billiard_latest_backup.enc" : "billiard_latest_backup.db";
        string backupFilePath = Path.Combine(backupFolder, backupFileName);

        // Đảm bảo flush dữ liệu SQLite trước khi copy
        context.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(FULL);");

        if (encrypt)
        {
            // Mã hóa AES-256 chống mở trộm dữ liệu - ghi đè tệp cũ
            SecurityHelper.EncryptFile(dbPath, backupFilePath, BackupKey);
        }
        else
        {
            File.Copy(dbPath, backupFilePath, true);
        }

        var fileInfo = new FileInfo(backupFilePath);
        string sha256 = SecurityHelper.ComputeFileSha256(backupFilePath);

        var backupLog = context.DatabaseBackupLogs.FirstOrDefault(b => b.BackupFileName == backupFileName);
        if (backupLog == null)
        {
            backupLog = new DatabaseBackupLog
            {
                BackupFileName = backupFileName,
                BackupFilePath = backupFilePath,
                FileSizeBytes = fileInfo.Length,
                Sha256Checksum = sha256,
                IsEncrypted = encrypt,
                TriggerReason = triggerReason,
                CreatedAt = DateTime.Now
            };
            context.DatabaseBackupLogs.Add(backupLog);
        }
        else
        {
            backupLog.BackupFilePath = backupFilePath;
            backupLog.FileSizeBytes = fileInfo.Length;
            backupLog.Sha256Checksum = sha256;
            backupLog.IsEncrypted = encrypt;
            backupLog.TriggerReason = triggerReason;
            backupLog.CreatedAt = DateTime.Now;
        }
        context.SaveChanges();

        _auditService.Log(null, "DATABASE_BACKUP", "System", backupLog.BackupLogId.ToString(), null, new
        {
            FileName = backupFileName,
            Size = fileInfo.Length,
            Reason = triggerReason,
            IsEncrypted = encrypt
        });

        // Tự động dọn dẹp các bản backup quá 30 ngày (Giữ tối đa 50 bản gần nhất)
        CleanOldBackups(context, backupFolder);

        return backupLog;
    }

    public bool RestoreBackup(string backupFilePath)
    {
        if (!File.Exists(backupFilePath)) return false;

        using var context = _contextFactory.CreateDbContext();
        string currentDbPath = context.DbPath;
        string tempRestorePath = Path.Combine(Path.GetDirectoryName(currentDbPath)!, "restore_temp.db");

        try
        {
            bool isEncrypted = backupFilePath.EndsWith(".enc", StringComparison.OrdinalIgnoreCase);
            if (isEncrypted)
            {
                SecurityHelper.DecryptFile(backupFilePath, tempRestorePath, BackupKey);
            }
            else
            {
                File.Copy(backupFilePath, tempRestorePath, true);
            }

            // Kiểm tra header SQLite cơ bản
            byte[] header = new byte[16];
            using (var fs = File.OpenRead(tempRestorePath))
            {
                fs.ReadExactly(header, 0, 16);
            }
            string headerString = System.Text.Encoding.ASCII.GetString(header);
            if (!headerString.StartsWith("SQLite format 3"))
            {
                if (File.Exists(tempRestorePath)) File.Delete(tempRestorePath);
                return false;
            }

            // Đổi tên file db hiện tại thành snapshot dự phòng trước khi ghi đè
            string safetyBackup = currentDbPath + ".before_restore";
            if (File.Exists(safetyBackup)) File.Delete(safetyBackup);
            File.Copy(currentDbPath, safetyBackup);

            // Ghi đè file db
            File.Copy(tempRestorePath, currentDbPath, true);
            File.Delete(tempRestorePath);

            _auditService.Log(null, "DATABASE_RESTORE", "System", null, null, $"Khôi phục thành công từ file {Path.GetFileName(backupFilePath)}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lỗi khôi phục CSDL: {ex.Message}");
            if (File.Exists(tempRestorePath)) File.Delete(tempRestorePath);
            return false;
        }
    }

    public List<DatabaseBackupLog> GetBackupHistory()
    {
        using var context = _contextFactory.CreateDbContext();
        return context.DatabaseBackupLogs.OrderByDescending(b => b.CreatedAt).Take(50).ToList();
    }

    public bool VerifyDatabaseIntegrity()
    {
        try
        {
            using var context = _contextFactory.CreateDbContext();
            // Chạy PRAGMA integrity_check của SQLite
            var result = context.Database.SqlQueryRaw<string>("PRAGMA integrity_check;").ToList();
            return result.Count > 0 && result[0].Equals("ok", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public (bool IsTampered, string Details) CheckTamperProofStatus()
    {
        using var context = _contextFactory.CreateDbContext();

        // 1. Kiểm tra chuỗi nhật ký AuditLog
        var (isChainValid, tamperedLogId, chainMsg) = _auditService.VerifyChainIntegrity();
        if (!isChainValid)
        {
            return (true, $"[CẢNH BÁO BẢO MẬT] {chainMsg}");
        }

        // 2. Kiểm tra tính toàn vẹn của Hóa đơn (Invoices)
        var invoices = context.Invoices.ToList();
        foreach (var inv in invoices)
        {
            string timeStr = inv.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            long roundedFinal = (long)Math.Round(inv.FinalAmount);
            string expectedPayload = $"{inv.InvoiceCode}_{roundedFinal}_{timeStr}_{inv.SessionId}_{inv.CashierUserId}";
            string expectedHash = SecurityHelper.ComputeSha256(expectedPayload);

            string legacyPayload = $"{inv.InvoiceCode}_{inv.FinalAmount}_{timeStr}_{inv.SessionId}_{inv.CashierUserId}";
            string legacyHash = SecurityHelper.ComputeSha256(legacyPayload);

            if (inv.TamperProofHash != expectedHash && inv.TamperProofHash != legacyHash)
            {
                return (true, $"[CẢNH BÁO GIAN LẬN] Hóa đơn #{inv.InvoiceCode} đã bị sửa trực tiếp số tiền hoặc nội dung trong CSDL SQLite!");
            }
        }

        return (false, "Dữ liệu hóa đơn và nhật ký hoàn toàn nguyên vẹn. Không phát hiện hành vi sửa lén!");
    }

    public void StartPeriodicAutoBackup(int intervalMinutes = 15)
    {
        StopPeriodicAutoBackup();
        _autoBackupTimer = new System.Timers.Timer(intervalMinutes * 60 * 1000);
        _autoBackupTimer.Elapsed += (s, e) =>
        {
            try
            {
                CreateBackup("AUTO_TIMER", true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi sao lưu tự động: {ex.Message}");
            }
        };
        _autoBackupTimer.AutoReset = true;
        _autoBackupTimer.Start();
    }

    public void StopPeriodicAutoBackup()
    {
        if (_autoBackupTimer != null)
        {
            _autoBackupTimer.Stop();
            _autoBackupTimer.Dispose();
            _autoBackupTimer = null;
        }
    }

    private void CleanOldBackups(AppDbContext context, string backupFolder)
    {
        try
        {
            // Xóa các file backup cũ còn sót lại để chỉ giữ đúng 1 file duy nhất ghi đè
            if (Directory.Exists(backupFolder))
            {
                var files = Directory.GetFiles(backupFolder);
                foreach (var f in files)
                {
                    string fname = Path.GetFileName(f);
                    if (fname != "billiard_latest_backup.enc" && fname != "billiard_latest_backup.db")
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
            }

            var oldLogs = context.DatabaseBackupLogs
                                 .Where(b => b.BackupFileName != "billiard_latest_backup.enc" && b.BackupFileName != "billiard_latest_backup.db")
                                 .ToList();

            if (oldLogs.Any())
            {
                context.DatabaseBackupLogs.RemoveRange(oldLogs);
                context.SaveChanges();
            }
        }
        catch { }
    }

    public void Dispose()
    {
        StopPeriodicAutoBackup();
    }
}
