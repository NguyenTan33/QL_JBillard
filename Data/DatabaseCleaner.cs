using Microsoft.EntityFrameworkCore;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Helpers;

namespace QL_JBillard.Data;

public static class DatabaseCleaner
{
    public static void ResetAndCleanDatabase(AppDbContext context)
    {
        // 1. Tắt tạm thời Foreign Key check để xóa dữ liệu giao dịch sạch sẽ
        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");

        // 2. Xóa sạch toàn bộ dữ liệu giao dịch, phiên chơi, hóa đơn, ca trực, nhật ký kiểm toán
        context.Database.ExecuteSqlRaw("DELETE FROM InvoicePayments;");
        context.Database.ExecuteSqlRaw("DELETE FROM Invoices;");
        context.Database.ExecuteSqlRaw("DELETE FROM OrderItems;");
        context.Database.ExecuteSqlRaw("DELETE FROM TableSessions;");
        context.Database.ExecuteSqlRaw("DELETE FROM BankDepositSlips;");
        context.Database.ExecuteSqlRaw("DELETE FROM Shifts;");
        context.Database.ExecuteSqlRaw("DELETE FROM StockTransactionDetails;");
        context.Database.ExecuteSqlRaw("DELETE FROM StockTransactions;");
        context.Database.ExecuteSqlRaw("DELETE FROM AuditLogs;");
        context.Database.ExecuteSqlRaw("DELETE FROM DatabaseBackupLogs;");
        context.Database.ExecuteSqlRaw("DELETE FROM Customers;");

        // 3. XÓA TOÀN BỘ NGƯỜI DÙNG - CHỈ CHỪA LẠI ĐÚNG TÀI KHOẢN ADMIN
        context.Database.ExecuteSqlRaw("DELETE FROM Users WHERE LOWER(Username) != 'admin';");

        var admin = context.Users.FirstOrDefault(u => u.Username.ToLower() == "admin");
        if (admin == null)
        {
            var adminSalt = SecurityHelper.GenerateSalt();
            context.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = SecurityHelper.HashPassword("admin123", adminSalt),
                Salt = adminSalt,
                FullName = "Quản Trị Viên",
                PhoneNumber = "0909999888",
                Role = UserRole.Admin,
                IsActive = true
            });
            context.SaveChanges();
        }
        else
        {
            var adminSalt = SecurityHelper.GenerateSalt();
            admin.PasswordHash = SecurityHelper.HashPassword("admin123", adminSalt);
            admin.Salt = adminSalt;
            admin.FullName = "Quản Trị Viên";
            admin.Role = UserRole.Admin;
            admin.IsActive = true;
            context.SaveChanges();
        }

        // 4. Đưa tất cả bàn bida về trạng thái Trống (Available = 0)
        context.Database.ExecuteSqlRaw("UPDATE Tables SET Status = 0;");

        // 5. Cài đặt số tiền két ban đầu là 1.000.000 đ trong SystemSettings
        var initCashSetting = context.SystemSettings.Find("InitialDrawerCash");
        if (initCashSetting != null)
        {
            initCashSetting.SettingValue = "1000000";
            initCashSetting.UpdatedAt = DateTime.Now;
        }
        else
        {
            context.SystemSettings.Add(new SystemSetting
            {
                SettingKey = "InitialDrawerCash",
                SettingValue = "1000000",
                Description = "Tiền két mặc định ban đầu khi mở ca (1.000.000đ)",
                UpdatedAt = DateTime.Now
            });
        }

        // 6. Reset tồn kho F&B về số lượng tiêu chuẩn sẵn sàng bán (50 đơn vị mỗi món)
        context.Database.ExecuteSqlRaw("UPDATE InventoryItems SET QuantityOnHand = 50;");

        // 7. Reset sequence SQLite để các mã tự tăng bắt đầu lại từ 1
        try
        {
            context.Database.ExecuteSqlRaw(@"
                DELETE FROM sqlite_sequence WHERE name IN (
                    'Invoices', 'InvoicePayments', 'OrderItems', 'TableSessions', 
                    'BankDepositSlips', 'Shifts', 'StockTransactions', 'StockTransactionDetails', 
                    'AuditLogs', 'DatabaseBackupLogs', 'Customers'
                );
            ");
        }
        catch { }

        // Bật lại Foreign Keys và tối ưu CSDL SQLite
        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
        context.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(FULL);");
        context.Database.ExecuteSqlRaw("VACUUM;");
        context.SaveChanges();
    }
}
