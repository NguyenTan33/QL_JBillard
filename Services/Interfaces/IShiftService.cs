using QL_JBillard.Domain.Entities;

namespace QL_JBillard.Services.Interfaces;

public interface IShiftService
{
    Shift? CurrentActiveShift { get; }
    Shift? GetLastClosedShift();
    decimal GetDrawerBalanceFromLastShift();
    Shift StartShift(int userId, decimal openingCash, string? notes = null, bool isDiscrepant = false, string? discrepancyReason = null);
    Shift CloseShift(int shiftId, decimal actualCash, string? notes = null, int? closingUserId = null);
    (decimal TotalCash, decimal TotalTransfer, int TotalInvoices, decimal TotalBankDeposits, decimal CurrentDrawerCash) GetCurrentShiftSummary();

    BankDepositSlip CreateBankDepositSlip(int userId, decimal amount, string? imagePath, string? notes, int? shiftId = null);
    List<BankDepositSlip> GetAllBankDepositSlips(DateTime? fromDate = null, DateTime? toDate = null);
    bool ApproveBankDepositSlip(int depositSlipId, int adminUserId);
    bool RejectBankDepositSlip(int depositSlipId, int adminUserId, string? reason);
    bool UpdateBankDepositSlip(int depositSlipId, decimal amount, string? imagePath, string? notes, int editorUserId);
}
