using QL_JBillard.Domain.Enums;

namespace QL_JBillard.Domain.Entities;

public class Shift
{
    public int ShiftId { get; set; }
    public int UserId { get; set; }
    
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
    
    public decimal OpeningCash { get; set; } = 0;             // Tiền mặt ban đầu trong két
    public decimal? PreviousShiftClosingCash { get; set; }    // Tiền thực tế bàn giao từ ca trước
    public bool IsOpeningDiscrepant { get; set; } = false;    // Cờ báo lệch tiền đầu ca
    public string? OpeningDiscrepancyNotes { get; set; }      // Lưu ý báo cáo cho quản lý khi lệch tiền đầu ca
    public decimal TotalBankDeposited { get; set; } = 0;      // Tổng tiền nộp ngân hàng trong ca (trừ két)
    public decimal SystemCalculatedCash { get; set; } = 0;     // Doanh thu tiền mặt hệ thống tính
    public decimal SystemCalculatedTransfer { get; set; } = 0; // Doanh thu chuyển khoản / VietQR
    public decimal? ActualCashRecorded { get; set; }           // Tiền mặt thu ngân thực đếm khi giao ca
    public decimal? Discrepancy { get; set; }                  // Chênh lệch (Thực tế - Tính toán)
    
    public ShiftStatus Status { get; set; } = ShiftStatus.Open;
    public string? Notes { get; set; }

    // Navigation
    public virtual User User { get; set; } = null!;
    public virtual ICollection<BankDepositSlip> BankDepositSlips { get; set; } = new List<BankDepositSlip>();
}
