namespace QL_JBillard.Domain.Entities;

public class TableType
{
    public int TableTypeId { get; set; }
    public string TypeName { get; set; } = string.Empty; // Bàn Thường, VIP, Super VIP, Carom, Pool...
    public decimal DefaultHourlyRate { get; set; }        // Giá giờ mặc định (VD: 60,000, 90,000, 150,000)
    public int MinPlayMinutes { get; set; } = 15;         // Số phút tối thiểu
    public string ColorCode { get; set; } = "#10B981";    // Mã màu đại diện
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual ICollection<Table> Tables { get; set; } = new List<Table>();
}
