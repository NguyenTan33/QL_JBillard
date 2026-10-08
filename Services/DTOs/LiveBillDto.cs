using QL_JBillard.Domain.Entities;

namespace QL_JBillard.Services.DTOs;

public class LiveBillDto
{
    public int SessionId { get; set; }
    public int TableId { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string TableTypeName { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
    public string? AppliedComboName { get; set; }
    public string PlayMode { get; set; } = "HOURLY";
    public decimal? ComboPrice { get; set; }
    public int? ComboDurationMinutes { get; set; }
    public bool IsComboMode => string.Equals(PlayMode, "COMBO", StringComparison.OrdinalIgnoreCase);
    public string ComboDurationText => ComboDurationMinutes.HasValue 
        ? $"{ComboDurationMinutes.Value / 60}h {(ComboDurationMinutes.Value % 60 > 0 ? $"{ComboDurationMinutes.Value % 60}p" : "")}".Trim() 
        : "";
    
    public DateTime StartTime { get; set; }
    public DateTime CurrentTime { get; set; } = DateTime.Now;
    public TimeSpan ElapsedTime => CurrentTime >= StartTime ? CurrentTime - StartTime : TimeSpan.Zero;
    public int TotalMinutes => (int)ElapsedTime.TotalMinutes;
    
    public decimal TimeCost { get; set; }
    public decimal ServiceCost { get; set; }
    public decimal SubTotal => TimeCost + ServiceCost;
    
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    
    public int AvailableCustomerPoints { get; set; }
    public int PointsToUse { get; set; }
    public decimal PointsDiscountAmount { get; set; }
    
    public decimal FinalAmount => Math.Max(0, SubTotal - DiscountAmount - PointsDiscountAmount);
    
    public Customer? Customer { get; set; }
    public List<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
