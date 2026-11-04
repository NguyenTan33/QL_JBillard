using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.ViewModels;

public partial class OrderQueueViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private readonly IAuthService _authService;
    private readonly ITableService _tableService;
    private readonly DispatcherTimer _tickerTimer;

    [ObservableProperty]
    private ObservableCollection<OrderItemCardViewModel> _filteredOrders = new();

    private List<OrderItemCardViewModel> _allOrders = new();

    [ObservableProperty]
    private string _selectedFilter = "PENDING"; // PENDING, ALL, COMPLETED, CANCELLED

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private string? _selectedTableNumber = "TẤT CẢ BÀN";

    [ObservableProperty]
    private ObservableCollection<string> _activeTableList = new();

    [ObservableProperty]
    private int _pendingCount;

    [ObservableProperty]
    private int _overdueCount;

    [ObservableProperty]
    private int _completedCount;

    // Sự kiện cảnh báo món trễ hơn 10 phút gửi lên MainViewModel để bắn Notion Toast
    public event Action<OrderItemCardViewModel>? OnOverdueOrderAlert;
    
    // Sự kiện thông báo cập nhật tiền bàn sau khi hoàn thành/hủy món
    public event Action? OnOrderStateChanged;

    public OrderQueueViewModel(IInventoryService inventoryService, IAuthService authService, ITableService tableService)
    {
        _inventoryService = inventoryService;
        _authService = authService;
        _tableService = tableService;

        _tickerTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _tickerTimer.Tick += (s, e) => TickWaitTimes();
        _tickerTimer.Start();

        LoadOrders();
    }

    public void LoadOrders()
    {
        var rawItems = _inventoryService.GetOrderItemsForQueue(onlyPending: false);

        // Món nào gọi trước lên trước (FIFO)
        var cards = rawItems.OrderBy(o => o.OrderedAt).Select(i => new OrderItemCardViewModel(i)).ToList();

        // Cập nhật danh sách bàn đang có order
        var tables = new List<string> { "TẤT CẢ BÀN" };
        var tableNames = cards.Select(c => c.TableNumber).Distinct().OrderBy(t => t);
        tables.AddRange(tableNames);
        ActiveTableList = new ObservableCollection<string>(tables);

        _allOrders = cards;
        ApplyFilter();
    }

    private void TickWaitTimes()
    {
        bool hasOverdueChanges = false;
        foreach (var item in _allOrders.Where(o => !o.IsCompleted && !o.IsCancelled))
        {
            item.UpdateWaitTime();

            // Nếu món trễ hơn 10 phút và chưa bắn alert thông báo
            if (item.IsOverdue && !item.HasAlerted)
            {
                item.HasAlerted = true;
                hasOverdueChanges = true;
                OnOverdueOrderAlert?.Invoke(item);
            }
        }

        UpdateCounters();

        if (hasOverdueChanges && SelectedFilter == "PENDING")
        {
            // Cập nhật lại thứ tự ưu tiên nếu có món quá hạn
        }
    }

    private void UpdateCounters()
    {
        PendingCount = _allOrders.Count(o => !o.IsCompleted && !o.IsCancelled);
        OverdueCount = _allOrders.Count(o => !o.IsCompleted && !o.IsCancelled && o.IsOverdue);
        CompletedCount = _allOrders.Count(o => o.IsCompleted && !o.IsCancelled);
    }

    partial void OnSelectedFilterChanged(string value) => ApplyFilter();
    partial void OnSearchKeywordChanged(string value) => ApplyFilter();
    partial void OnSelectedTableNumberChanged(string? value) => ApplyFilter();

    [RelayCommand]
    private void SetFilter(string filter)
    {
        SelectedFilter = filter;
    }

    private void ApplyFilter()
    {
        var query = _allOrders.AsEnumerable();

        // 1. Lọc theo trạng thái
        query = SelectedFilter switch
        {
            "PENDING" => query.Where(o => !o.IsCompleted && !o.IsCancelled),
            "COMPLETED" => query.Where(o => o.IsCompleted && !o.IsCancelled),
            "CANCELLED" => query.Where(o => o.IsCancelled),
            _ => query
        };

        // 2. Lọc theo bàn
        if (!string.IsNullOrEmpty(SelectedTableNumber) && SelectedTableNumber != "TẤT CẢ BÀN")
        {
            query = query.Where(o => o.TableNumber.Equals(SelectedTableNumber, StringComparison.OrdinalIgnoreCase));
        }

        // 3. Tìm kiếm theo tên món
        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            string kw = SearchKeyword.Trim().ToLower();
            query = query.Where(o => o.ProductName.ToLower().Contains(kw) || o.TableNumber.ToLower().Contains(kw));
        }

        // Ưu tiên: Món trễ trước, món gọi trước lên trước (FIFO)
        var result = query.OrderByDescending(o => o.IsOverdue)
                          .ThenBy(o => o.OrderedAt)
                          .ToList();

        FilteredOrders = new ObservableCollection<OrderItemCardViewModel>(result);
        UpdateCounters();
    }

    [RelayCommand]
    private void CompleteItem(OrderItemCardViewModel? item)
    {
        if (item == null || item.IsCompleted || item.IsCancelled) return;

        int userId = _authService.CurrentUser?.UserId ?? 1;
        bool ok = _inventoryService.CompleteOrderItem(item.OrderItemId, userId);
        if (ok)
        {
            item.IsCompleted = true;
            item.UpdateWaitTime();
            ApplyFilter();
            OnOrderStateChanged?.Invoke();
        }
    }

    [RelayCommand]
    private void CancelItem(OrderItemCardViewModel? item)
    {
        if (item == null || item.IsCancelled) return;

        int userId = _authService.CurrentUser?.UserId ?? 1;
        bool ok = _inventoryService.CancelOrderItem(item.OrderItemId, userId, "Hủy trực tiếp từ danh mục Order");
        if (ok)
        {
            item.IsCancelled = true;
            item.UpdateWaitTime();
            ApplyFilter();
            OnOrderStateChanged?.Invoke();
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadOrders();
    }
}
