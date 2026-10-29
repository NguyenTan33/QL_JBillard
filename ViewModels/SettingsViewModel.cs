using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QL_JBillard.Data;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.ViewModels;

public class BackupIntervalOption
{
    public int Minutes { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IBackupAndSecurityService _backupService;

    [ObservableProperty]
    private string _clubName = string.Empty;

    [ObservableProperty]
    private string _clubAddress = string.Empty;

    [ObservableProperty]
    private string _clubPhone = string.Empty;

    [ObservableProperty]
    private string _bankBin = string.Empty;

    [ObservableProperty]
    private string _bankAccountNo = string.Empty;

    [ObservableProperty]
    private string _bankAccountName = string.Empty;

    [ObservableProperty]
    private int _autoBackupIntervalMinutes = 30;

    [ObservableProperty]
    private ObservableCollection<BackupIntervalOption> _intervalOptions = new()
    {
        new BackupIntervalOption { Minutes = 15, DisplayName = "15 phút" },
        new BackupIntervalOption { Minutes = 30, DisplayName = "30 phút (Mặc định)" },
        new BackupIntervalOption { Minutes = 60, DisplayName = "1 giờ (60 phút)" },
        new BackupIntervalOption { Minutes = 120, DisplayName = "2 giờ (120 phút)" },
        new BackupIntervalOption { Minutes = 240, DisplayName = "4 giờ (240 phút)" },
        new BackupIntervalOption { Minutes = 480, DisplayName = "8 giờ (480 phút)" },
        new BackupIntervalOption { Minutes = 1440, DisplayName = "24 giờ (1 ngày)" }
    };

    [ObservableProperty]
    private BackupIntervalOption? _selectedInterval;

    [ObservableProperty]
    private ObservableCollection<DatabaseBackupLog> _backupHistory = new();

    [ObservableProperty]
    private string? _message;

    public SettingsViewModel(IDbContextFactory<AppDbContext> contextFactory, IBackupAndSecurityService backupService)
    {
        _contextFactory = contextFactory;
        _backupService = backupService;
        LoadSettings();
        LoadBackups();
    }

    [RelayCommand]
    public void LoadSettings()
    {
        using var context = _contextFactory.CreateDbContext();
        ClubName = context.SystemSettings.Find("ClubName")?.SettingValue ?? "";
        ClubAddress = context.SystemSettings.Find("ClubAddress")?.SettingValue ?? "";
        ClubPhone = context.SystemSettings.Find("ClubPhone")?.SettingValue ?? "";
        BankBin = context.SystemSettings.Find("BankBin")?.SettingValue ?? "970422";
        BankAccountNo = context.SystemSettings.Find("BankAccountNo")?.SettingValue ?? "";
        BankAccountName = context.SystemSettings.Find("BankAccountName")?.SettingValue ?? "";

        var intervalStr = context.SystemSettings.Find("AutoBackupIntervalMinutes")?.SettingValue;
        if (int.TryParse(intervalStr, out int val) && val >= 5)
        {
            AutoBackupIntervalMinutes = val;
        }
        else
        {
            AutoBackupIntervalMinutes = 30;
        }

        SelectedInterval = IntervalOptions.FirstOrDefault(o => o.Minutes == AutoBackupIntervalMinutes)
                           ?? IntervalOptions.FirstOrDefault(o => o.Minutes == 30);
    }

    [RelayCommand]
    public void LoadBackups()
    {
        var list = _backupService.GetBackupHistory();
        BackupHistory = new ObservableCollection<DatabaseBackupLog>(list);
    }

    [RelayCommand]
    private void SaveSettings()
    {
        using var context = _contextFactory.CreateDbContext();
        UpdateSetting(context, "ClubName", ClubName);
        UpdateSetting(context, "ClubAddress", ClubAddress);
        UpdateSetting(context, "ClubPhone", ClubPhone);
        UpdateSetting(context, "BankBin", BankBin);
        UpdateSetting(context, "BankAccountNo", BankAccountNo);
        UpdateSetting(context, "BankAccountName", BankAccountName);

        if (SelectedInterval != null)
        {
            AutoBackupIntervalMinutes = SelectedInterval.Minutes;
        }
        UpdateSetting(context, "AutoBackupIntervalMinutes", AutoBackupIntervalMinutes.ToString());

        context.SaveChanges();

        // Cập nhật ngay chu kỳ chạy tự động sao lưu nền
        _backupService.StartPeriodicAutoBackup(AutoBackupIntervalMinutes);

        Message = $"Đã lưu cài đặt hệ thống thành công! (Tự động sao lưu: {AutoBackupIntervalMinutes} phút/lần)";
    }

    private static void UpdateSetting(AppDbContext context, string key, string val)
    {
        var s = context.SystemSettings.Find(key);
        if (s != null)
        {
            s.SettingValue = val;
            s.UpdatedAt = DateTime.Now;
        }
        else
        {
            context.SystemSettings.Add(new SystemSetting
            {
                SettingKey = key,
                SettingValue = val,
                Description = key,
                UpdatedAt = DateTime.Now
            });
        }
    }

    [RelayCommand]
    private void CreateBackupNow()
    {
        try
        {
            var log = _backupService.CreateBackup("MANUAL", true);
            LoadBackups();
            Message = $"Đã tạo bản sao lưu mã hóa AES-256 an toàn: {log.BackupFileName}";
        }
        catch (Exception ex)
        {
            Message = $"Lỗi sao lưu: {ex.Message}";
        }
    }

    [RelayCommand]
    private void FactoryResetDatabase()
    {
        var result = System.Windows.MessageBox.Show(
            "CẢNH BÁO NGUY HIỂM: Bạn có chắc chắn muốn XÓA SẠCH toàn bộ cơ sở dữ liệu (hóa đơn, phiên chơi, ca trực, khách hàng)?\n\nHệ thống sẽ chỉ giữ lại đúng tài khoản Quản trị viên (admin) và thiết lập tiền két ban đầu là 1.000.000 đ.",
            "Xác nhận xóa sạch cơ sở dữ liệu quán",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                DatabaseCleaner.ResetAndCleanDatabase(context);
                LoadSettings();
                LoadBackups();
                Message = "Đã xóa sạch cơ sở dữ liệu thành công! Tiền két khởi tạo: 1.000.000 đ. Chỉ còn tài khoản admin.";
                System.Windows.MessageBox.Show("Đã hoàn tất dọn dẹp cơ sở dữ liệu sạch sẽ!", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Message = $"Lỗi xóa dữ liệu: {ex.Message}";
                System.Windows.MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
