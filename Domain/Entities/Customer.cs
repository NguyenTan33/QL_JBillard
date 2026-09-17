namespace QL_JBillard.Domain.Entities;

public class Customer
{
    public int CustomerId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty; // Unique key tìm kiếm nhanh
    public string FullName { get; set; } = string.Empty;
    public int TierId { get; set; } = 1;                    // Hạng thành viên
    public int CurrentPoints { get; set; } = 0;             // Điểm hiện tại có thể tiêu
    public int TotalPointsAccumulated { get; set; } = 0;    // Tổng điểm tích từ trước đến nay
    public decimal TotalSpent { get; set; } = 0;            // Tổng tiền đã thanh toán
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastVisitAt { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public virtual MembershipTier Tier { get; set; } = null!;
    public virtual ICollection<TableSession> Sessions { get; set; } = new List<TableSession>();
    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
