using System;
using CommunityToolkit.Mvvm.ComponentModel;
using QL_JBillard.Domain.Entities;

namespace QL_JBillard.ViewModels;

public partial class OrderItemCardViewModel : ObservableObject
{
    public int OrderItemId { get; set; }
    public int SessionId { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string AreaName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime OrderedAt { get; set; }
    public string? ImagePath { get; set; }

    public bool IsComboItem { get; set; }
    public string? ComboName { get; set; }

    [ObservableProperty]
    private TimeSpan _waitTime;

    [ObservableProperty]
    private string _waitTimeString = "00:00";

    [ObservableProperty]
    private bool _isOverdue; // Trễ hơn 10 phút

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private bool _isCancelled;

    [ObservableProperty]
    private string _statusText = "Đang chờ phục vụ";

    [ObservableProperty]
    private string _statusBadgeColor = "#3B82F6"; // Blue

    public bool HasAlerted { get; set; } = false;

    public OrderItemCardViewModel(OrderItem item)
    {
        OrderItemId = item.OrderItemId;
        SessionId = item.SessionId;
        TableNumber = item.Session?.Table?.TableNumber ?? $"Bàn #{item.Session?.TableId}";
        AreaName = item.Session?.Table?.Area?.AreaName ?? "Khu vực";
        ProductName = item.Product?.ProductName ?? "Mặt hàng";
        CategoryName = item.Product?.Category?.CategoryName ?? "F&B";
        Quantity = item.Quantity;
        UnitPrice = item.UnitPrice;
        TotalAmount = item.TotalAmount;
        OrderedAt = item.OrderedAt;
        ImagePath = item.Product?.ImagePath;

        IsCompleted = item.IsCompleted;
        IsCancelled = item.IsCancelled;
        IsComboItem = item.IsComboItem;
        ComboName = item.ComboName;

        UpdateWaitTime();
    }

    public void UpdateWaitTime()
    {
        if (IsCompleted)
        {
            StatusText = "✓ Đã phục vụ";
            StatusBadgeColor = "#059669"; // Emerald
            IsOverdue = false;
            WaitTimeString = "Hoàn thành";
            return;
        }

        if (IsCancelled)
        {
            StatusText = "✕ Đã báo hủy";
            StatusBadgeColor = "#EF4444"; // Red
            IsOverdue = false;
            WaitTimeString = "Đã hủy";
            return;
        }

        var now = DateTime.Now;
        WaitTime = now >= OrderedAt ? now - OrderedAt : TimeSpan.Zero;

        if (WaitTime.TotalHours >= 1)
        {
            WaitTimeString = $"{(int)WaitTime.TotalHours}h {WaitTime.Minutes:D2}m";
        }
        else
        {
            WaitTimeString = $"{WaitTime.Minutes:D2}:{WaitTime.Seconds:D2}";
        }

        if (WaitTime.TotalMinutes >= 10)
        {
            IsOverdue = true;
            StatusText = $"🔴 TRỄ {(int)WaitTime.TotalMinutes} PHÚT!";
            StatusBadgeColor = "#DC2626"; // Strong Red
        }
        else if (WaitTime.TotalMinutes >= 5)
        {
            IsOverdue = false;
            StatusText = $"🟡 Chờ {(int)WaitTime.TotalMinutes} phút";
            StatusBadgeColor = "#D97706"; // Amber
        }
        else
        {
            IsOverdue = false;
            StatusText = "🟢 Đang chuẩn bị";
            StatusBadgeColor = "#059669"; // Green
        }
    }
}
