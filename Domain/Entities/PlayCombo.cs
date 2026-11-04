namespace QL_JBillard.Domain.Entities;

public class PlayCombo
{
    public int ComboId { get; set; }
    public string ComboName { get; set; } = string.Empty; // Ví dụ: "Combo 3 Tiếng + 2 Nước"
    public int? TableTypeId { get; set; } // null = Áp dụng tất cả loại bàn
    public decimal Price { get; set; } = 150_000;
    public int DurationMinutes { get; set; } = 180; // 3 tiếng = 180 phút
    public string? IncludedItems { get; set; } = "2 lon nước ngọt / Sting tùy chọn";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public string DurationFormatted => $"{DurationMinutes / 60}h {(DurationMinutes % 60 > 0 ? $"{DurationMinutes % 60}p" : "")}".Trim();

    // Navigation
    public virtual TableType? TableType { get; set; }
}
