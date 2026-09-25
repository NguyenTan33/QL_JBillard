using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QL_JBillard.Data;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Helpers;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.Services.Implementations;

public class AuditService : IAuditService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public AuditService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public void Log(int? userId, string action, string entityName, string? entityId, object? oldValues = null, object? newValues = null)
    {
        try
        {
            using var context = _contextFactory.CreateDbContext();

            // Lấy hash của bản ghi gần nhất để tạo chuỗi liên kết (Chained Hashing)
            var lastLog = context.AuditLogs.OrderByDescending(x => x.AuditLogId).FirstOrDefault();
            string prevHash = lastLog?.RecordHash ?? "GENESIS_BLOCK_JBILLIARD_SECURE";

            var now = DateTime.Now;
            string timeStr = now.ToString("yyyy-MM-dd HH:mm:ss");
            var jsonOpts = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string oldJson = oldValues switch
            {
                null => string.Empty,
                string s => s,
                _ => JsonSerializer.Serialize(oldValues, jsonOpts)
            };
            string newJson = newValues switch
            {
                null => string.Empty,
                string s => s,
                _ => JsonSerializer.Serialize(newValues, jsonOpts)
            };

            // Tính mã băm bảo mật SHA-256 kết hợp dữ liệu và hash trước
            string payload = $"{userId}_{action}_{entityName}_{entityId}_{prevHash}_{timeStr}";
            string recordHash = SecurityHelper.ComputeSha256(payload);

            var logEntry = new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                OldValuesJson = oldJson,
                NewValuesJson = newJson,
                MachineName = Environment.MachineName,
                CreatedAt = now,
                PreviousHash = prevHash,
                RecordHash = recordHash
            };

            context.AuditLogs.Add(logEntry);
            context.SaveChanges();
        }
        catch (Exception ex)
        {
            // Ghi nhận lỗi fallback vào console nếu có sự cố
            System.Diagnostics.Debug.WriteLine($"Lỗi ghi AuditLog: {ex.Message}");
        }
    }

    public List<AuditLog> GetRecentLogs(int limit = 100)
    {
        using var context = _contextFactory.CreateDbContext();
        return context.AuditLogs
                      .Include(x => x.User)
                      .OrderByDescending(x => x.AuditLogId)
                      .Take(limit)
                      .ToList();
    }

    public (bool IsValid, int TamperedIndex, string Message) VerifyChainIntegrity()
    {
        using var context = _contextFactory.CreateDbContext();
        var logs = context.AuditLogs.OrderBy(x => x.AuditLogId).ToList();

        if (logs.Count == 0) return (true, 0, "Chưa có bản ghi nhật ký.");

        string expectedPrevHash = "GENESIS_BLOCK_JBILLIARD_SECURE";

        for (int i = 0; i < logs.Count; i++)
        {
            var current = logs[i];

            // 1. Kiểm tra liên kết với bản ghi trước
            if (current.PreviousHash != expectedPrevHash)
            {
                return (false, current.AuditLogId, $"Phát hiện đứt gãy chuỗi nhật ký tại Log ID #{current.AuditLogId} (bị xóa hoặc chèn lén)!");
            }

            // 2. Tính lại hash của chính bản ghi hiện tại
            string timeStr = current.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            string payload = $"{current.UserId}_{current.Action}_{current.EntityName}_{current.EntityId}_{current.PreviousHash}_{timeStr}";
            string computedHash = SecurityHelper.ComputeSha256(payload);

            if (current.RecordHash != computedHash)
            {
                return (false, current.AuditLogId, $"Dữ liệu tại Log ID #{current.AuditLogId} đã bị sửa trực tiếp trong CSDL!");
            }

            expectedPrevHash = current.RecordHash;
        }

        return (true, 0, "Toàn bộ chuỗi nhật ký kiểm toán hợp lệ và nguyên vẹn 100%.");
    }
}
