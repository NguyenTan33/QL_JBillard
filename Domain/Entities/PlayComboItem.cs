namespace QL_JBillard.Domain.Entities;

/// <summary>
/// Thực thể quan hệ nhiều-nhiều (3NF) giữa Gói Combo Bida và Món trong Thực đơn (F&amp;B)
/// Giúp hệ thống bếp/pha chế nhận diện chính xác món và tự động trừ kho nguyên phụ liệu.
/// </summary>
public class PlayComboItem
{
    public int ComboItemId { get; set; }
    public int ComboId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;

    // Navigation
    public virtual PlayCombo PlayCombo { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}
