namespace QL_JBillard.Domain.Entities;

public class Invoice
{
    public int InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty; // Mã HĐ: HD-YYYYMMDD-XXXX
    public int SessionId { get; set; }
    public int? CustomerId { get; set; }
    public int CashierUserId { get; set; }
    public int? ShiftId { get; set; } // Liên kết trực tiếp với ca làm việc để tránh mất bill khỏi doanh thu ca
    
    public int TotalPlayMinutes { get; set; }
    public decimal TimeAmount { get; set; }
    public decimal ServiceAmount { get; set; }
    
    public decimal DiscountPercent { get; set; } = 0;
    public decimal DiscountAmount { get; set; } = 0;
    
    public int PointsUsed { get; set; } = 0;
    public decimal PointsDiscountAmount { get; set; } = 0;
    
    public decimal SurchargeAmount { get; set; } = 0;
    public decimal FinalAmount { get; set; }
    public int PointsEarned { get; set; } = 0;
    
    public string? AppliedComboName { get; set; } // Ghi lại tên combo nếu áp dụng gói
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? Notes { get; set; }
    
    // Thuật toán chống gian lận: Chữ ký số SHA-256 bảo vệ hóa đơn khỏi bị sửa trộm trong file SQLite
    public string TamperProofHash { get; set; } = string.Empty;

    // Navigation
    public virtual TableSession Session { get; set; } = null!;
    public virtual Customer? Customer { get; set; }
    public virtual User CashierUser { get; set; } = null!;
    public virtual ICollection<InvoicePayment> Payments { get; set; } = new List<InvoicePayment>();
}
