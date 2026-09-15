namespace QL_JBillard.Domain.Entities;

public class Area
{
    public int AreaId { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // Khóa ngoại trỏ về Floor (null nếu quán không chia tầng)
    public int? FloorId { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual Floor? Floor { get; set; }
    public virtual ICollection<Table> Tables { get; set; } = new List<Table>();
}
