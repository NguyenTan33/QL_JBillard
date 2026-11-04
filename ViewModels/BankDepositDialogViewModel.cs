using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using QL_JBillard.Data;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.ViewModels;

public partial class BankDepositDialogViewModel : ObservableObject
{
    private readonly IShiftService _shiftService;
    private readonly IAuthService _authService;

    [ObservableProperty]
    private decimal _currentDrawerCash;

    [ObservableProperty]
    private decimal _depositAmount;

    [ObservableProperty]
    private string _bankName = "MBBank (Napas 247)";

    [ObservableProperty]
    private string _bankAccountNo = "012345678999";

    [ObservableProperty]
    private string _bankAccountName = "CLB BIDA J BILLIARDS";

    [ObservableProperty]
    private string? _imagePath;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _errorMessage;

    public event Action<BankDepositSlip>? OnDepositSuccess;
    public event Action? RequestClose;

    public BankDepositDialogViewModel(IShiftService shiftService, IAuthService authService, decimal currentDrawerCash)
    {
        _shiftService = shiftService;
        _authService = authService;
        CurrentDrawerCash = currentDrawerCash;
        DepositAmount = currentDrawerCash > 0 ? currentDrawerCash : 0;
        Notes = "Nộp tiền mặt doanh thu vào tài khoản ngân hàng của quán";
    }

    [RelayCommand]
    private void QuickFillMax()
    {
        DepositAmount = CurrentDrawerCash;
    }

    [RelayCommand]
    private void SelectReceiptImage()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Chọn ảnh chụp hóa đơn / biên lai chuyển tiền ngân hàng",
            Filter = "Ảnh hóa đơn/biên lai (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp|Tất cả tệp (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var receiptDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppData", "Receipts");
                if (!Directory.Exists(receiptDir))
                {
                    Directory.CreateDirectory(receiptDir);
                }

                string extension = Path.GetExtension(openFileDialog.FileName);
                string newFileName = $"Receipt_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}{extension}";
                string destPath = Path.Combine(receiptDir, newFileName);

                File.Copy(openFileDialog.FileName, destPath, true);
                ImagePath = destPath;
                ErrorMessage = null;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi tải ảnh: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void ConfirmDeposit()
    {
        if (DepositAmount <= 0)
        {
            ErrorMessage = "⚠️ Vui lòng nhập số tiền nộp lớn hơn 0 VNĐ!";
            return;
        }

        if (DepositAmount > CurrentDrawerCash)
        {
            ErrorMessage = $"⚠️ Số tiền nộp ({DepositAmount:N0} đ) vượt quá số tiền thực giữ trong két ({CurrentDrawerCash:N0} đ)!";
            return;
        }

        int userId = _authService.CurrentUser?.UserId ?? 1;

        try
        {
            var slip = _shiftService.CreateBankDepositSlip(userId, DepositAmount, ImagePath, Notes);
            OnDepositSuccess?.Invoke(slip);
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Lỗi tạo phiếu nộp: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke();
    }
}
