namespace QL_JBillard.Domain.Entities;

public class InventoryItem
{
    public int InventoryItemId { get; set; }
    public int ProductId { get; set; }
    public int QuantityOnHand { get; set; } = 0;           // Số lượng tồn kho hiện hành
    public DateTime LastRestockedAt { get; set; } = DateTime.Now;

    // Navigation
    public virtual Product Product { get; set; } = null!;
}
