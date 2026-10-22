using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QL_JBillard.Data;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Services.DTOs;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.ViewModels;

public partial class CheckoutDialogViewModel : ObservableObject
{
    private readonly IBillingService _billingService;
    private readonly ICustomerService _customerService;
    private readonly IVietQrService _vietQrService;
    private readonly IPrintService _printService;
    private readonly IBackupAndSecurityService _backupService;
    private readonly IAuthService _authService;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public TableItemViewModel TableItem { get; }
    public LiveBillDto LiveBill { get; private set; }

    [ObservableProperty]
    private string _customerPhone = string.Empty;

    [ObservableProperty]
    private string _customerName = string.Empty;

    [ObservableProperty]
    private Customer? _customer;

    [ObservableProperty]
    private decimal _discountPercent = 0;

    [ObservableProperty]
    private decimal _discountAmount = 0;

    [ObservableProperty]
    private int _availablePoints = 0;

    [ObservableProperty]
    private int _pointsToUse = 0;

    [ObservableProperty]
    private decimal _pointDiscountAmount = 0;

    [ObservableProperty]
    private decimal _finalAmount;

    [ObservableProperty]
    private decimal _cashGiven = 0;

    [ObservableProperty]
    private decimal _changeDue = 0;

    [ObservableProperty]
    private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;

    [ObservableProperty]
    private BitmapImage? _vietQrImage;

    [ObservableProperty]
    private string _bankInfoText = string.Empty;

    [ObservableProperty]
    private string _qrMemoText = string.Empty;

    [ObservableProperty]
    private bool _printReceiptAfterCheckout = true;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isProcessing;

    public bool IsCashMode => SelectedPaymentMethod == PaymentMethod.Cash;
    public bool IsVietQrMode => SelectedPaymentMethod == PaymentMethod.VietQr;

    public ObservableCollection<PaymentMethod> AvailablePaymentMethods { get; } = new()
    {
        PaymentMethod.Cash,
        PaymentMethod.VietQr,
        PaymentMethod.BankTransfer
    };

    public event Action? RequestClose;
    public event Action<Invoice>? OnCheckoutComplete;

    public CheckoutDialogViewModel(
        IBillingService billingService,
        ICustomerService customerService,
        IVietQrService vietQrService,
        IPrintService printService,
        IBackupAndSecurityService backupService,
        IAuthService authService,
        IDbContextFactory<AppDbContext> contextFactory,
        TableItemViewModel tableItem)
    {
        _billingService = billingService;
        _customerService = customerService;
        _vietQrService = vietQrService;
        _printService = printService;
        _backupService = backupService;
        _authService = authService;
        _contextFactory = contextFactory;
        TableItem = tableItem;

        int sessionId = tableItem.Model.CurrentSessionId ?? 0;
        LiveBill = _billingService.CalculateLiveBill(sessionId);

        // Nạp thông tin khách nếu đã có sẵn từ phiên
        if (LiveBill.Customer != null)
        {
            Customer = LiveBill.Customer;
            CustomerPhone = Customer.PhoneNumber;
            CustomerName = Customer.FullName;
            AvailablePoints = Customer.CurrentPoints;
            DiscountPercent = Customer.Tier?.DiscountPercentHour ?? 0;
        }

        Recalculate();
        SetExactCash();
    }

    [RelayCommand]
    private void SearchOrRegisterCustomer()
    {
        if (string.IsNullOrWhiteSpace(CustomerPhone)) return;

        var found = _customerService.FindByPhone(CustomerPhone);
        if (found != null)
        {
            Customer = found;
            CustomerName = found.FullName;
            AvailablePoints = found.CurrentPoints;
            DiscountPercent = found.Tier?.DiscountPercentHour ?? 0;
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(CustomerName))
            {
                Customer = _customerService.CreateOrUpdateCustomer(CustomerPhone, CustomerName);
                AvailablePoints = Customer.CurrentPoints;
                DiscountPercent = 0;
            }
        }
        Recalculate();
    }

    partial void OnDiscountPercentChanged(decimal value) => Recalculate();
    partial void OnPointsToUseChanged(int value) => Recalculate();
    
    partial void OnSelectedPaymentMethodChanged(PaymentMethod value)
    {
        OnPropertyChanged(nameof(IsCashMode));
        OnPropertyChanged(nameof(IsVietQrMode));
        UpdatePaymentView();
    }

    partial void OnCashGivenChanged(decimal value)
    {
        CalculateChange();
    }

    private void CalculateChange()
    {
        ChangeDue = Math.Max(0, CashGiven - FinalAmount);
    }

    [RelayCommand]
    public void SetExactCash()
    {
        CashGiven = FinalAmount;
        CalculateChange();
    }

    [RelayCommand]
    public void SetCash100k() => SetFastCash(100000m);

    [RelayCommand]
    public void SetCash200k() => SetFastCash(200000m);

    [RelayCommand]
    public void SetCash500k() => SetFastCash(500000m);

    private void SetFastCash(decimal amount)
    {
        CashGiven = amount;
        CalculateChange();
    }

    private void Recalculate()
    {
        // 1. Giảm giá %
        decimal subTotal = LiveBill.SubTotal;
        DiscountAmount = Math.Round(subTotal * (DiscountPercent / 100m) / 1000m, MidpointRounding.AwayFromZero) * 1000m;

        // 2. Điểm tích lũy (1 điểm = 1,000đ)
        if (PointsToUse > AvailablePoints) PointsToUse = AvailablePoints;
        PointDiscountAmount = PointsToUse * 1000m;

        // 3. Số tiền cuối cùng
        FinalAmount = Math.Max(0, subTotal - DiscountAmount - PointDiscountAmount);

        CalculateChange();
        UpdatePaymentView();
    }

    private void UpdatePaymentView()
    {
        if (SelectedPaymentMethod == PaymentMethod.VietQr)
        {
            GenerateDynamicVietQr();
        }
        else
        {
            VietQrImage = null;
        }
    }

    private void GenerateDynamicVietQr()
    {
        try
        {
            using var context = _contextFactory.CreateDbContext();
            string bankBin = context.SystemSettings.Find("BankBin")?.SettingValue ?? "970422";
            string bankAcc = context.SystemSettings.Find("BankAccountNo")?.SettingValue ?? "012345678999";
            string bankOwner = context.SystemSettings.Find("BankAccountName")?.SettingValue ?? "CLB BIDA J BILLIARDS";
            string content = $"BIDA {TableItem.TableNumber}";

            string payload = _vietQrService.GenerateEmvCoPayload(bankBin, bankAcc, FinalAmount, content);
            VietQrImage = _vietQrService.GenerateQrBitmap(payload);

            BankInfoText = $"STK: {bankAcc} • Chủ TK: {bankOwner}";
            QrMemoText = $"Nội dung CK: {content}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Lỗi sinh mã VietQR: {ex.Message}";
        }
    }

    [RelayCommand]
    private void UseAllPoints()
    {
        // Sử dụng tối đa điểm nhưng không vượt quá tổng hóa đơn
        decimal remaining = Math.Max(0, LiveBill.SubTotal - DiscountAmount);
        int maxPointsNeeded = (int)(remaining / 1000m);
        PointsToUse = Math.Min(AvailablePoints, maxPointsNeeded);
    }

    [RelayCommand]
    private void ConfirmCheckout()
    {
        IsProcessing = true;
        ErrorMessage = null;

        try
        {
            int cashierId = _authService.CurrentUser?.UserId ?? 1;
            int sessionId = TableItem.Model.CurrentSessionId ?? 0;

            // Chốt hóa đơn
            var invoice = _billingService.CheckoutTable(
                sessionId,
                cashierId,
                SelectedPaymentMethod,
                DiscountPercent,
                PointsToUse,
                Notes
            );

            // In hóa đơn nếu người dùng chọn
            if (PrintReceiptAfterCheckout)
            {
                _printService.PrintReceipt(invoice);
            }

            // Tự động sao lưu dữ liệu ngay sau khi thanh toán thành công
            try
            {
                _backupService.CreateBackup("ON_PAYMENT", true);
            }
            catch { }

            OnCheckoutComplete?.Invoke(invoice);
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Lỗi thanh toán: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }
}
