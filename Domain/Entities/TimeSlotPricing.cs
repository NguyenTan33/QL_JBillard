namespace QL_JBillard.Domain.Entities;

public class TimeSlotPricing
{
    public int TimeSlotId { get; set; }
    public int TableTypeId { get; set; }
    public string SlotName { get; set; } = string.Empty; // Ví dụ: "Sáng (08:00 - 12:00)", "Chiều (12:00 - 18:00)"
    
    // Giờ bắt đầu (0..23) và phút (0..59)
    public int StartHour { get; set; } = 8;
    public int StartMinute { get; set; } = 0;
    
    // Giờ kết thúc (0..24) và phút (0..59)
    public int EndHour { get; set; } = 12;
    public int EndMinute { get; set; } = 0;
    
    public decimal HourlyRate { get; set; } = 50_000;
    public bool IsActive { get; set; } = true;

    public string TimeRangeDisplay => $"{StartHour:D2}:{StartMinute:D2} - {EndHour:D2}:{EndMinute:D2}";

    // Navigation
    public virtual TableType? TableType { get; set; }
}
