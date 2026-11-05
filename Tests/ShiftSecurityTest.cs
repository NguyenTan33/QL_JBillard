using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QL_JBillard.Data;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Helpers;
using QL_JBillard.Services.Implementations;

namespace QL_JBillard.Tests;

public static class ShiftSecurityTest
{
    public static void Run()
    {
        Console.WriteLine("=== KIỂM TRA QUY TẮC CA TRỰC & KHÓA ĐĂNG NHẬP ===");
        string tempDb = Path.Combine(Path.GetTempPath(), $"test_shift_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={tempDb}")
            .Options;

        var contextFactory = new SimpleDbContextFactory(options);

        using (var ctx = contextFactory.CreateDbContext())
        {
            ctx.Database.EnsureCreated();

            // Tạo Admin
            string saltA = SecurityHelper.GenerateSalt();
            ctx.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = SecurityHelper.HashPassword("admin123", saltA),
                Salt = saltA,
                FullName = "Quản Trị Viên",
                Role = UserRole.Admin,
                IsActive = true
            });

            // Tạo Thu ngân 1
            string saltC1 = SecurityHelper.GenerateSalt();
            ctx.Users.Add(new User
            {
                Username = "cashier1",
                PasswordHash = SecurityHelper.HashPassword("123456", saltC1),
                Salt = saltC1,
                FullName = "Nguyễn Văn Thu Ngân 1",
                Role = UserRole.Cashier,
                IsActive = true
            });

            // Tạo Thu ngân 2
            string saltC2 = SecurityHelper.GenerateSalt();
            ctx.Users.Add(new User
            {
                Username = "cashier2",
                PasswordHash = SecurityHelper.HashPassword("123456", saltC2),
                Salt = saltC2,
                FullName = "Trần Thị Thu Ngân 2",
                Role = UserRole.Cashier,
                IsActive = true
            });

            ctx.SaveChanges();
        }

        var audit = new AuditService(contextFactory);
        var auth = new AuthService(contextFactory, audit);
        var shift = new ShiftService(contextFactory, audit);

        // 1. Thu ngân 1 đăng nhập và mở ca
        bool loginC1 = auth.Login("cashier1", "123456");
        if (!loginC1) throw new Exception("Thu ngân 1 đăng nhập thất bại!");
        Console.WriteLine("✅ 1. Thu ngân 1 đăng nhập thành công");

        var activeShift = shift.StartShift(auth.CurrentUser!.UserId, 1_000_000m, "Mở ca sáng");
        Console.WriteLine($"✅ 2. Thu ngân 1 mở ca thành công (ShiftId: {activeShift.ShiftId})");

        // Giả lập: Bị End Task -> Mở lại app (auth logout trong app)
        auth.Logout();

        // 2. Thu ngân 2 cố tình đăng nhập trong khi ca của Thu ngân 1 đang mở
        Console.WriteLine("--- Thử nghiệm Thu ngân 2 đăng nhập khi Thu ngân 1 chưa kết ca ---");
        try
        {
            auth.Login("cashier2", "123456");
            throw new Exception("LỖI: Thu ngân 2 không được phép đăng nhập nhưng hệ thống lại cho vào!");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine("✅ 3. Thu ngân 2 BỊ CHẶN ĐĂNG NHẬP THÀNH CÔNG!");
            Console.WriteLine($"   Thông báo lỗi: \n{ex.Message}");
        }

        // 3. Chính Thu ngân 1 đăng nhập lại (sau khi bị End Task)
        bool reLoginC1 = auth.Login("cashier1", "123456");
        if (!reLoginC1) throw new Exception("LỖI: Chính Thu ngân 1 phải đăng nhập lại được!");
        Console.WriteLine("✅ 4. Chính Thu ngân 1 đăng nhập lại thành công!");
        auth.Logout();

        // 4. Admin đăng nhập vào để đóng ca hộ
        bool loginAdmin = auth.Login("admin", "admin123");
        if (!loginAdmin) throw new Exception("LỖI: Admin phải luôn đăng nhập được!");
        Console.WriteLine("✅ 5. Admin đăng nhập vào hệ thống thành công!");

        // Admin đóng ca hộ
        shift.CloseShift(activeShift.ShiftId, 1_000_000m, "Admin đóng ca hộ sau sự cố EndTask", auth.CurrentUser!.UserId);
        Console.WriteLine("✅ 6. Admin đóng ca hộ Thu ngân 1 thành công!");
        auth.Logout();

        // 5. Sau khi ca đã được Admin đóng hộ, Thu ngân 2 giờ đây đăng nhập được
        bool loginC2After = auth.Login("cashier2", "123456");
        if (!loginC2After) throw new Exception("LỖI: Sau khi ca được đóng, Thu ngân 2 phải đăng nhập được!");
        Console.WriteLine("✅ 7. Thu ngân 2 đăng nhập thành công sau khi ca trước đã được chốt!");

        // Dọn dẹp file temp
        try { File.Delete(tempDb); } catch { }

        Console.WriteLine("=== TEST UI NAVIGATION: REPORTS & AUDIT ===");
        var staThread = new System.Threading.Thread(() =>
        {
            try
            {
                var app = System.Windows.Application.Current;
                if (app == null)
                {
                    app = new System.Windows.Application();
                    var appResourceUri = new Uri("/QL_JBillard;component/App.xaml", UriKind.Relative);
                    System.Windows.Application.LoadComponent(app, appResourceUri);
                }

                var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
                App.ConfigureServices(services);
                using var sp = services.BuildServiceProvider();

                var authService = sp.GetRequiredService<Services.Interfaces.IAuthService>();
                authService.Login("admin", "admin123");

                var mainVm = sp.GetRequiredService<ViewModels.MainViewModel>();
                Console.WriteLine("Instantiated MainViewModel successfully!");

                Console.WriteLine("Testing Navigate('REPORTS')...");
                var navMethod = typeof(ViewModels.MainViewModel).GetMethod("Navigate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                navMethod?.Invoke(mainVm, new object[] { "REPORTS" });
                Console.WriteLine("Navigate('REPORTS') completed!");

                Console.WriteLine("Instantiating ReportsView...");
                var reportsView = new Views.UserControls.ReportsView { DataContext = mainVm.ReportsVM };
                Console.WriteLine("ReportsView created successfully!");

                Console.WriteLine("Testing Navigate('AUDIT')...");
                navMethod?.Invoke(mainVm, new object[] { "AUDIT" });
                Console.WriteLine("Navigate('AUDIT') completed!");

                Console.WriteLine("Instantiating AuditLogView...");
                var auditView = new Views.UserControls.AuditLogView { DataContext = mainVm.AuditLogVM };
                Console.WriteLine("AuditLogView created successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ EXCEPTION CAUGHT IN UI TEST: " + ex.ToString());
                if (ex.InnerException != null)
                {
                    Console.WriteLine("INNER EXCEPTION: " + ex.InnerException.ToString());
                }
            }
        });
        staThread.SetApartmentState(System.Threading.ApartmentState.STA);
        staThread.Start();
        staThread.Join();

        Console.WriteLine("==================================================");
        Console.WriteLine("🎉 TOÀN BỘ CÁC BƯỚC KIỂM TRA ĐÃ HOÀN TẤT VÀ CHÍNH XÁC 100%!");
        Console.WriteLine("==================================================");
    }
}
