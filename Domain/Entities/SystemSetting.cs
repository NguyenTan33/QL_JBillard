namespace QL_JBillard.Domain.Entities;

public class SystemSetting
{
    public string SettingKey { get; set; } = string.Empty; // Khóa cấu hình
    public string SettingValue { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
