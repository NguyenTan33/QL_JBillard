using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QL_JBillard.Data;
using QL_JBillard.Services.Implementations;
using QL_JBillard.Services.Interfaces;
using QL_JBillard.ViewModels;
using QL_JBillard.Views.Windows;

namespace QL_JBillard;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--verify"))
        {
            Tests.SystemVerification.RunVerification();
            Shutdown();
            return;
        }

        if (e.Args.Contains("--clean-db"))
        {
            var cleanServices = new ServiceCollection();
            ConfigureServices(cleanServices);
            using var sp = cleanServices.BuildServiceProvider();
            var contextFactory = sp.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var context = contextFactory.CreateDbContext();
            DatabaseInitializer.Initialize(context);
            DatabaseCleaner.ResetAndCleanDatabase(context);
            Console.WriteLine("✅ ĐÃ XÓA SẠCH DỮ LIỆU CƠ SỞ DỮ LIỆU! TIỀN KÉT BAN ĐẦU: 1.000.000 Đ, CHỈ GIỮ LẠI TÀI KHOẢN ADMIN.");
            Shutdown();
            return;
        }

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // 1. Tự động khởi tạo cấu trúc CSDL SQLite & Nạp dữ liệu ban đầu chuẩn 3NF
        using (var scope = _serviceProvider.CreateScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var context = contextFactory.CreateDbContext();
            DatabaseInitializer.Initialize(context);
        }

        // 2. Kích hoạt dịch vụ tự động sao lưu dữ liệu SQLite định kỳ (Mặc định 30 phút hoặc theo cài đặt quán)
        int autoBackupMinutes = 30;
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var context = contextFactory.CreateDbContext();
            var setting = context.SystemSettings.Find("AutoBackupIntervalMinutes");
            if (setting != null && int.TryParse(setting.SettingValue, out int minutes) && minutes >= 5)
            {
                autoBackupMinutes = minutes;
            }
        }
        catch { }

        var backupService = _serviceProvider.GetRequiredService<IBackupAndSecurityService>();
        backupService.StartPeriodicAutoBackup(autoBackupMinutes);

        // 3. Mở cửa sổ Đăng Nhập Offline
        ShowLoginWindow();
    }

    private void ShowLoginWindow()
    {
        if (_serviceProvider == null) return;

        var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
        var loginVm = (LoginViewModel)loginWindow.DataContext;

        loginVm.OnLoginSuccess += () =>
        {
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            Current.MainWindow = mainWindow;
            mainWindow.Show();
            loginWindow.Close();
        };

        Current.MainWindow = loginWindow;
        loginWindow.Show();
    }

    public static void ConfigureServices(IServiceCollection services)
    {
        // Data Access Layer
        services.AddDbContextFactory<AppDbContext>(options =>
        {
            var appFolder = AppDomain.CurrentDomain.BaseDirectory;
            var dataFolder = System.IO.Path.Combine(appFolder, "AppData");
            if (!System.IO.Directory.Exists(dataFolder))
            {
                System.IO.Directory.CreateDirectory(dataFolder);
            }
            string dbPath = System.IO.Path.Combine(dataFolder, "qlin_billiard.db");
            options.UseSqlite($"Data Source={dbPath}");
        });

        // Core Business Services (SOLID & OOP)
        services.AddSingleton<IAuditService, AuditService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<ITableService, TableService>();
        services.AddSingleton<IBillingService, BillingService>();
        services.AddSingleton<IInventoryService, InventoryService>();
        services.AddSingleton<ICustomerService, CustomerService>();
        services.AddSingleton<IVietQrService, VietQrService>();
        services.AddSingleton<IPrintService, PrintService>();
        services.AddSingleton<IBackupAndSecurityService, BackupAndSecurityService>();
        services.AddSingleton<IShiftService, ShiftService>();
        services.AddSingleton<IReportService, ReportService>();

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<TableMapViewModel>();
        services.AddTransient<OrderQueueViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<CustomerViewModel>();
        services.AddTransient<ShiftViewModel>();
        services.AddTransient<AuditLogViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<UserManagementViewModel>();
        services.AddTransient<TableManagementViewModel>();
        services.AddTransient<CategoryManagementViewModel>();
        services.AddTransient<MainViewModel>();

        // Windows
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
    }
}
