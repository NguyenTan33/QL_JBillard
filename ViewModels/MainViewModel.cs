using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IAuthService _authService;
    private readonly IBackupAndSecurityService _securityService;
    private readonly IShiftService _shiftService;
    private readonly DispatcherTimer _clockTimer;

    [ObservableProperty]
    private object _currentViewModel;

    [ObservableProperty]
    private string _activeTab = "TABLES"; // TABLES, INVENTORY, CUSTOMERS, SHIFT, AUDIT, SETTINGS

    [ObservableProperty]
    private User? _currentUser;

    public bool IsAdmin => CurrentUser?.Role == UserRole.Admin;
    public bool IsCashier => CurrentUser?.Role == UserRole.Cashier;

    [ObservableProperty]
    private string _integrityBadgeText = "CSDL NGUYÊN VẸN 100%";

    [ObservableProperty]
    private bool _isDatabaseTampered = false;

    [ObservableProperty]
    private string _currentDateTimeString = string.Empty;

    [ObservableProperty]
    private string _shiftStatusText = "Chưa mở ca";

    // ViewModels con
    public TableMapViewModel TableMapVM { get; }
    public OrderQueueViewModel OrderQueueVM { get; }
    public ReportsViewModel ReportsVM { get; }
    public InventoryViewModel InventoryVM { get; }
    public CustomerViewModel CustomerVM { get; }
    public ShiftViewModel ShiftVM { get; }
    public AuditLogViewModel AuditLogVM { get; }
    public SettingsViewModel SettingsVM { get; }
    public UserManagementViewModel UserManagementVM { get; }
    public TableManagementViewModel TableManagementVM { get; }

    // Danh sách thông báo Notion dạng Toast ở góc trên bên phải màn hình (Tự ẩn sau 7s)
    public System.Collections.ObjectModel.ObservableCollection<ToastNotificationViewModel> ToastNotifications { get; } = new();

    public event Action? RequestLogout;

    public MainViewModel(
        IAuthService authService,
        IBackupAndSecurityService securityService,
        IShiftService shiftService,
        TableMapViewModel tableMapVM,
        OrderQueueViewModel orderQueueVM,
        ReportsViewModel reportsVM,
        InventoryViewModel inventoryVM,
        CustomerViewModel customerVM,
        ShiftViewModel shiftVM,
        AuditLogViewModel auditLogVM,
        SettingsViewModel settingsVM,
        UserManagementViewModel userManagementVM,
        TableManagementViewModel tableManagementVM)
    {
        _authService = authService;
        _securityService = securityService;
        _shiftService = shiftService;

        TableMapVM = tableMapVM;
        OrderQueueVM = orderQueueVM;
        ReportsVM = reportsVM;
        InventoryVM = inventoryVM;
        CustomerVM = customerVM;
        ShiftVM = shiftVM;
        AuditLogVM = auditLogVM;
        SettingsVM = settingsVM;
        UserManagementVM = userManagementVM;
        TableManagementVM = tableManagementVM;

        // Đăng ký sự kiện cập nhật cấu trúc bàn sang sơ đồ bàn chơi
        TableManagementVM.OnTableStructureChanged += () => TableMapVM.LoadData();

        // Đăng ký sự kiện cảnh báo món trễ hơn 10 phút và cập nhật bàn
        OrderQueueVM.OnOverdueOrderAlert += HandleOverdueOrder;
        OrderQueueVM.OnOrderStateChanged += () => TableMapVM.LoadData();

        CurrentUser = _authService.CurrentUser;

        // Nếu là Thu ngân và chưa có ca mở -> Bắt buộc điều hướng ngay vào trang mở ca
        if (IsCashier && _shiftService.CurrentActiveShift == null)
        {
            _currentViewModel = shiftVM;
            _activeTab = "SHIFT";
            ShiftVM.RefreshShiftStatus();
        }
        else
        {
            _currentViewModel = tableMapVM;
            _activeTab = "TABLES";
        }

        // Đồng hồ thời gian thực
        UpdateClock();
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (s, e) => UpdateClock();
        _clockTimer.Start();

        CheckSecurityHealth();
        UpdateShiftStatus();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        var dayOfWeekVi = now.DayOfWeek switch
        {
            DayOfWeek.Monday => "Thứ Hai",
            DayOfWeek.Tuesday => "Thứ Ba",
            DayOfWeek.Wednesday => "Thứ Tư",
            DayOfWeek.Thursday => "Thứ Năm",
            DayOfWeek.Friday => "Thứ Sáu",
            DayOfWeek.Saturday => "Thứ Bảy",
            DayOfWeek.Sunday => "Chủ Nhật",
            _ => ""
        };
        CurrentDateTimeString = $"{dayOfWeekVi}, {now:dd/MM/yyyy} • {now:HH:mm:ss}";
    }

    public void UpdateShiftStatus()
    {
        var activeShift = _shiftService.CurrentActiveShift;
        if (activeShift != null)
        {
            ShiftStatusText = $"Ca trực mở lúc {activeShift.StartTime:HH:mm}";
        }
        else
        {
            ShiftStatusText = "Chưa mở ca trực";
        }
    }

    public void CheckSecurityHealth()
    {
        var (tampered, _) = _securityService.CheckTamperProofStatus();
        IsDatabaseTampered = tampered;
        IntegrityBadgeText = tampered ? "CẢNH BÁO: CSDL BỊ SỬA LÉN!" : "CSDL AN TOÀN (HMAC-SHA256)";
    }

    private void HandleOverdueOrder(OrderItemCardViewModel item)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var toast = new ToastNotificationViewModel(
                "⚠️ CẢNH BÁO TRỄ MÓN (>10 PHÚT)",
                $"{item.TableNumber} - {item.Quantity}x {item.ProductName} đã gọi hơn 10 phút chưa phục vụ!",
                item.TableNumber,
                ToastType.Danger // ĐỎ CẢNH BÁO RIÊNG CHO TRỄ MÓN
            );
            toast.OnDismissed += t => ToastNotifications.Remove(t);
            ToastNotifications.Add(toast);
        });
    }

    public void ShowGlobalToast(string title, string message, ToastType type = ToastType.Success)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var toast = new ToastNotificationViewModel(title, message, "", type);
            toast.OnDismissed += t => ToastNotifications.Remove(t);
            ToastNotifications.Add(toast);
        });
    }

    [RelayCommand]
    private void Navigate(string tab)
    {
        // Thu ngân có quyền truy cập: Sơ đồ bàn, Danh sách Order, Ca trực & Tiền két, Thực đơn kho hàng, Khách hàng & Hội viên
        if (IsCashier && tab != "TABLES" && tab != "SHIFT" && tab != "ORDER_QUEUE" && tab != "INVENTORY" && tab != "CUSTOMERS")
        {
            tab = "TABLES";
        }
        ActiveTab = tab;
        CurrentViewModel = tab switch
        {
            "TABLES" => TableMapVM,
            "ORDER_QUEUE" => OrderQueueVM,
            "REPORTS" => ReportsVM,
            "INVENTORY" => InventoryVM,
            "CUSTOMERS" => CustomerVM,
            "SHIFT" => ShiftVM,
            "AUDIT" => AuditLogVM,
            "SETTINGS" => SettingsVM,
            "USERS" => UserManagementVM,
            "TABLE_MGMT" => TableManagementVM,
            _ => TableMapVM
        };

        if (tab == "TABLES") TableMapVM.LoadData();
        if (tab == "ORDER_QUEUE") OrderQueueVM.LoadOrders();
        if (tab == "REPORTS") ReportsVM.LoadCurrentTab();
        if (tab == "INVENTORY") InventoryVM.LoadData();
        if (tab == "CUSTOMERS") CustomerVM.LoadData();
        if (tab == "USERS") UserManagementVM.LoadUsers();
        if (tab == "TABLE_MGMT") TableManagementVM.LoadAll();
        if (tab == "SHIFT")
        {
            ShiftVM.RefreshShiftStatus();
            UpdateShiftStatus();
        }
        if (tab == "AUDIT")
        {
            AuditLogVM.LoadLogs();
            AuditLogVM.VerifyDatabase();
            CheckSecurityHealth();
        }
    }

    public bool CheckCanExitOrLogout(out string warningTitle, out string warningMessage)
    {
        warningTitle = string.Empty;
        warningMessage = string.Empty;

        var activeShift = _shiftService.CurrentActiveShift;
        if (activeShift == null)
        {
            return true;
        }

        var currentUser = CurrentUser ?? _authService.CurrentUser;
        if (currentUser == null)
        {
            return true;
        }

        // 1. Role Admin mở ca -> Phải đóng ca mới cho đăng xuất hoặc thoát app
        if (currentUser.Role == UserRole.Admin && activeShift.UserId == currentUser.UserId)
        {
            warningTitle = "Quản Trị Viên Chưa Đóng Ca";
            warningMessage = "⚠️ BẠN ĐANG MỞ CA TRỰC!\n\n" +
                             "Quản trị viên đang có một ca làm việc đang mở trong hệ thống.\n" +
                             "Hệ thống yêu cầu phải hoàn tất đóng ca trực trước khi thực hiện hành động này.\n\n" +
                             "👉 Vui lòng vào mục 'Ca Trực & Tiền Két' để kiểm đếm két và đóng ca!";
            return false;
        }

        // 2. Role Thu ngân có ca đang mở -> Phải hoàn tất kết ca mới cho out app hoặc đăng xuất
        if (currentUser.Role == UserRole.Cashier)
        {
            warningTitle = "Chưa Hoàn Tất Kết Ca";
            warningMessage = "⚠️ CHƯA HOÀN TẤT KẾT CA!\n\n" +
                             "Thu ngân phải hoàn tất kết ca và kiểm đếm bàn giao tiền két trước khi đăng xuất hoặc thoát ứng dụng.\n\n" +
                             "👉 Vui lòng vào mục 'Ca Trực & Tiền Két' để hoàn tất kết ca!";
            return false;
        }

        return true;
    }

    [RelayCommand]
    private void Logout()
    {
        if (!CheckCanExitOrLogout(out string title, out string message))
        {
            System.Windows.MessageBox.Show(
                message,
                title,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            Navigate("SHIFT");
            return;
        }

        _authService.Logout();
        RequestLogout?.Invoke();
    }

    public void Dispose()
    {
        _clockTimer.Stop();
    }
}
