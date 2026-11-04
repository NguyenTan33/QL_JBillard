using System;
using QL_JBillard.Domain.Enums;

namespace QL_JBillard.Domain.Entities;

public class BankDepositSlip
{
    public int DepositSlipId { get; set; }
    public string SlipCode { get; set; } = string.Empty; // Mã phiếu: e.g. "NP-20261104-001"
    public int? ShiftId { get; set; }
    public int UserId { get; set; }                      // Người nộp (Thu ngân hoặc Admin)
    public decimal Amount { get; set; }                  // Số tiền nộp ngân hàng
    public string? BankName { get; set; }                // Tên ngân hàng nhận (VD: MBBank)
    public string? BankAccountNo { get; set; }           // Số tài khoản nhận
    public string? BankAccountName { get; set; }         // Tên chủ tài khoản nhận
    public string? ImagePath { get; set; }               // Đường dẫn ảnh chụp biên lai chuyển tiền
    public string? Notes { get; set; }                   // Ghi chú / lý do nộp
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    // Trạng thái phê duyệt: "Approved" (Đã duyệt), "Pending" (Chờ duyệt), "Rejected" (Từ chối)
    public string Status { get; set; } = "Approved";    
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public bool IsModified { get; set; } = false;        // Đánh dấu nếu phiếu bị chỉnh sửa sau khi lập

    // Navigation
    public virtual Shift? Shift { get; set; }
    public virtual User User { get; set; } = null!;
    public virtual User? ApprovedByUser { get; set; }
}
