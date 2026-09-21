using QL_JBillard.Domain.Enums;

namespace QL_JBillard.Domain.Entities;

public class InvoicePayment
{
    public int PaymentId { get; set; }
    public int InvoiceId { get; set; }
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public string? TransactionReference { get; set; } // Mã GD VietQR / Bank
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation
    public virtual Invoice Invoice { get; set; } = null!;
}
