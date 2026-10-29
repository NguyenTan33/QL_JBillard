using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QL_JBillard.Data;
using QL_JBillard.Services.Interfaces;
using QL_JBillard.ViewModels;
using QL_JBillard.Views.Dialogs;

namespace QL_JBillard;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;

    public MainWindow(MainViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
        DataContext = _viewModel;

        RegisterDialogEvents();
    }

    private void RegisterDialogEvents()
    {
        // 0. Mở bàn (Chọn hình thức chơi theo giờ hoặc Gói Combo)
        _viewModel.TableMapVM.RequestOpenTableDialog += (tableItem) =>
        {
            var tableService = _serviceProvider.GetRequiredService<ITableService>();
            var authService = _serviceProvider.GetRequiredService<IAuthService>();
            var custService = _serviceProvider.GetRequiredService<ICustomerService>();

            var openVm = new OpenTableDialogViewModel(tableItem, tableService, authService, custService);
            var dialog = new Views.Dialogs.OpenTableDialog(openVm) { Owner = this };
            bool? res = dialog.ShowDialog();
            if (res == true)
            {
                _viewModel.TableMapVM.RefreshTable(tableItem.TableId);
                _viewModel.ShowGlobalToast("MỞ BÀN THÀNH CÔNG", $"Đã mở bàn {tableItem.TableNumber} thành công! Bắt đầu tính giờ.");
            }
        };

        // 0.1. Thông báo thao tác bàn (Đặt bàn, hủy đặt, bảo trì)
        _viewModel.TableMapVM.ShowNotification += (msg) =>
        {
            _viewModel.ShowGlobalToast("THÔNG BÁO BÀN CHƠI", msg, ToastType.Success);
        };

        // 1. Gọi món (Order F&B)
        _viewModel.TableMapVM.RequestOrderDialog += (tableItem) =>
        {
            var invService = _serviceProvider.GetRequiredService<IInventoryService>();
            var authService = _serviceProvider.GetRequiredService<IAuthService>();
            var activeTables = _viewModel.TableMapVM.AllTables.Where(t => t.IsOccupied).ToList();
            var orderVm = new OrderDialogViewModel(invService, authService, tableItem, activeTables);

            var dialog = new OrderDialog(orderVm) { Owner = this };
            dialog.ShowDialog();

            _viewModel.TableMapVM.LoadData();
        };

        // 1.1. Nộp tiền ngân hàng (Bank Deposit)
        _viewModel.ShiftVM.RequestOpenBankDepositDialog += () =>
        {
            var shiftService = _serviceProvider.GetRequiredService<IShiftService>();
            var authService = _serviceProvider.GetRequiredService<IAuthService>();
            var depositVm = new BankDepositDialogViewModel(shiftService, authService, _viewModel.ShiftVM.CurrentDrawerCash);
            depositVm.OnDepositSuccess += (slip) =>
            {
                _viewModel.ShiftVM.RefreshShiftStatus();
            };

            var dialog = new Views.Dialogs.BankDepositDialog(depositVm) { Owner = this };
            dialog.ShowDialog();
            _viewModel.ShiftVM.RefreshShiftStatus();
        };

        // 2. Thanh toán hóa đơn (Checkout)
        _viewModel.TableMapVM.RequestCheckoutDialog += (tableItem) =>
        {
            var shiftService = _serviceProvider.GetRequiredService<IShiftService>();
            if (shiftService.CurrentActiveShift == null)
            {
                System.Windows.MessageBox.Show(
                    "⚠️ BẠN CHƯA MỞ CA TRỰC!\n\nKhông thể thanh toán bàn khi chưa có ca làm việc nào mở.\nVui lòng vào mục 'Ca Trực & Tiền Két' để kiểm đếm tiền két và xác nhận mở ca trước khi thanh toán để ghi nhận doanh thu chính xác.",
                    "Chưa Mở Ca Làm Việc",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning
                );
                _viewModel.NavigateCommand.Execute("SHIFT");
                return;
            }

            var billService = _serviceProvider.GetRequiredService<IBillingService>();
            var custService = _serviceProvider.GetRequiredService<ICustomerService>();
            var qrService = _serviceProvider.GetRequiredService<IVietQrService>();
            var printService = _serviceProvider.GetRequiredService<IPrintService>();
            var backupService = _serviceProvider.GetRequiredService<IBackupAndSecurityService>();
            var authService = _serviceProvider.GetRequiredService<IAuthService>();
            var contextFactory = _serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

            var checkoutVm = new CheckoutDialogViewModel(
                billService, custService, qrService, printService, backupService, authService, contextFactory, tableItem
            );

            var dialog = new CheckoutDialog(checkoutVm) { Owner = this };
            dialog.ShowDialog();

            _viewModel.TableMapVM.RefreshTable(tableItem.TableId);
            _viewModel.CheckSecurityHealth();
        };

        // 3. Chuyển bàn (Switch Table)
        _viewModel.TableMapVM.RequestSwitchTableDialog += (tableItem) =>
        {
            var tableService = _serviceProvider.GetRequiredService<ITableService>();
            var authService = _serviceProvider.GetRequiredService<IAuthService>();
            var switchVm = new SwitchTableViewModel(tableService, authService, tableItem);

            var dialog = new SwitchTableDialog(switchVm) { Owner = this };
            bool? res = dialog.ShowDialog();
            if (res == true)
            {
                _viewModel.TableMapVM.LoadData();
                _viewModel.ShowGlobalToast("CHUYỂN BÀN THÀNH CÔNG", $"Đã chuyển phiên chơi của {tableItem.TableNumber} sang bàn mới thành công!");
            }
        };

        // 4. Nhắc nhở mở ca khi thao tác bàn
        _viewModel.TableMapVM.RequestNavigateToShift += () =>
        {
            var res = System.Windows.MessageBox.Show(
                "⚠️ BẠN CHƯA MỞ CA TRỰC!\n\nKhông thể mở bàn phục vụ khách khi chưa có ca làm việc nào mở.\nBạn có muốn chuyển sang trang 'Ca Trực & Tiền Két' để kiểm đếm tiền và mở ca ngay không?",
                "Chưa Mở Ca Trực",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning
            );
            if (res == System.Windows.MessageBoxResult.Yes)
            {
                _viewModel.NavigateCommand.Execute("SHIFT");
            }
        };

        // 5. Xử lý khi đóng ca mà còn bàn đang mở
        _viewModel.ShiftVM.RequestOccupiedTablesChoice += (occupiedTables, onChoiceSelected) =>
        {
            var handoffDialog = new Views.Dialogs.ShiftHandoffDialog(occupiedTables) { Owner = this };
            handoffDialog.ShowDialog();
            onChoiceSelected(handoffDialog.Choice);
        };

        _viewModel.ShiftVM.RequestNavigateToTables += () =>
        {
            _viewModel.NavigateCommand.Execute("TABLES");
        };

        // 6. Đăng xuất
        _viewModel.RequestLogout += () =>
        {
            var loginWindow = _serviceProvider.GetRequiredService<Views.Windows.LoginWindow>();
            var loginVm = (LoginViewModel)loginWindow.DataContext;
            loginVm.OnLoginSuccess += () =>
            {
                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                Application.Current.MainWindow = mainWindow;
                mainWindow.Show();
                loginWindow.Close();
            };
            Application.Current.MainWindow = loginWindow;
            loginWindow.Show();
            Close();
        };
    }
}
