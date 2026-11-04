using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.ViewModels;

public partial class TableManagementViewModel : ObservableObject
{
    private readonly ITableService _tableService;
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string _activeSubTab = "TABLES"; // TABLES, AREAS_FLOORS, TYPES_PRICES

    [ObservableProperty]
    private string? _message;

    // --- 1. DỮ LIỆU & FORM QUẢN LÝ BÀN ---
    [ObservableProperty]
    private ObservableCollection<Table> _tablesList = new();

    [ObservableProperty]
    private Table? _selectedTable;

    [ObservableProperty]
    private int _formTableId;

    [ObservableProperty]
    private string _formTableNumber = string.Empty;

    [ObservableProperty]
    private Area? _formTableSelectedArea;

    [ObservableProperty]
    private TableType? _formTableSelectedType;

    [ObservableProperty]
    private bool _isEditingTable;

    // --- 2. DỮ LIỆU & FORM KHU VỰC & TẦNG ---
    [ObservableProperty]
    private ObservableCollection<Floor> _floorsList = new();

    [ObservableProperty]
    private ObservableCollection<Area> _areasList = new();

    // Form Tầng
    [ObservableProperty]
    private int _formFloorId;

    [ObservableProperty]
    private string _formFloorName = string.Empty;

    [ObservableProperty]
    private int _formFloorOrderIndex = 1;

    [ObservableProperty]
    private bool _isEditingFloor;

    // Form Khu Vực
    [ObservableProperty]
    private int _formAreaId;

    [ObservableProperty]
    private string _formAreaName = string.Empty;

    [ObservableProperty]
    private string? _formAreaDescription;

    [ObservableProperty]
    private Floor? _formAreaSelectedFloor;

    [ObservableProperty]
    private bool _isEditingArea;

    // --- 3. DỮ LIỆU & FORM LOẠI BÀN & GIÁ GIỜ ---
    [ObservableProperty]
    private ObservableCollection<TableType> _tableTypesList = new();

    [ObservableProperty]
    private int _formTypeId;

    [ObservableProperty]
    private string _formTypeName = string.Empty;

    [ObservableProperty]
    private decimal _formDefaultHourlyRate = 50_000;

    [ObservableProperty]
    private int _formMinPlayMinutes = 15;

    [ObservableProperty]
    private string _formColorCode = "#10B981";

    [ObservableProperty]
    private bool _isEditingType;

    // --- 4. DỮ LIỆU & FORM KHUNG GIỜ GIÁ & GÓI COMBO ---
    [ObservableProperty]
    private ObservableCollection<TimeSlotPricing> _timeSlotsList = new();

    [ObservableProperty]
    private int _formSlotId;

    [ObservableProperty]
    private string _formSlotName = string.Empty;

    [ObservableProperty]
    private TableType? _formSlotSelectedType;

    [ObservableProperty]
    private int _formSlotStartHour = 8;

    [ObservableProperty]
    private int _formSlotStartMinute = 0;

    [ObservableProperty]
    private int _formSlotEndHour = 12;

    [ObservableProperty]
    private int _formSlotEndMinute = 0;

    [ObservableProperty]
    private decimal _formSlotHourlyRate = 50_000;

    [ObservableProperty]
    private bool _formSlotIsActive = true;

    [ObservableProperty]
    private bool _isEditingSlot;

    // Form Combo
    [ObservableProperty]
    private ObservableCollection<PlayCombo> _combosList = new();

    [ObservableProperty]
    private int _formComboId;

    [ObservableProperty]
    private string _formComboName = string.Empty;

    [ObservableProperty]
    private TableType? _formComboSelectedType;

    [ObservableProperty]
    private decimal _formComboPrice = 150_000;

    [ObservableProperty]
    private int _formComboDurationMinutes = 180;

    [ObservableProperty]
    private string? _formComboIncludedItems = "2 Lon nước ngọt tùy chọn";

    [ObservableProperty]
    private string? _formComboDescription;

    [ObservableProperty]
    private bool _formComboIsActive = true;

    [ObservableProperty]
    private bool _isEditingCombo;

    // Sự kiện khi có thay đổi cấu trúc bàn -> làm mới TableMapView
    public event Action? OnTableStructureChanged;

    public TableManagementViewModel(ITableService tableService, IAuthService authService)
    {
        _tableService = tableService;
        _authService = authService;
        LoadAll();
    }

    [RelayCommand]
    public void SetSubTab(string tab)
    {
        ActiveSubTab = tab;
        Message = null;
    }

    [RelayCommand]
    public void LoadAll()
    {
        // 1. Tải tầng & khu vực
        var floors = _tableService.GetAllFloors();
        FloorsList = new ObservableCollection<Floor>(floors);

        var areas = _tableService.GetAllAreas();
        AreasList = new ObservableCollection<Area>(areas);

        // 2. Tải loại bàn
        var types = _tableService.GetAllTableTypes();
        TableTypesList = new ObservableCollection<TableType>(types);

        // 3. Tải bàn
        var tables = _tableService.GetAllTables();
        TablesList = new ObservableCollection<Table>(tables);

        // 4. Tải khung giờ & gói combo
        var slots = _tableService.GetTimeSlots();
        TimeSlotsList = new ObservableCollection<TimeSlotPricing>(slots);

        var combos = _tableService.GetCombos(activeOnly: false);
        CombosList = new ObservableCollection<PlayCombo>(combos);

        // Chuẩn bị form ban đầu
        PrepareNewTable();
        PrepareNewFloor();
        PrepareNewArea();
        PrepareNewType();
        PrepareNewTimeSlot();
        PrepareNewCombo();
    }

    // ==========================================
    // CÁC THAO TÁC QUẢN LÝ BÀN CHƠI (TABLES CRUD)
    // ==========================================
    [RelayCommand]
    public void PrepareNewTable()
    {
        IsEditingTable = false;
        FormTableId = 0;
        FormTableNumber = string.Empty;
        FormTableSelectedArea = AreasList.FirstOrDefault();
        FormTableSelectedType = TableTypesList.FirstOrDefault();
    }

    [RelayCommand]
    public void EditTable(Table? table)
    {
        if (table == null) return;
        IsEditingTable = true;
        FormTableId = table.TableId;
        FormTableNumber = table.TableNumber;
        FormTableSelectedArea = AreasList.FirstOrDefault(a => a.AreaId == table.AreaId);
        FormTableSelectedType = TableTypesList.FirstOrDefault(t => t.TableTypeId == table.TableTypeId);
        Message = null;
    }

    [RelayCommand]
    public void SaveTable()
    {
        if (string.IsNullOrWhiteSpace(FormTableNumber))
        {
            Message = "⚠️ Vui lòng nhập số hiệu/tên bàn (VD: BÀN 01, VIP 02)!";
            return;
        }

        if (FormTableSelectedArea == null || FormTableSelectedType == null)
        {
            Message = "⚠️ Vui lòng chọn khu vực và loại bàn!";
            return;
        }

        try
        {
            if (IsEditingTable && FormTableId > 0)
            {
                var table = _tableService.GetTableById(FormTableId);
                if (table != null)
                {
                    table.TableNumber = FormTableNumber.Trim().ToUpper();
                    table.AreaId = FormTableSelectedArea.AreaId;
                    table.TableTypeId = FormTableSelectedType.TableTypeId;
                    _tableService.SaveTable(table);
                    Message = $"✅ Đã cập nhật thành công bàn {table.TableNumber}!";
                }
            }
            else
            {
                var newTable = new Table
                {
                    TableNumber = FormTableNumber.Trim().ToUpper(),
                    AreaId = FormTableSelectedArea.AreaId,
                    TableTypeId = FormTableSelectedType.TableTypeId,
                    Status = TableStatus.Available,
                    IsActive = true
                };
                _tableService.SaveTable(newTable);
                Message = $"✅ Đã thêm mới thành công bàn {newTable.TableNumber}!";
            }

            LoadAll();
            OnTableStructureChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Message = $"❌ Lỗi lưu bàn: {ex.Message}";
        }
    }

    [RelayCommand]
    public void DeleteTable(Table? table)
    {
        if (table == null) return;
        if (table.Status == TableStatus.Occupied)
        {
            MessageBox.Show(
                $"Không thể xóa bàn '{table.TableNumber}' khi bàn đang có khách chơi!\nVui lòng hoàn tất thanh toán hoặc chuyển phiên chơi trước khi xóa bàn.",
                "Bàn Đang Có Khách",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Message = $"❌ Không thể xóa bàn {table.TableNumber} khi đang có khách chơi!";
            return;
        }

        var confirm = MessageBox.Show(
            $"Bạn có chắc chắn muốn xóa bàn '{table.TableNumber}' không?\n\n(Lưu ý: Nếu bàn đã từng phát sinh hóa đơn hoặc phiên chơi, hệ thống sẽ bảo lưu lịch sử kế toán và chuyển bàn sang trạng thái ngừng hoạt động)",
            "Xác Nhận Xóa Bàn",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            bool success = _tableService.DeleteTable(table.TableId);
            if (success)
            {
                Message = $"✅ Đã xóa bàn {table.TableNumber} thành công!";
                LoadAll();
                OnTableStructureChanged?.Invoke();
            }
            else
            {
                MessageBox.Show($"Không thể xóa bàn {table.TableNumber} do đang có dữ liệu ràng buộc.", "Lỗi Xóa Bàn", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể xóa bàn {table.TableNumber} do có dữ liệu liên kết tham chiếu: {ex.Message}", "Lỗi Khóa Ngoại", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ==========================================
    // CÁC THAO TÁC QUẢN LÝ TẦNG LẦU (FLOORS CRUD)
    // ==========================================
    [RelayCommand]
    public void PrepareNewFloor()
    {
        IsEditingFloor = false;
        FormFloorId = 0;
        FormFloorName = string.Empty;
        FormFloorOrderIndex = FloorsList.Count + 1;
    }

    [RelayCommand]
    public void EditFloor(Floor? floor)
    {
        if (floor == null) return;
        IsEditingFloor = true;
        FormFloorId = floor.FloorId;
        FormFloorName = floor.FloorName;
        FormFloorOrderIndex = floor.OrderIndex;
        Message = null;
    }

    [RelayCommand]
    public void SaveFloor()
    {
        if (string.IsNullOrWhiteSpace(FormFloorName))
        {
            Message = "⚠️ Vui lòng nhập tên tầng lầu (VD: Tầng 1, Tầng 2, Tầng Lửng)!";
            return;
        }

        try
        {
            var floor = new Floor
            {
                FloorId = IsEditingFloor ? FormFloorId : 0,
                FloorName = FormFloorName.Trim(),
                OrderIndex = FormFloorOrderIndex,
                IsActive = true
            };
            _tableService.SaveFloor(floor);
            Message = $"✅ Đã lưu thông tin tầng '{floor.FloorName}' thành công!";
            LoadAll();
            OnTableStructureChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Message = $"❌ Lỗi lưu tầng: {ex.Message}";
        }
    }

    [RelayCommand]
    public void DeleteFloor(Floor? floor)
    {
        if (floor == null) return;

        int areaCount = AreasList.Count(a => a.FloorId == floor.FloorId);
        int tableCount = TablesList.Count(t => t.Area?.FloorId == floor.FloorId);

        if (areaCount > 0 || tableCount > 0)
        {
            MessageBox.Show(
                $"Không thể xóa tầng '{floor.FloorName}'!\n\nHiện tại đang có {areaCount} khu vực và {tableCount} bàn trực thuộc tầng này.\nVui lòng chuyển hoặc xóa các bàn và khu vực sang tầng khác trước khi xóa tầng này.",
                "Cảnh Báo Tham Chiếu Khóa Ngoại",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Message = $"❌ Không thể xóa tầng '{floor.FloorName}' vì có {areaCount} khu vực và {tableCount} bàn đang trực thuộc!";
            return;
        }

        var confirm = MessageBox.Show(
            $"Bạn có chắc chắn muốn xóa tầng '{floor.FloorName}' khỏi hệ thống không?",
            "Xác Nhận Xóa Tầng Lầu",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            bool success = _tableService.DeleteFloor(floor.FloorId);
            if (success)
            {
                Message = $"✅ Đã xóa tầng '{floor.FloorName}' thành công!";
                LoadAll();
                OnTableStructureChanged?.Invoke();
            }
            else
            {
                MessageBox.Show($"Không thể xóa tầng '{floor.FloorName}' vì đang có dữ liệu liên kết.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể xóa tầng '{floor.FloorName}' do có dữ liệu liên kết tham chiếu: {ex.Message}", "Lỗi Khóa Ngoại", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ==========================================
    // CÁC THAO TÁC QUẢN LÝ KHU VỰC (AREAS CRUD)
    // ==========================================
    [RelayCommand]
    public void PrepareNewArea()
    {
        IsEditingArea = false;
        FormAreaId = 0;
        FormAreaName = string.Empty;
        FormAreaDescription = string.Empty;
        FormAreaSelectedFloor = FloorsList.FirstOrDefault();
    }

    [RelayCommand]
    public void EditArea(Area? area)
    {
        if (area == null) return;
        IsEditingArea = true;
        FormAreaId = area.AreaId;
        FormAreaName = area.AreaName;
        FormAreaDescription = area.Description;
        FormAreaSelectedFloor = FloorsList.FirstOrDefault(f => f.FloorId == area.FloorId);
        Message = null;
    }

    [RelayCommand]
    public void SaveArea()
    {
        if (string.IsNullOrWhiteSpace(FormAreaName))
        {
            Message = "⚠️ Vui lòng nhập tên khu vực (VD: Khu Bàn Lỗ, Khu VIP, Khu 3 Băng)!";
            return;
        }

        try
        {
            var area = new Area
            {
                AreaId = IsEditingArea ? FormAreaId : 0,
                AreaName = FormAreaName.Trim(),
                Description = FormAreaDescription,
                FloorId = FormAreaSelectedFloor?.FloorId,
                IsActive = true
            };
            _tableService.SaveArea(area);
            Message = $"✅ Đã lưu khu vực '{area.AreaName}' thành công!";
            LoadAll();
            OnTableStructureChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Message = $"❌ Lỗi lưu khu vực: {ex.Message}";
        }
    }

    [RelayCommand]
    public void DeleteArea(Area? area)
    {
        if (area == null) return;

        int tableCount = TablesList.Count(t => t.AreaId == area.AreaId);
        if (tableCount > 0)
        {
            MessageBox.Show(
                $"Không thể xóa khu vực '{area.AreaName}'!\n\nHiện tại đang có {tableCount} bàn bida đang thuộc khu vực này.\nVui lòng đổi hoặc chuyển các bàn này sang khu vực khác trước khi xóa khu vực.",
                "Cảnh Báo Tham Chiếu Khóa Ngoại",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Message = $"❌ Không thể xóa khu vực '{area.AreaName}' vì có {tableCount} bàn đang thuộc khu vực này!";
            return;
        }

        var confirm = MessageBox.Show(
            $"Bạn có chắc chắn muốn xóa khu vực '{area.AreaName}' khỏi hệ thống không?",
            "Xác Nhận Xóa Khu Vực",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            bool success = _tableService.DeleteArea(area.AreaId);
            if (success)
            {
                Message = $"✅ Đã xóa khu vực '{area.AreaName}' thành công!";
                LoadAll();
                OnTableStructureChanged?.Invoke();
            }
            else
            {
                MessageBox.Show($"Không thể xóa khu vực '{area.AreaName}' vì đang có dữ liệu liên kết.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể xóa khu vực '{area.AreaName}' do có dữ liệu liên kết tham chiếu: {ex.Message}", "Lỗi Khóa Ngoại", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ==========================================
    // CÁC THAO TÁC LOẠI BÀN & BẢNG GIÁ (TYPES & PRICING)
    // ==========================================
    [RelayCommand]
    public void PrepareNewType()
    {
        IsEditingType = false;
        FormTypeId = 0;
        FormTypeName = string.Empty;
        FormDefaultHourlyRate = 50_000;
        FormMinPlayMinutes = 15;
        FormColorCode = "#10B981";
    }

    [RelayCommand]
    public void EditType(TableType? type)
    {
        if (type == null) return;
        IsEditingType = true;
        FormTypeId = type.TableTypeId;
        FormTypeName = type.TypeName;
        FormDefaultHourlyRate = type.DefaultHourlyRate;
        FormMinPlayMinutes = type.MinPlayMinutes;
        FormColorCode = string.IsNullOrWhiteSpace(type.ColorCode) ? "#10B981" : type.ColorCode;
        Message = null;
    }

    [RelayCommand]
    public void SaveTableType()
    {
        if (string.IsNullOrWhiteSpace(FormTypeName))
        {
            Message = "⚠️ Vui lòng nhập tên loại bàn (VD: Pool 9-Ball, Carom 3-Băng, Snooker)!";
            return;
        }

        if (FormDefaultHourlyRate <= 0)
        {
            Message = "⚠️ Giá giờ chơi phải lớn hơn 0 VNĐ!";
            return;
        }

        try
        {
            var tt = new TableType
            {
                TableTypeId = IsEditingType ? FormTypeId : 0,
                TypeName = FormTypeName.Trim(),
                DefaultHourlyRate = FormDefaultHourlyRate,
                MinPlayMinutes = FormMinPlayMinutes > 0 ? FormMinPlayMinutes : 15,
                ColorCode = FormColorCode,
                IsActive = true
            };
            _tableService.SaveTableType(tt);
            Message = $"✅ Đã lưu loại bàn '{tt.TypeName}' với giá giờ {tt.DefaultHourlyRate:N0} đ/h!";
            LoadAll();
            OnTableStructureChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Message = $"❌ Lỗi lưu loại bàn: {ex.Message}";
        }
    }

    [RelayCommand]
    public void DeleteTableType(TableType? type)
    {
        if (type == null) return;

        int tableCount = TablesList.Count(t => t.TableTypeId == type.TableTypeId);
        if (tableCount > 0)
        {
            MessageBox.Show(
                $"Không thể xóa loại bàn '{type.TypeName}'!\n\nHiện tại đang có {tableCount} bàn bida đang sử dụng loại bàn này.\nVui lòng chuyển loại bàn của các bàn đó sang loại khác trước khi xóa loại bàn này.",
                "Cảnh Báo Tham Chiếu Khóa Ngoại",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Message = $"❌ Không thể xóa loại bàn '{type.TypeName}' vì có {tableCount} bàn đang sử dụng!";
            return;
        }

        int slotCount = TimeSlotsList.Count(s => s.TableTypeId == type.TableTypeId);
        if (slotCount > 0)
        {
            MessageBox.Show(
                $"Không thể xóa loại bàn '{type.TypeName}'!\n\nHiện tại đang có {slotCount} khung giờ giá cấu hình cho loại bàn này.\nVui lòng xóa các khung giờ giá liên quan trước khi xóa loại bàn.",
                "Cảnh Báo Tham Chiếu Khóa Ngoại",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Message = $"❌ Không thể xóa loại bàn '{type.TypeName}' vì có {slotCount} khung giờ giá đang áp dụng!";
            return;
        }

        var confirm = MessageBox.Show(
            $"Bạn có chắc chắn muốn xóa loại bàn '{type.TypeName}' khỏi hệ thống không?",
            "Xác Nhận Xóa Loại Bàn",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            bool success = _tableService.DeleteTableType(type.TableTypeId);
            if (success)
            {
                Message = $"✅ Đã xóa loại bàn '{type.TypeName}' thành công!";
                LoadAll();
                OnTableStructureChanged?.Invoke();
            }
            else
            {
                MessageBox.Show($"Không thể xóa loại bàn '{type.TypeName}' vì đang có dữ liệu liên kết.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể xóa loại bàn '{type.TypeName}' do có dữ liệu liên kết tham chiếu: {ex.Message}", "Lỗi Khóa Ngoại", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // --- CÁC HÀM XỬ LÝ KHUNG GIỜ GIÁ (TIME-SLOT PRICING) ---
    [RelayCommand]
    public void PrepareNewTimeSlot()
    {
        FormSlotId = 0;
        FormSlotName = "Khung Giờ Sáng (08:00 - 12:00)";
        FormSlotSelectedType = TableTypesList.FirstOrDefault();
        FormSlotStartHour = 8;
        FormSlotStartMinute = 0;
        FormSlotEndHour = 12;
        FormSlotEndMinute = 0;
        FormSlotHourlyRate = 50_000;
        FormSlotIsActive = true;
        IsEditingSlot = false;
    }

    [RelayCommand]
    public void EditTimeSlot(TimeSlotPricing? slot)
    {
        if (slot == null) return;
        FormSlotId = slot.TimeSlotId;
        FormSlotName = slot.SlotName;
        FormSlotSelectedType = TableTypesList.FirstOrDefault(t => t.TableTypeId == slot.TableTypeId);
        FormSlotStartHour = slot.StartHour;
        FormSlotStartMinute = slot.StartMinute;
        FormSlotEndHour = slot.EndHour;
        FormSlotEndMinute = slot.EndMinute;
        FormSlotHourlyRate = slot.HourlyRate;
        FormSlotIsActive = slot.IsActive;
        IsEditingSlot = true;
        Message = $"Đang chỉnh sửa khung giờ: {slot.SlotName}";
    }

    [RelayCommand]
    public void SaveTimeSlot()
    {
        Message = null;
        if (string.IsNullOrWhiteSpace(FormSlotName))
        {
            Message = "⚠️ Vui lòng nhập tên khung giờ!";
            return;
        }

        if (FormSlotSelectedType == null)
        {
            Message = "⚠️ Vui lòng chọn loại bàn áp dụng khung giờ!";
            return;
        }

        if (FormSlotHourlyRate <= 0)
        {
            Message = "⚠️ Giá giờ của khung giờ phải lớn hơn 0 VNĐ!";
            return;
        }

        try
        {
            var slot = new TimeSlotPricing
            {
                TimeSlotId = IsEditingSlot ? FormSlotId : 0,
                SlotName = FormSlotName.Trim(),
                TableTypeId = FormSlotSelectedType.TableTypeId,
                StartHour = FormSlotStartHour,
                StartMinute = FormSlotStartMinute,
                EndHour = FormSlotEndHour,
                EndMinute = FormSlotEndMinute,
                HourlyRate = FormSlotHourlyRate,
                IsActive = FormSlotIsActive
            };
            _tableService.SaveTimeSlot(slot);
            Message = $"✅ Đã lưu khung giờ '{slot.SlotName}' ({slot.HourlyRate:N0} đ/h)!";
            LoadAll();
        }
        catch (Exception ex)
        {
            Message = $"❌ Lỗi lưu khung giờ: {ex.Message}";
        }
    }

    [RelayCommand]
    public void DeleteTimeSlot(TimeSlotPricing? slot)
    {
        if (slot == null) return;
        bool success = _tableService.DeleteTimeSlot(slot.TimeSlotId);
        if (success)
        {
            Message = $"✅ Đã xóa khung giờ '{slot.SlotName}'!";
            LoadAll();
        }
        else
        {
            Message = $"❌ Lỗi khi xóa khung giờ!";
        }
    }

    // --- CÁC HÀM XỬ LÝ GÓI COMBO BIDA ---
    [RelayCommand]
    public void PrepareNewCombo()
    {
        FormComboId = 0;
        FormComboName = "Combo 3 Tiếng + 2 Nước";
        FormComboSelectedType = null;
        FormComboPrice = 150_000;
        FormComboDurationMinutes = 180;
        FormComboIncludedItems = "2 Lon nước ngọt tùy chọn";
        FormComboDescription = "Gói 3 tiếng chơi bida kèm đồ uống";
        FormComboIsActive = true;
        IsEditingCombo = false;
    }

    [RelayCommand]
    public void EditCombo(PlayCombo? combo)
    {
        if (combo == null) return;
        FormComboId = combo.ComboId;
        FormComboName = combo.ComboName;
        FormComboSelectedType = TableTypesList.FirstOrDefault(t => t.TableTypeId == combo.TableTypeId);
        FormComboPrice = combo.Price;
        FormComboDurationMinutes = combo.DurationMinutes;
        FormComboIncludedItems = combo.IncludedItems;
        FormComboDescription = combo.Description;
        FormComboIsActive = combo.IsActive;
        IsEditingCombo = true;
        Message = $"Đang chỉnh sửa gói combo: {combo.ComboName}";
    }

    [RelayCommand]
    public void SaveCombo()
    {
        Message = null;
        if (string.IsNullOrWhiteSpace(FormComboName))
        {
            Message = "⚠️ Vui lòng nhập tên gói combo!";
            return;
        }

        if (FormComboPrice <= 0)
        {
            Message = "⚠️ Giá gói combo phải lớn hơn 0 VNĐ!";
            return;
        }

        if (FormComboDurationMinutes <= 0)
        {
            Message = "⚠️ Thời lượng combo phải lớn hơn 0 phút!";
            return;
        }

        try
        {
            var combo = new PlayCombo
            {
                ComboId = IsEditingCombo ? FormComboId : 0,
                ComboName = FormComboName.Trim(),
                TableTypeId = FormComboSelectedType?.TableTypeId,
                Price = FormComboPrice,
                DurationMinutes = FormComboDurationMinutes,
                IncludedItems = FormComboIncludedItems?.Trim(),
                Description = FormComboDescription?.Trim(),
                IsActive = FormComboIsActive
            };
            _tableService.SaveCombo(combo);
            Message = $"✅ Đã lưu gói combo '{combo.ComboName}' ({combo.Price:N0} đ)!";
            LoadAll();
        }
        catch (Exception ex)
        {
            Message = $"❌ Lỗi lưu gói combo: {ex.Message}";
        }
    }

    [RelayCommand]
    public void DeleteCombo(PlayCombo? combo)
    {
        if (combo == null) return;
        var confirm = MessageBox.Show(
            $"Bạn có chắc chắn muốn xóa gói combo '{combo.ComboName}' ({combo.Price:N0} đ) khỏi hệ thống không?",
            "Xác Nhận Xóa Combo",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            bool success = _tableService.DeleteCombo(combo.ComboId);
            if (success)
            {
                Message = $"✅ Đã xóa gói combo '{combo.ComboName}' thành công!";
                LoadAll();
            }
            else
            {
                MessageBox.Show($"Không thể xóa gói combo '{combo.ComboName}' vì gói đang được sử dụng hoặc có dữ liệu liên kết.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể xóa gói combo '{combo.ComboName}' do có dữ liệu hóa đơn/phiên chơi đang tham chiếu: {ex.Message}", "Lỗi Khóa Ngoại", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
