using QL_JBillard.Domain.Enums;

namespace QL_JBillard.Domain.Entities;

public class TableSession
{
    public int SessionId { get; set; }
    public int TableId { get; set; }
    public int? CustomerId { get; set; }
    public int CreatedByUserId { get; set; }
    
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
    public int TotalPlayMinutes { get; set; } = 0;
    
    // Lưu lại giá giờ tại thời điểm mở bàn để không bị ảnh hưởng khi bảng giá thay đổi trong tương lai
    public decimal AppliedHourlyRate { get; set; }
    public decimal TimeCost { get; set; } = 0;
    
    public SessionStatus Status { get; set; } = SessionStatus.Active;
    public string? Note { get; set; }

    // Hình thức chơi: HOURLY (theo giờ/khung giờ) hoặc COMBO (gói trọn gói)
    public string PlayMode { get; set; } = "HOURLY";
    public int? ComboId { get; set; }
    public string? ComboName { get; set; }
    public decimal? ComboPrice { get; set; }
    public int? ComboDurationMinutes { get; set; }

    // Navigation
    public virtual Table Table { get; set; } = null!;
    public virtual Customer? Customer { get; set; }
    public virtual User CreatedByUser { get; set; } = null!;
    public virtual PlayCombo? Combo { get; set; }
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public virtual Invoice? Invoice { get; set; }
}
