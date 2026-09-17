namespace QL_JBillard.Domain.Entities;

public class StockTransactionDetail
{
    public int DetailId { get; set; }
    public int StockTransactionId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }

    // Navigation
    public virtual StockTransaction StockTransaction { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}
