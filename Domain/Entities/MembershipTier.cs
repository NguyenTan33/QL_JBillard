namespace QL_JBillard.Domain.Entities;

public class MembershipTier
{
    public int TierId { get; set; }
    public string TierName { get; set; } = string.Empty; // Đồng, Bạc, Vàng, Kim Cương
    public decimal MinSpentThreshold { get; set; } = 0;   // Chi tiêu tối thiểu để thăng hạng
    public decimal DiscountPercentHour { get; set; } = 0; // % giảm tiền giờ
    public decimal DiscountPercentFnb { get; set; } = 0;  // % giảm tiền đồ ăn uống
    public decimal PointEarnRate { get; set; } = 0.01m;   // 1% doanh thu quy đổi thành điểm (100k = 1k điểm)

    // Navigation
    public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();
}
