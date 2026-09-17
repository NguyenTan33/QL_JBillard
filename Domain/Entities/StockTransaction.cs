using QL_JBillard.Domain.Enums;

namespace QL_JBillard.Domain.Entities;

public class StockTransaction
{
    public int StockTransactionId { get; set; }
    public StockTransactionType TransactionType { get; set; }
    public int PerformedByUserId { get; set; }
    public string? ReferenceCode { get; set; } // Mã hóa đơn hoặc mã phiếu nhập
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation
    public virtual User PerformedByUser { get; set; } = null!;
    public virtual ICollection<StockTransactionDetail> Details { get; set; } = new List<StockTransactionDetail>();
}
