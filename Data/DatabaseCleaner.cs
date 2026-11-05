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

    /// <summary>
    /// Đưa toàn bộ hệ thống về TRẮNG 100% chuẩn bản thương mại xuất bản cho khách hàng:
    /// - 0 Bàn, 0 Khu vực, 0 Tầng, 0 Loại bàn
    /// - 0 Món ăn, 0 Danh mục, 0 Tồn kho
    /// - 0 Gói combo, 0 Khung giờ giá
    /// - 0 Hóa đơn, 0 Ca trực, 0 Khách hàng
    /// - Tiền két ban đầu = 0 đ (Khách tự nạp khi mở ca đầu tiên)
    /// - DUY NHẤT 1 TÀI KHOẢN ADMIN: admin / admin123
    /// </summary>
    public static void WipeAllToCommercialBlank(AppDbContext context)
    {
        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");

        // 1. Xóa toàn bộ dữ liệu giao dịch & nghiệp vụ
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

        // 2. Xóa bàn, tầng, phòng, loại bàn, khung giờ, combo
        context.Database.ExecuteSqlRaw("DELETE FROM PlayComboItems;");
        context.Database.ExecuteSqlRaw("DELETE FROM PlayCombos;");
        context.Database.ExecuteSqlRaw("DELETE FROM TimeSlotPricings;");
        context.Database.ExecuteSqlRaw("DELETE FROM Tables;");
        context.Database.ExecuteSqlRaw("DELETE FROM Areas;");
        context.Database.ExecuteSqlRaw("DELETE FROM Floors;");
        context.Database.ExecuteSqlRaw("DELETE FROM TableTypes;");

        // 3. Xóa món ăn, danh mục, tồn kho
        context.Database.ExecuteSqlRaw("DELETE FROM InventoryItems;");
        context.Database.ExecuteSqlRaw("DELETE FROM Products;");
        context.Database.ExecuteSqlRaw("DELETE FROM ProductCategories;");

        // 4. Chỉ giữ duy nhất 1 tài khoản Admin
        context.Database.ExecuteSqlRaw("DELETE FROM Users;");
        var adminSalt = SecurityHelper.GenerateSalt();
        context.Users.Add(new User
        {
            Username = "admin",
            PasswordHash = SecurityHelper.HashPassword("admin123", adminSalt),
            Salt = adminSalt,
            FullName = "Quản Trị Viên",
            PhoneNumber = "0900000000",
            Role = UserRole.Admin,
            IsActive = true
        });
        context.SaveChanges();

        // 5. Cài đặt hệ thống về mặc định mẫu cho khách mới
        var settingName = context.SystemSettings.Find("ClubName");
        if (settingName != null) settingName.SettingValue = "CÂU LẠC BỘ BILLIARDS";
        var settingAddr = context.SystemSettings.Find("ClubAddress");
        if (settingAddr != null) settingAddr.SettingValue = "Chưa cập nhật địa chỉ quán";
        var settingPhone = context.SystemSettings.Find("ClubPhone");
        if (settingPhone != null) settingPhone.SettingValue = "0900.000.000";
        var settingBankBin = context.SystemSettings.Find("BankBin");
        if (settingBankBin != null) settingBankBin.SettingValue = "970422";
        var settingAccNo = context.SystemSettings.Find("BankAccountNo");
        if (settingAccNo != null) settingAccNo.SettingValue = "0000000000";
        var settingAccName = context.SystemSettings.Find("BankAccountName");
        if (settingAccName != null) settingAccName.SettingValue = "CHU TAI KHOAN";
        var initCash = context.SystemSettings.Find("InitialDrawerCash");
        if (initCash != null) initCash.SettingValue = "0";

        // 6. Reset id tự tăng
        try
        {
            context.Database.ExecuteSqlRaw("DELETE FROM sqlite_sequence;");
        }
        catch { }

        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
        context.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(FULL);");
        context.Database.ExecuteSqlRaw("VACUUM;");
        context.SaveChanges();
    }
}
