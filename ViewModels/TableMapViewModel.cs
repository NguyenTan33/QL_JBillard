using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.ViewModels;

public partial class TableMapViewModel : ObservableObject, IDisposable
{
    private readonly ITableService _tableService;
    private readonly IAuthService _authService;
    private readonly IShiftService _shiftService;
    private readonly DispatcherTimer _timer;

    [ObservableProperty]
    private ObservableCollection<Floor> _floors = new();

    [ObservableProperty]
    private Floor? _selectedFloor;

    [ObservableProperty]
    private ObservableCollection<Area> _areas = new();

    [ObservableProperty]
    private Area? _selectedArea;

    [ObservableProperty]
    private ObservableCollection<TableItemViewModel> _allTables = new();

    [ObservableProperty]
    private ObservableCollection<TableItemViewModel> _filteredTables = new();

    [ObservableProperty]
    private TableItemViewModel? _selectedTable;

    [ObservableProperty]
    private int _totalTableCount;

    [ObservableProperty]
    private int _availableTableCount;

    [ObservableProperty]
    private int _occupiedTableCount;

    [ObservableProperty]
    private int _reservedTableCount;

    [ObservableProperty]
    private int _maintenanceTableCount;

    [ObservableProperty]
    private decimal _totalLiveRevenue;

    [ObservableProperty]
    private string _filterStatus = "ALL"; // ALL, AVAILABLE, OCCUPIED, MAINTENANCE

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    // Sự kiện yêu cầu View hiển thị Dialog
    public event Action<TableItemViewModel>? RequestOpenTableDialog;
    public event Action<TableItemViewModel>? RequestOrderDialog;
    public event Action<TableItemViewModel>? RequestCheckoutDialog;
    public event Action<TableItemViewModel>? RequestSwitchTableDialog;
    public event Action<string>? ShowNotification;
    public event Action? RequestNavigateToShift;

    public TableMapViewModel(ITableService tableService, IAuthService authService, IShiftService shiftService)
    {
        _tableService = tableService;
        _authService = authService;
        _shiftService = shiftService;

        // Timer đếm giây thời gian thực (1 giây/lần)
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (s, e) =>
        {
            foreach (var table in AllTables.Where(t => t.IsOccupied))
            {
                table.TickTimer();
            }
            TotalLiveRevenue = AllTables.Where(t => t.IsOccupied).Sum(t => t.EstimatedTotalCost);
        };
        _timer.Start();

        LoadData();
    }

    [RelayCommand]
    public void LoadData()
    {
        var floors = _tableService.GetAllFloors();
        var allFloors = new List<Floor>
        {
            new Floor { FloorId = 0, FloorName = "── Tất Cả Tầng ──" }
        };
        allFloors.AddRange(floors);
        Floors = new ObservableCollection<Floor>(allFloors);
        SelectedFloor = Floors.FirstOrDefault();

        var areas = _tableService.GetAllAreas();
        var allAreas = new List<Area>
        {
            new Area { AreaId = 0, AreaName = "── Tất Cả Khu Vực ──" }
        };
        allAreas.AddRange(areas);
        Areas = new ObservableCollection<Area>(allAreas);
        SelectedArea = Areas.FirstOrDefault();

        var tables = _tableService.GetAllTables();
        AllTables = new ObservableCollection<TableItemViewModel>(tables.Select(t => new TableItemViewModel(t)));

        ApplyFilter();
    }

    public void ApplyFilter()
    {
        var query = AllTables.AsEnumerable();

        if (SelectedFloor != null && SelectedFloor.FloorId > 0)
        {
            query = query.Where(t => t.Model.Area?.FloorId == SelectedFloor.FloorId);
        }

        if (SelectedArea != null && SelectedArea.AreaId > 0)
        {
            query = query.Where(t => t.Model.AreaId == SelectedArea.AreaId);
        }

        if (FilterStatus == "AVAILABLE")
        {
            query = query.Where(t => t.IsAvailable);
        }
        else if (FilterStatus == "OCCUPIED")
        {
            query = query.Where(t => t.IsOccupied);
        }
        else if (FilterStatus == "RESERVED")
        {
            query = query.Where(t => t.IsReserved);
        }
        else if (FilterStatus == "MAINTENANCE")
        {
            query = query.Where(t => t.IsMaintenance);
        }

        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            query = query.Where(t => t.TableNumber.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ||
                                     t.TableTypeName.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ||
                                     t.AreaName.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase));
        }

        FilteredTables = new ObservableCollection<TableItemViewModel>(query);

        TotalTableCount = AllTables.Count;
        AvailableTableCount = AllTables.Count(t => t.IsAvailable);
        OccupiedTableCount = AllTables.Count(t => t.IsOccupied);
        ReservedTableCount = AllTables.Count(t => t.IsReserved);
        MaintenanceTableCount = AllTables.Count(t => t.IsMaintenance);
        TotalLiveRevenue = AllTables.Where(t => t.IsOccupied).Sum(t => t.EstimatedTotalCost);
    }

    partial void OnSelectedFloorChanged(Floor? value) => ApplyFilter();
    partial void OnSelectedAreaChanged(Area? value) => ApplyFilter();
    partial void OnFilterStatusChanged(string value) => ApplyFilter();
    partial void OnSearchKeywordChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void ClearAreaFilter()
    {
        SelectedFloor = Floors.FirstOrDefault();
        SelectedArea = Areas.FirstOrDefault();
        FilterStatus = "ALL";
        SearchKeyword = string.Empty;
    }

    [RelayCommand]
    private void OpenTable(TableItemViewModel? table)
    {
        if (table == null || (!table.IsAvailable && !table.IsReserved)) return;

        if (_shiftService.CurrentActiveShift == null)
        {
            ShowNotification?.Invoke("⚠️ CHƯA MỞ CA TRỰC! Vui lòng mở ca trước khi mở bàn phục vụ khách.");
            RequestNavigateToShift?.Invoke();
            return;
        }

        RequestOpenTableDialog?.Invoke(table);
    }

    [RelayCommand]
    private void QuickOpenTable(TableItemViewModel? table)
    {
        if (table == null || (!table.IsAvailable && !table.IsReserved)) return;

        if (_shiftService.CurrentActiveShift == null)
        {
            ShowNotification?.Invoke("⚠️ CHƯA MỞ CA TRỰC! Vui lòng mở ca trước khi mở bàn phục vụ khách.");
            RequestNavigateToShift?.Invoke();
            return;
        }

        RequestOpenTableDialog?.Invoke(table);
    }

    [RelayCommand]
    private void Order(TableItemViewModel? table)
    {
        if (table == null || !table.IsOccupied) return;
        RequestOrderDialog?.Invoke(table);
    }

    [RelayCommand]
    private void Checkout(TableItemViewModel? table)
    {
        if (table == null || !table.IsOccupied) return;
        RequestCheckoutDialog?.Invoke(table);
    }

    [RelayCommand]
    private void SwitchTable(TableItemViewModel? table)
    {
        if (table == null || !table.IsOccupied) return;
        RequestSwitchTableDialog?.Invoke(table);
    }

    [RelayCommand]
    private void ToggleReserve(TableItemViewModel? table)
    {
        if (table == null || table.IsOccupied) return;
        int userId = _authService.CurrentUser?.UserId ?? 1;
        var newStatus = table.Status == TableStatus.Reserved ? TableStatus.Available : TableStatus.Reserved;
        _tableService.UpdateTableStatus(table.TableId, newStatus, userId);
        RefreshTable(table.TableId);
        string msg = newStatus == TableStatus.Reserved
            ? $"Đã ghi nhận ĐẶT BÀN {table.TableNumber} thành công!"
            : $"Đã hủy đặt bàn {table.TableNumber}, đưa về trạng thái BÀN TRỐNG.";
        ShowNotification?.Invoke(msg);
    }

    [RelayCommand]
    private void ToggleMaintenance(TableItemViewModel? table)
    {
        if (table == null || table.IsOccupied) return;
        int userId = _authService.CurrentUser?.UserId ?? 1;
        var newStatus = table.Status == TableStatus.Maintenance ? TableStatus.Available : TableStatus.Maintenance;
        _tableService.UpdateTableStatus(table.TableId, newStatus, userId);
        RefreshTable(table.TableId);
    }

    public void RefreshTable(int tableId)
    {
        var updated = _tableService.GetTableById(tableId);
        var existing = AllTables.FirstOrDefault(t => t.TableId == tableId);
        if (existing != null && updated != null)
        {
            existing.Model.Status = updated.Status;
            existing.Model.CurrentSessionId = updated.CurrentSessionId;
            existing.Model.CurrentSession = updated.CurrentSession;
            existing.UpdateFromModel();
        }
        ApplyFilter();
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
