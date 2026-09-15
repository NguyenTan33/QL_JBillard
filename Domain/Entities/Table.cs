using QL_JBillard.Domain.Enums;

namespace QL_JBillard.Domain.Entities;

public class Table
{
    public int TableId { get; set; }
    public string TableNumber { get; set; } = string.Empty; // VD: "Bàn 01", "Bàn VIP 02"
    public int AreaId { get; set; }
    public int TableTypeId { get; set; }
    public TableStatus Status { get; set; } = TableStatus.Available;
    public int? CurrentSessionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Area Area { get; set; } = null!;
    public virtual TableType TableType { get; set; } = null!;
    public virtual TableSession? CurrentSession { get; set; }
    public virtual ICollection<TableSession> Sessions { get; set; } = new List<TableSession>();
}
