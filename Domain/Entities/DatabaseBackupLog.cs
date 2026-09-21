namespace QL_JBillard.Domain.Entities;

public class DatabaseBackupLog
{
    public int BackupLogId { get; set; }
    public string BackupFileName { get; set; } = string.Empty;
    public string BackupFilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Sha256Checksum { get; set; } = string.Empty; // Mã băm toàn vẹn file backup
    public bool IsEncrypted { get; set; } = true;
    public string TriggerReason { get; set; } = "AUTO";         // AUTO, ON_PAYMENT, SHIFT_CLOSE, MANUAL
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
