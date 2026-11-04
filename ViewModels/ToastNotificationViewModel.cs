using System;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace QL_JBillard.ViewModels;

public enum ToastType
{
    Success, // Xanh lá - Mở bàn thành công, thao tác tốt
    Danger,  // ĐỎ CẢNH BÁO - DÀNH RIÊNG CHO TRỄ MÓN (>10 phút)
    Warning, // Vàng cam
    Info     // Xanh dương
}

public partial class ToastNotificationViewModel : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private string _tableInfo = string.Empty;

    [ObservableProperty]
    private string _timeAgo = string.Empty;

    [ObservableProperty]
    private ToastType _type = ToastType.Success;

    [ObservableProperty]
    private bool _isVisible = true;

    // Màu viền thẻ thông báo
    public string BorderBrushColor => Type switch
    {
        ToastType.Danger => "#EF4444",   // ĐỎ CẢNH BÁO CHO TRỄ MÓN
        ToastType.Warning => "#F59E0B",  // VÀNG CAM
        ToastType.Info => "#3B82F6",     // XANH DƯƠNG
        _ => "#10B981"                   // XANH LÁ MƯỢT CHO MỞ BÀN / THÀNH CÔNG
    };

    // Màu nền icon
    public string IconBgColor => Type switch
    {
        ToastType.Danger => "#DC2626",   // Đỏ
        ToastType.Warning => "#D97706",  // Cam
        ToastType.Info => "#2563EB",     // Lam
        _ => "#059669"                   // Xanh ngọc lục bảo
    };

    // Màu chữ tiêu đề
    public string TitleColor => Type switch
    {
        ToastType.Danger => "#FCA5A5",   // Chữ đỏ sáng
        ToastType.Warning => "#FDE68A",  // Chữ vàng sáng
        ToastType.Info => "#93C5FD",     // Chữ lam sáng
        _ => "#6EE7B7"                   // Chữ xanh ngọc sáng
    };

    // Biểu tượng icon
    public string IconText => Type switch
    {
        ToastType.Danger => "⚠️",
        ToastType.Warning => "🔔",
        ToastType.Info => "ℹ️",
        _ => "✓"
    };

    private readonly DispatcherTimer _dismissTimer;

    public event Action<ToastNotificationViewModel>? OnDismissed;

    public ToastNotificationViewModel(string title, string message, string tableInfo = "", ToastType type = ToastType.Success)
    {
        Title = title;
        Message = message;
        TableInfo = tableInfo;
        Type = type;
        TimeAgo = DateTime.Now.ToString("HH:mm:ss");

        // Tự động đóng sau đúng 7 giây theo yêu cầu
        _dismissTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(7)
        };
        _dismissTimer.Tick += (s, e) =>
        {
            _dismissTimer.Stop();
            Dismiss();
        };
        _dismissTimer.Start();
    }

    [RelayCommand]
    public void Dismiss()
    {
        _dismissTimer.Stop();
        IsVisible = false;
        OnDismissed?.Invoke(this);
    }
}
