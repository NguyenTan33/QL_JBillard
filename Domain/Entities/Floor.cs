namespace QL_JBillard.Domain.Entities;

public class Floor
{
    public int FloorId { get; set; }
    public string FloorName { get; set; } = string.Empty;
    public int OrderIndex { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    // Navigation property (1 Floor có nhiều Area)
    public virtual ICollection<Area> Areas { get; set; } = new List<Area>();
}
