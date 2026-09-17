namespace QL_JBillard.Domain.Entities;

public class Unit
{
    public int UnitId { get; set; }
    public string UnitName { get; set; } = string.Empty; // Lon, Chai, Đĩa, Gói, Cái, Hộp...

    // Navigation
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
