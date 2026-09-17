namespace QL_JBillard.Domain.Entities;

public class ProductCategory
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty; // Nước giải khát, Bia, Đồ ăn, Thuốc lá, Phụ kiện...
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
