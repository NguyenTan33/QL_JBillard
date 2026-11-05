using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.ViewModels;

public partial class ShiftViewModel : ObservableObject
{
    private readonly IShiftService _shiftService;
    private readonly IAuthService _authService;
    private readonly ITableService _tableService;

    [ObservableProperty]
    private Shift? _currentShift;

    [ObservableProperty]
    private bool _hasActiveShift;

    [ObservableProperty]
    private bool _isShiftOwnerOrAdmin = true;

    [ObservableProperty]
    private string? _shiftOwnershipWarning;

    public bool IsAdmin => _authService.CurrentUser?.Role == UserRole.Admin;

    [ObservableProperty]
    private decimal _liveCashRevenue;

    [ObservableProperty]
    private decimal _liveTransferRevenue;

    [ObservableProperty]
    private decimal _totalRevenue;

    [ObservableProperty]
    private int _liveInvoiceCount;

    [ObservableProperty]
    private decimal _liveBankDeposits;

    [ObservableProperty]
    private decimal _currentDrawerCash;

    [ObservableProperty]
    private decimal _expectedCashInDrawer;

    [ObservableProperty]
    private decimal _discrepancy;

    [ObservableProperty]
    private string _discrepancyStatus = "☑ Khớp tiền két (Không lệch)";

    [ObservableProperty]
    private string _discrepancyColor = "#059669";

    [ObservableProperty]
    private bool _canConfirmClose = true;

    // --- BẢNG ĐẾM MỆNH GIÁ TIỀN MẶT (500K -> 500Đ) ---
    [ObservableProperty]
    private int _count500k = 0;

    [ObservableProperty]
    private int _count200k = 0;

    [ObservableProperty]
    private int _count100k = 0;

    [ObservableProperty]
    private int _count50k = 0;

    [ObservableProperty]
    private int _count20k = 0;

    [ObservableProperty]
    private int _count10k = 0;

    [ObservableProperty]
    private int _count5k = 0;

    [ObservableProperty]
    private int _count2k = 0;

    [ObservableProperty]
    private int _count1k = 0;

    [ObservableProperty]
    private int _count500d = 0;

    public decimal Subtotal500k => Count500k * 500000m;
    public decimal Subtotal200k => Count200k * 200000m;
    public decimal Subtotal100k => Count100k * 100000m;
    public decimal Subtotal50k => Count50k * 50000m;
    public decimal Subtotal20k => Count20k * 20000m;
    public decimal Subtotal10k => Count10k * 10000m;
    public decimal Subtotal5k => Count5k * 5000m;
    public decimal Subtotal2k => Count2k * 2000m;
    public decimal Subtotal1k => Count1k * 1000m;
    public decimal Subtotal500d => Count500d * 500m;

    public decimal DenominationTotalCash => 
        Subtotal500k + Subtotal200k + Subtotal100k + Subtotal50k + Subtotal20k +
        Subtotal10k + Subtotal5k + Subtotal2k + Subtotal1k + Subtotal500d;

    // --- FORM MỞ CA (START SHIFT) ---
    [ObservableProperty]
    private decimal _previousShiftClosingCash;

    [ObservableProperty]
    private decimal _formOpeningCash;

    [ObservableProperty]
    private string _formOpeningCashInput = "0";

    [ObservableProperty]
    private bool _isOpeningMatch = true;

    [ObservableProperty]
    private decimal _openingDiscrepancyAmount = 0;

    [ObservableProperty]
    private string? _openingDiscrepancyNotes;

    [ObservableProperty]
    private string? _formStartNotes;

    // --- FORM CHỐT CA (CLOSE SHIFT) ---
    [ObservableProperty]
    private decimal _formActualCash;

    [ObservableProperty]
    private string _formActualCashInput = "0";

    [ObservableProperty]
    private string? _formCloseNotes;

    [ObservableProperty]
    private string? _message;

    // --- DANH SÁCH PHIẾU NỘP TIỀN NGÂN HÀNG & BỘ LỌC ADMIN ---
    [ObservableProperty]
    private ObservableCollection<BankDepositSlip> _bankDepositSlips = new();

    [ObservableProperty]
    private string _depositFilterPeriod = "TODAY"; // TODAY, THIS_WEEK, THIS_MONTH, ALL, CUSTOM

    [ObservableProperty]
    private DateTime _depositFilterFromDate = DateTime.Today;

    [ObservableProperty]
    private DateTime _depositFilterToDate = DateTime.Today;

    [ObservableProperty]
    private bool _isCustomDepositFilter = false;

    [ObservableProperty]
    private decimal _filteredDepositTotal = 0;

    [RelayCommand]
    public void SetDepositFilter(string period)
    {
        DepositFilterPeriod = period;
        IsCustomDepositFilter = period == "CUSTOM";

        DateTime now = DateTime.Now;
        if (period == "TODAY")
        {
            DepositFilterFromDate = now.Date;
            DepositFilterToDate = now.Date;
        }
        else if (period == "THIS_WEEK")
        {
            int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
            DepositFilterFromDate = now.Date.AddDays(-diff);
            DepositFilterToDate = now.Date;
        }
        else if (period == "THIS_MONTH")
        {
            DepositFilterFromDate = new DateTime(now.Year, now.Month, 1);
            DepositFilterToDate = now.Date;
        }
        else if (period == "ALL")
        {
            DepositFilterFromDate = DateTime.MinValue;
            DepositFilterToDate = DateTime.MaxValue;
        }

        LoadBankDepositSlips();
    }

    [RelayCommand]
    public void ApplyCustomDepositFilter()
    {
        DepositFilterPeriod = "CUSTOM";
        IsCustomDepositFilter = true;
        LoadBankDepositSlips();
    }

    public event Action? RequestOpenBankDepositDialog;
    public event Action<List<Table>, Action<Views.Dialogs.ShiftHandoffChoice>>? RequestOccupiedTablesChoice;
    public event Action? RequestNavigateToTables;

    public ShiftViewModel(IShiftService shiftService, IAuthService authService, ITableService tableService)
    {
        _shiftService = shiftService;
        _authService = authService;
        _tableService = tableService;
        RefreshShiftStatus();
    }

    [RelayCommand]
    public void RefreshShiftStatus()
    {
        CurrentShift = _shiftService.CurrentActiveShift;
        HasActiveShift = CurrentShift != null;
        OnPropertyChanged(nameof(IsAdmin));

        int currentUserId = _authService.CurrentUser?.UserId ?? 0;
        bool isOwner = CurrentShift != null && CurrentShift.UserId == currentUserId;
        IsShiftOwnerOrAdmin = IsAdmin || isOwner;

        if (HasActiveShift)
        {
            if (!IsShiftOwnerOrAdmin)
            {
                ShiftOwnershipWarning = $"⚠️ Ca trực hiện tại do nhân viên [{CurrentShift?.User?.FullName}] mở. Bạn không có quyền chốt ca này. Vui lòng để nhân viên này chốt ca hoặc liên hệ Quản trị viên (Admin) xử lý!";
            }
            else if (IsAdmin && !isOwner)
            {
                ShiftOwnershipWarning = $"👑 Chế độ Quản Trị Viên: Bạn đang quản lý ca của nhân viên [{CurrentShift?.User?.FullName}]. Bạn có toàn quyền can thiệp hoặc chốt ca thay thế nhân viên nếu cần.";
            }
            else
            {
                ShiftOwnershipWarning = null;
            }

            var (cash, transfer, invoices, deposits, drawer) = _shiftService.GetCurrentShiftSummary();
            LiveCashRevenue = cash;
            LiveTransferRevenue = transfer;
            TotalRevenue = cash + transfer;
            LiveInvoiceCount = invoices;
            LiveBankDeposits = deposits;
            CurrentDrawerCash = drawer;

            ExpectedCashInDrawer = (CurrentShift?.OpeningCash ?? 0) + cash - deposits;
            FormActualCash = ExpectedCashInDrawer;
            FormActualCashInput = $"{FormActualCash:N0}";
            UpdateDiscrepancy(FormActualCash);
        }
        else
        {
            // Chưa có ca -> nạp số tiền bàn giao từ ca trước
            PreviousShiftClosingCash = _shiftService.GetDrawerBalanceFromLastShift();
            FormOpeningCash = PreviousShiftClosingCash;
            FormOpeningCashInput = $"{FormOpeningCash:N0}";
            CheckOpeningMatch();
        }

        // Tải lịch sử nộp tiền ngân hàng luôn luôn (Dù đang trong ca hay chưa mở ca)
        LoadBankDepositSlips();
    }

    public void LoadBankDepositSlips()
    {
        DateTime? from = null;
        DateTime? to = null;

        if (IsAdmin)
        {
            if (DepositFilterPeriod != "ALL")
            {
                from = DepositFilterFromDate.Date;
                to = DepositFilterToDate.Date.AddDays(1).AddTicks(-1);
            }
        }
        else
        {
            // Thu ngân không có chức năng lọc nâng cao, chỉ xem phiếu nộp của chính mình hôm nay
            from = DateTime.Today;
            to = DateTime.Today.AddDays(1).AddTicks(-1);
        }

        var slips = _shiftService.GetAllBankDepositSlips(from, to);
        int currentUserId = _authService.CurrentUser?.UserId ?? 0;

        // Admin xem toàn bộ phiếu nộp theo bộ lọc của cả quán, Thu ngân chỉ xem phiếu do chính mình lập
        if (!IsAdmin)
        {
            slips = slips.Where(s => s.UserId == currentUserId).ToList();
        }

        BankDepositSlips = new ObservableCollection<BankDepositSlip>(slips);
        FilteredDepositTotal = slips.Sum(s => s.Amount);
    }

    partial void OnCount500kChanged(int value) => RecalculateFromDenominations();
    partial void OnCount200kChanged(int value) => RecalculateFromDenominations();
    partial void OnCount100kChanged(int value) => RecalculateFromDenominations();
    partial void OnCount50kChanged(int value) => RecalculateFromDenominations();
    partial void OnCount20kChanged(int value) => RecalculateFromDenominations();
    partial void OnCount10kChanged(int value) => RecalculateFromDenominations();
    partial void OnCount5kChanged(int value) => RecalculateFromDenominations();
    partial void OnCount2kChanged(int value) => RecalculateFromDenominations();
    partial void OnCount1kChanged(int value) => RecalculateFromDenominations();
    partial void OnCount500dChanged(int value) => RecalculateFromDenominations();

    private void NotifyDenominationProperties()
    {
        OnPropertyChanged(nameof(Subtotal500k));
        OnPropertyChanged(nameof(Subtotal200k));
        OnPropertyChanged(nameof(Subtotal100k));
        OnPropertyChanged(nameof(Subtotal50k));
        OnPropertyChanged(nameof(Subtotal20k));
        OnPropertyChanged(nameof(Subtotal10k));
        OnPropertyChanged(nameof(Subtotal5k));
        OnPropertyChanged(nameof(Subtotal2k));
        OnPropertyChanged(nameof(Subtotal1k));
        OnPropertyChanged(nameof(Subtotal500d));
        OnPropertyChanged(nameof(DenominationTotalCash));
    }

    private bool _isUpdatingDenominations;

    private void RecalculateFromDenominations()
    {
        if (_isUpdatingDenominations) return;
        _isUpdatingDenominations = true;
        try
        {
            NotifyDenominationProperties();
            decimal total = DenominationTotalCash;
            FormActualCash = total;
            FormActualCashInput = $"{total:N0}";
            UpdateDiscrepancy(total);
        }
        finally
        {
            _isUpdatingDenominations = false;
        }
    }

    [RelayCommand]
    private void ClearDenominations()
    {
        _isUpdatingDenominations = true;
        try
        {
            Count500k = 0;
            Count200k = 0;
            Count100k = 0;
            Count50k = 0;
            Count20k = 0;
            Count10k = 0;
            Count5k = 0;
            Count2k = 0;
            Count1k = 0;
            Count500d = 0;
            NotifyDenominationProperties();
            FormActualCash = 0;
            FormActualCashInput = "0";
            UpdateDiscrepancy(0);
        }
        finally
        {
            _isUpdatingDenominations = false;
        }
    }

    [RelayCommand]
    public void IncrementCount(string? denom)
    {
        switch (denom)
        {
            case "500k": Count500k++; break;
            case "200k": Count200k++; break;
            case "100k": Count100k++; break;
            case "50k": Count50k++; break;
            case "20k": Count20k++; break;
            case "10k": Count10k++; break;
            case "5k": Count5k++; break;
            case "2k": Count2k++; break;
            case "1k": Count1k++; break;
            case "500d": Count500d++; break;
        }
    }

    [RelayCommand]
    public void DecrementCount(string? denom)
    {
        switch (denom)
        {
            case "500k": if (Count500k > 0) Count500k--; break;
            case "200k": if (Count200k > 0) Count200k--; break;
            case "100k": if (Count100k > 0) Count100k--; break;
            case "50k": if (Count50k > 0) Count50k--; break;
            case "20k": if (Count20k > 0) Count20k--; break;
            case "10k": if (Count10k > 0) Count10k--; break;
            case "5k": if (Count5k > 0) Count5k--; break;
            case "2k": if (Count2k > 0) Count2k--; break;
            case "1k": if (Count1k > 0) Count1k--; break;
            case "500d": if (Count500d > 0) Count500d--; break;
        }
    }

    partial void OnFormOpeningCashInputChanged(string value)
    {
        string clean = System.Text.RegularExpressions.Regex.Replace(value ?? "", @"[^\d]", "");
        if (decimal.TryParse(clean, out decimal val))
        {
            _formOpeningCash = val;
        }
        else
        {
            _formOpeningCash = 0;
        }
        OnPropertyChanged(nameof(FormOpeningCash));
        CheckOpeningMatch();
    }

    partial void OnFormOpeningCashChanged(decimal value)
    {
        CheckOpeningMatch();
    }

    private void CheckOpeningMatch()
    {
        OpeningDiscrepancyAmount = FormOpeningCash - PreviousShiftClosingCash;
        IsOpeningMatch = (OpeningDiscrepancyAmount == 0);
    }

    partial void OnFormActualCashInputChanged(string value)
    {
        string clean = System.Text.RegularExpressions.Regex.Replace(value ?? "", @"[^\d]", "");
        if (decimal.TryParse(clean, out decimal val))
        {
            _formActualCash = val;
        }
        else
        {
            _formActualCash = 0;
        }
        OnPropertyChanged(nameof(FormActualCash));
        UpdateDiscrepancy(_formActualCash);
    }

    partial void OnFormActualCashChanged(decimal value)
    {
        UpdateDiscrepancy(value);
    }

    private void UpdateDiscrepancy(decimal actualCash)
    {
        Discrepancy = actualCash - ExpectedCashInDrawer;
        if (Discrepancy == 0)
        {
            DiscrepancyStatus = "☑ Khớp tiền két (Không lệch)";
            DiscrepancyColor = "#059669";
            CanConfirmClose = true;
        }
        else if (Discrepancy > 0)
        {
            DiscrepancyStatus = $"⚠️ Thừa tiền trong két: +{Discrepancy:N0} đ (Vui lòng kiểm đếm lại)";
            DiscrepancyColor = "#DC2626";
            CanConfirmClose = false;
        }
        else
        {
            DiscrepancyStatus = $"❌ Hụt tiền két (Thiếu): {Discrepancy:N0} đ (Vui lòng kiểm đếm lại)";
            DiscrepancyColor = "#DC2626";
            CanConfirmClose = false;
        }
    }

    [RelayCommand]
    private void StartShift()
    {
        int userId = _authService.CurrentUser?.UserId ?? 1;

        if (!IsOpeningMatch && string.IsNullOrWhiteSpace(OpeningDiscrepancyNotes))
        {
            Message = "⚠️ Số tiền thực đếm lệch so với tiền bàn giao ca trước! Vui lòng nhập lưu ý/báo cáo lý do lệch tiền gửi Quản lý/Chủ quán!";
            return;
        }

        try
        {
            var shift = _shiftService.StartShift(
                userId,
                FormOpeningCash,
                FormStartNotes,
                !IsOpeningMatch,
                OpeningDiscrepancyNotes
            );

            Message = !IsOpeningMatch 
                ? $"Đã mở ca làm việc và ghi nhận biên bản lệch đầu ca ({OpeningDiscrepancyAmount:N0} đ) cho Quản lý!"
                : "Bắt đầu ca làm việc thành công! Tiền két khớp 100%.";

            RefreshShiftStatus();
        }
        catch (Exception ex)
        {
            Message = $"Lỗi mở ca: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenBankDeposit()
    {
        if (!HasActiveShift)
        {
            Message = "⚠️ Vui lòng mở ca làm việc trước khi lập phiếu nộp tiền ngân hàng!";
            return;
        }
        RequestOpenBankDepositDialog?.Invoke();
    }

    [RelayCommand]
    private void CloseShift()
    {
        if (CurrentShift == null) return;

        int currentUserId = _authService.CurrentUser?.UserId ?? 0;
        bool isOwner = CurrentShift.UserId == currentUserId;

        if (!IsAdmin && !isOwner)
        {
            Message = $"❌ Bạn không có quyền chốt ca của nhân viên [{CurrentShift.User?.FullName}]! Vui lòng để nhân viên này chốt ca hoặc liên hệ Admin xử lý.";
            return;
        }

        // Nếu có chênh lệch và chưa có ghi chú giải trình
        if (Discrepancy != 0 && string.IsNullOrWhiteSpace(FormCloseNotes))
        {
            Message = $"❌ Tiền két không khớp (Lệch {Discrepancy:N0} đ)! Vui lòng kiểm đếm lại cho chính xác hoặc nhập giải trình bắt buộc!";
            return;
        }

        // Kiểm tra xem có bàn nào đang hoạt động (chưa thanh toán) không
        var occupiedTables = _tableService.GetAllTables()
                                          .Where(t => t.Status == TableStatus.Occupied)
                                          .ToList();

        if (occupiedTables.Any() && RequestOccupiedTablesChoice != null)
        {
            RequestOccupiedTablesChoice.Invoke(occupiedTables, (choice) =>
            {
                if (choice == Views.Dialogs.ShiftHandoffChoice.HandoffToNextShift)
                {
                    string handoffNote = $"[Chuyển tiếp {occupiedTables.Count} bàn đang chơi cho ca sau: {string.Join(", ", occupiedTables.Select(t => t.TableNumber))}]";
                    DoExecuteCloseShift(handoffNote);
                }
                else if (choice == Views.Dialogs.ShiftHandoffChoice.CheckoutAllFirst)
                {
                    Message = "⚠️ Vui lòng thanh toán tất cả các bàn đang chơi trước khi chốt ca!";
                    RequestNavigateToTables?.Invoke();
                }
                else
                {
                    Message = "Đã hủy chốt ca.";
                }
            });
            return;
        }

        DoExecuteCloseShift(null);
    }

    private void DoExecuteCloseShift(string? handoffNote)
    {
        try
        {
            int currentUserId = _authService.CurrentUser?.UserId ?? 1;
            string finalNotes = FormCloseNotes ?? "";
            if (!string.IsNullOrWhiteSpace(handoffNote))
            {
                finalNotes = string.IsNullOrWhiteSpace(finalNotes) ? handoffNote : $"{handoffNote} | {finalNotes}";
            }

            var closedShift = _shiftService.CloseShift(CurrentShift!.ShiftId, FormActualCash, finalNotes, currentUserId);
            Message = $"Đã chốt ca thành công! Tiền mặt thực bàn giao sang ca sau: {closedShift.ActualCashRecorded:N0} đ.";
            RefreshShiftStatus();
        }
        catch (Exception ex)
        {
            Message = $"Lỗi chốt ca: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ApproveSlip(BankDepositSlip? slip)
    {
        if (slip == null || !IsAdmin) return;
        int adminId = _authService.CurrentUser?.UserId ?? 1;

        bool success = _shiftService.ApproveBankDepositSlip(slip.DepositSlipId, adminId);
        if (success)
        {
            Message = $"✅ Đã phê duyệt phiếu nộp tiền {slip.SlipCode}!";
            RefreshShiftStatus();
        }
    }

    [RelayCommand]
    private void RejectSlip(BankDepositSlip? slip)
    {
        if (slip == null || !IsAdmin) return;
        int adminId = _authService.CurrentUser?.UserId ?? 1;

        bool success = _shiftService.RejectBankDepositSlip(slip.DepositSlipId, adminId, "Admin từ chối xác nhận");
        if (success)
        {
            Message = $"❌ Đã từ chối phiếu nộp tiền {slip.SlipCode} và hoàn tiền lại vào két!";
            RefreshShiftStatus();
        }
    }
}
