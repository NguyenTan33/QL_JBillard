namespace QL_JBillard.Domain.Entities;

public class AuditLog
{
    public int AuditLogId { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = string.Empty;       // TABLE_OPEN, TABLE_SWITCH, ORDER_CANCEL, DISCOUNT, MANUAL_TIME...
    public string EntityName { get; set; } = string.Empty;   // Tên thực thể bị tác động
    public string? EntityId { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string MachineName { get; set; } = Environment.MachineName;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    // Blockchain-style Chained Hashing: Chống sửa lén / xóa trộm bản ghi nhật ký
    public string PreviousHash { get; set; } = "GENESIS";
    public string RecordHash { get; set; } = string.Empty;

    // Navigation
    public virtual User? User { get; set; }
}
