namespace QL_JBillard.Domain.Entities;

public class Product
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty; // Mã vạch hoặc mã rút gọn (VD: STING, REDBULL, THUOCLA555)
    public string ProductName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public int UnitId { get; set; }
    public decimal CostPrice { get; set; } = 0;             // Giá nhập vốn
    public decimal SellingPrice { get; set; }               // Giá bán lẻ
    public bool IsInventoryItem { get; set; } = true;       // true: trừ kho khi bán; false: dịch vụ không quản lý tồn
    public int MinStockAlert { get; set; } = 5;             // Ngưỡng cảnh báo sắp hết hàng
    public string? ImagePath { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual ProductCategory Category { get; set; } = null!;
    public virtual Unit Unit { get; set; } = null!;
    public virtual InventoryItem? InventoryItem { get; set; }
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public virtual ICollection<StockTransactionDetail> StockTransactionDetails { get; set; } = new List<StockTransactionDetail>();
}
