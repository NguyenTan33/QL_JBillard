using Microsoft.EntityFrameworkCore;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Helpers;

namespace QL_JBillard.Data;

public static class DatabaseInitializer
{
    public static void Initialize(AppDbContext context)
    {
        // Tự động tạo cấu trúc bảng CSDL SQLite nếu chưa tồn tại
        context.Database.EnsureCreated();

        // Nâng cấp cấu trúc bảng nếu database đã tồn tại từ các phiên bản trước
        try
        {
            context.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS BankDepositSlips (
                    DepositSlipId INTEGER PRIMARY KEY AUTOINCREMENT,
                    SlipCode TEXT NOT NULL,
                    ShiftId INTEGER NULL,
                    UserId INTEGER NOT NULL,
                    Amount TEXT NOT NULL,
                    BankName TEXT NULL,
                    BankAccountNo TEXT NULL,
                    BankAccountName TEXT NULL,
                    ImagePath TEXT NULL,
                    Notes TEXT NULL,
                    CreatedAt TEXT NOT NULL,
                    Status TEXT NOT NULL DEFAULT 'Approved',
                    ApprovedByUserId INTEGER NULL,
                    ApprovedAt TEXT NULL,
                    IsModified INTEGER NOT NULL DEFAULT 0
                );
            ");
            context.Database.ExecuteSqlRaw("ALTER TABLE Shifts ADD COLUMN PreviousShiftClosingCash TEXT NULL;");
        }
        catch { }

        try { context.Database.ExecuteSqlRaw("ALTER TABLE Shifts ADD COLUMN IsOpeningDiscrepant INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { context.Database.ExecuteSqlRaw("ALTER TABLE Shifts ADD COLUMN OpeningDiscrepancyNotes TEXT NULL;"); } catch { }
        try { context.Database.ExecuteSqlRaw("ALTER TABLE Shifts ADD COLUMN TotalBankDeposited TEXT NOT NULL DEFAULT '0';"); } catch { }

        // Migration cho Invoices và OrderItems
        try { context.Database.ExecuteSqlRaw("ALTER TABLE Invoices ADD COLUMN ShiftId INTEGER NULL;"); } catch { }
        try { context.Database.ExecuteSqlRaw("ALTER TABLE TableSessions ADD COLUMN AccumulatedPlayMinutes INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { context.Database.ExecuteSqlRaw("ALTER TABLE OrderItems ADD COLUMN IsCompleted INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { context.Database.ExecuteSqlRaw("ALTER TABLE OrderItems ADD COLUMN CompletedAt TEXT NULL;"); } catch { }
        try { context.Database.ExecuteSqlRaw("ALTER TABLE OrderItems ADD COLUMN CompletedByUserId INTEGER NULL;"); } catch { }
        try { context.Database.ExecuteSqlRaw("ALTER TABLE OrderItems ADD COLUMN IsComboItem INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { context.Database.ExecuteSqlRaw("ALTER TABLE OrderItems ADD COLUMN ComboName TEXT NULL;"); } catch { }

        // Migration cho Khung Giờ Giá & Gói Combo (v2.5)
        try
        {
            context.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS TimeSlotPricings (
                    TimeSlotId INTEGER PRIMARY KEY AUTOINCREMENT,
                    TableTypeId INTEGER NOT NULL,
                    SlotName TEXT NOT NULL,
                    StartHour INTEGER NOT NULL DEFAULT 8,
                    StartMinute INTEGER NOT NULL DEFAULT 0,
                    EndHour INTEGER NOT NULL DEFAULT 12,
                    EndMinute INTEGER NOT NULL DEFAULT 0,
                    HourlyRate TEXT NOT NULL DEFAULT '50000',
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    FOREIGN KEY (TableTypeId) REFERENCES TableTypes(TableTypeId) ON DELETE CASCADE
                );
            ");
            context.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS PlayCombos (
                    ComboId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ComboName TEXT NOT NULL,
                    TableTypeId INTEGER NULL,
                    Price TEXT NOT NULL DEFAULT '150000',
                    DurationMinutes INTEGER NOT NULL DEFAULT 180,
                    IncludedItems TEXT NULL,
                    Description TEXT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    FOREIGN KEY (TableTypeId) REFERENCES TableTypes(TableTypeId) ON DELETE SET NULL
                );
            ");
            context.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS PlayComboItems (
                    ComboItemId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ComboId INTEGER NOT NULL,
                    ProductId INTEGER NOT NULL,
                    Quantity INTEGER NOT NULL DEFAULT 1,
                    FOREIGN KEY (ComboId) REFERENCES PlayCombos(ComboId) ON DELETE CASCADE,
                    FOREIGN KEY (ProductId) REFERENCES Products(ProductId) ON DELETE CASCADE
                );
            ");
        }
        catch { }

        try { context.Database.ExecuteSqlRaw("ALTER TABLE TableSessions ADD COLUMN PlayMode TEXT NOT NULL DEFAULT 'HOURLY';"); } catch { }
        try { context.Database.ExecuteSqlRaw("ALTER TABLE TableSessions ADD COLUMN ComboId INTEGER NULL;"); } catch { }
        try { context.Database.ExecuteSqlRaw("ALTER TABLE TableSessions ADD COLUMN ComboName TEXT NULL;"); } catch { }
        try { context.Database.ExecuteSqlRaw("ALTER TABLE TableSessions ADD COLUMN ComboPrice TEXT NULL;"); } catch { }
        try { context.Database.ExecuteSqlRaw("ALTER TABLE TableSessions ADD COLUMN ComboDurationMinutes INTEGER NULL;"); } catch { }
        try { context.Database.ExecuteSqlRaw("ALTER TABLE TableSessions ADD COLUMN TransferHistoryJson TEXT NULL;"); } catch { }
        try { context.Database.ExecuteSqlRaw("ALTER TABLE Invoices ADD COLUMN AppliedComboName TEXT NULL;"); } catch { }

        // 1. Seed System Settings nếu chưa có
        if (!context.SystemSettings.Any())
        {
            context.SystemSettings.AddRange(
                new SystemSetting { SettingKey = "ClubName", SettingValue = "J-BILLIARDS CLUB & LOUNGE", Description = "Tên câu lạc bộ bida" },
                new SystemSetting { SettingKey = "ClubAddress", SettingValue = "123 Đường Số 9, P. Linh Trung, TP. Thủ Đức, TP. Hồ Chí Minh", Description = "Địa chỉ quán" },
                new SystemSetting { SettingKey = "ClubPhone", SettingValue = "0901.234.567", Description = "Số điện thoại hotline" },
                new SystemSetting { SettingKey = "BankBin", SettingValue = "970422", Description = "Mã định danh ngân hàng (970422 là MBBank)" },
                new SystemSetting { SettingKey = "BankAccountNo", SettingValue = "012345678999", Description = "Số tài khoản nhận tiền VietQR" },
                new SystemSetting { SettingKey = "BankAccountName", SettingValue = "CLB BIDA J BILLIARDS", Description = "Tên chủ tài khoản thụ hưởng" },
                new SystemSetting { SettingKey = "AutoBackupIntervalMinutes", SettingValue = "30", Description = "Khoảng thời gian tự động sao lưu CSDL (phút)" },
                new SystemSetting { SettingKey = "InitialDrawerCash", SettingValue = "1000000", Description = "Tiền két mặc định ban đầu khi mở ca (1.000.000đ)" },
                new SystemSetting { SettingKey = "PointRatePerVnd", SettingValue = "10000", Description = "Số tiền tương ứng 1 điểm tích lũy (10,000đ = 1 điểm)" },
                new SystemSetting { SettingKey = "PointRedeemValue", SettingValue = "1000", Description = "Giá trị quy đổi 1 điểm khi trừ vào hóa đơn (1 điểm = 1,000đ)" }
            );
            context.SaveChanges();
        }
        else
        {
            var autoBackupSetting = context.SystemSettings.Find("AutoBackupIntervalMinutes");
            if (autoBackupSetting == null)
            {
                context.SystemSettings.Add(new SystemSetting { SettingKey = "AutoBackupIntervalMinutes", SettingValue = "30", Description = "Khoảng thời gian tự động sao lưu CSDL (phút)" });
                context.SaveChanges();
            }

            var initDrawerSetting = context.SystemSettings.Find("InitialDrawerCash");
            if (initDrawerSetting == null)
            {
                context.SystemSettings.Add(new SystemSetting { SettingKey = "InitialDrawerCash", SettingValue = "1000000", Description = "Tiền két mặc định ban đầu khi mở ca (1.000.000đ)" });
                context.SaveChanges();
            }
        }

        // 2. Seed Người dùng mặc định: Chỉ giữ duy nhất tài khoản Quản trị viên (Admin)
        if (!context.Users.Any())
        {
            var adminSalt = SecurityHelper.GenerateSalt();

            context.Users.Add(
                new User
                {
                    Username = "admin",
                    PasswordHash = SecurityHelper.HashPassword("admin123", adminSalt),
                    Salt = adminSalt,
                    FullName = "Quản Trị Viên",
                    PhoneNumber = "0909999888",
                    Role = UserRole.Admin,
                    IsActive = true
                }
            );
            context.SaveChanges();
        }

        // 3. Seed Hạng Thành Viên
        if (!context.MembershipTiers.Any())
        {
            context.MembershipTiers.AddRange(
                new MembershipTier { TierName = "Tiêu Chuẩn (Standard)", MinSpentThreshold = 0, DiscountPercentHour = 0, DiscountPercentFnb = 0, PointEarnRate = 0.01m },
                new MembershipTier { TierName = "Hội Viên Bạc (Silver)", MinSpentThreshold = 1_000_000, DiscountPercentHour = 5, DiscountPercentFnb = 0, PointEarnRate = 0.012m },
                new MembershipTier { TierName = "Hội Viên Vàng (Gold)", MinSpentThreshold = 5_000_000, DiscountPercentHour = 10, DiscountPercentFnb = 5, PointEarnRate = 0.015m },
                new MembershipTier { TierName = "VIP Kim Cương (Diamond)", MinSpentThreshold = 15_000_000, DiscountPercentHour = 15, DiscountPercentFnb = 10, PointEarnRate = 0.02m }
            );
            context.SaveChanges();
        }

        // 4. Seed Không Gian: Tầng, Khu Vực, Loại Bàn
        if (!context.Floors.Any())
        {
            var f1 = new Floor { FloorName = "Tầng 1 - Sảnh Chính", OrderIndex = 1 };
            var f2 = new Floor { FloorName = "Tầng 2 - Phòng VIP Lounge", OrderIndex = 2 };
            context.Floors.AddRange(f1, f2);
            context.SaveChanges();

            var a1 = new Area { AreaName = "Khu Bàn Lỗ (Pool 9-Ball)", FloorId = f1.FloorId };
            var a2 = new Area { AreaName = "Khu Bàn Phăng (Libre)", FloorId = f1.FloorId };
            var a3 = new Area { AreaName = "Phòng VIP Lạnh", FloorId = f2.FloorId };
            var a4 = new Area { AreaName = "Khu 3 Băng & Snooker Match", FloorId = f2.FloorId };
            context.Areas.AddRange(a1, a2, a3, a4);
            context.SaveChanges();

            var tPool = new TableType { TypeName = "Bida Lỗ (Pool 9-Ball)", DefaultHourlyRate = 50_000, MinPlayMinutes = 15, ColorCode = "#10B981" };
            var tLibre = new TableType { TypeName = "Bida Phăng (Libre)", DefaultHourlyRate = 45_000, MinPlayMinutes = 15, ColorCode = "#3B82F6" };
            var tVip = new TableType { TypeName = "Bàn VIP (Phòng Lạnh)", DefaultHourlyRate = 80_000, MinPlayMinutes = 15, ColorCode = "#8B5CF6" };
            var tCarom = new TableType { TypeName = "3 Băng (Carom 3C Master)", DefaultHourlyRate = 100_000, MinPlayMinutes = 15, ColorCode = "#F59E0B" };
            var tSnooker = new TableType { TypeName = "Snooker Master Thi Đấu", DefaultHourlyRate = 120_000, MinPlayMinutes = 15, ColorCode = "#EC4899" };
            context.TableTypes.AddRange(tPool, tLibre, tVip, tCarom, tSnooker);
            context.SaveChanges();

            // Seed danh sách 14 bàn chuẩn câu lạc bộ
            var tables = new List<Table>
            {
                // Khu Lỗ Tầng 1
                new Table { TableNumber = "BÀN 01", AreaId = a1.AreaId, TableTypeId = tPool.TableTypeId, Status = TableStatus.Available },
                new Table { TableNumber = "BÀN 02", AreaId = a1.AreaId, TableTypeId = tPool.TableTypeId, Status = TableStatus.Available },
                new Table { TableNumber = "BÀN 03", AreaId = a1.AreaId, TableTypeId = tPool.TableTypeId, Status = TableStatus.Available },
                new Table { TableNumber = "BÀN 04", AreaId = a1.AreaId, TableTypeId = tPool.TableTypeId, Status = TableStatus.Available },

                // Khu Libre Tầng 1
                new Table { TableNumber = "BÀN 05", AreaId = a2.AreaId, TableTypeId = tLibre.TableTypeId, Status = TableStatus.Available },
                new Table { TableNumber = "BÀN 06", AreaId = a2.AreaId, TableTypeId = tLibre.TableTypeId, Status = TableStatus.Available },
                new Table { TableNumber = "BÀN 07", AreaId = a2.AreaId, TableTypeId = tLibre.TableTypeId, Status = TableStatus.Available },
                new Table { TableNumber = "BÀN 08", AreaId = a2.AreaId, TableTypeId = tLibre.TableTypeId, Status = TableStatus.Available },

                // Khu VIP Tầng 2
                new Table { TableNumber = "VIP 01", AreaId = a3.AreaId, TableTypeId = tVip.TableTypeId, Status = TableStatus.Available },
                new Table { TableNumber = "VIP 02", AreaId = a3.AreaId, TableTypeId = tVip.TableTypeId, Status = TableStatus.Available },
                new Table { TableNumber = "VIP 03", AreaId = a3.AreaId, TableTypeId = tVip.TableTypeId, Status = TableStatus.Available },

                // Khu 3 Băng & Snooker Tầng 2
                new Table { TableNumber = "CAROM 01", AreaId = a4.AreaId, TableTypeId = tCarom.TableTypeId, Status = TableStatus.Available },
                new Table { TableNumber = "CAROM 02", AreaId = a4.AreaId, TableTypeId = tCarom.TableTypeId, Status = TableStatus.Available },
                new Table { TableNumber = "SNOOKER 01", AreaId = a4.AreaId, TableTypeId = tSnooker.TableTypeId, Status = TableStatus.Available }
            };
            context.Tables.AddRange(tables);
            context.SaveChanges();
        }

        // 5. Seed Danh Mục, Đơn Vị Tính & Sản Phẩm F&B / Kho Vật Tư
        if (!context.ProductCategories.Any())
        {
            var catDrink = new ProductCategory { CategoryName = "Đồ Uống Lon & Chai" };
            var catCoffee = new ProductCategory { CategoryName = "Cà Phê & Pha Chế" };
            var catBeer = new ProductCategory { CategoryName = "Bia & Cồn" };
            var catFood = new ProductCategory { CategoryName = "Đồ Ăn Nhanh Chế Biến" };
            var catGear = new ProductCategory { CategoryName = "Phụ Kiện Bida" };
            context.ProductCategories.AddRange(catDrink, catCoffee, catBeer, catFood, catGear);

            var uLon = new Unit { UnitName = "Lon" };
            var uChai = new Unit { UnitName = "Chai" };
            var uLy = new Unit { UnitName = "Ly" };
            var uDia = new Unit { UnitName = "Đĩa" };
            var uGoi = new Unit { UnitName = "Gói" };
            var uCai = new Unit { UnitName = "Cái" };
            var uVien = new Unit { UnitName = "Viên" };
            context.Units.AddRange(uLon, uChai, uLy, uDia, uGoi, uCai, uVien);
            context.SaveChanges();

            // Danh sách sản phẩm thực tế
            var products = new List<Product>
            {
                // Đồ uống lon & chai
                new Product { ProductCode = "STING", ProductName = "Nước Tăng Lực Sting Dâu", CategoryId = catDrink.CategoryId, UnitId = uLon.UnitId, CostPrice = 9_000, SellingPrice = 16_000, MinStockAlert = 12, IsInventoryItem = true },
                new Product { ProductCode = "REDBULL", ProductName = "Nước Tăng Lực Bò Húc Thái", CategoryId = catDrink.CategoryId, UnitId = uLon.UnitId, CostPrice = 12_000, SellingPrice = 20_000, MinStockAlert = 12, IsInventoryItem = true },
                new Product { ProductCode = "LAVIE", ProductName = "Nước Khoáng Thiên Nhiên Lavie 500ml", CategoryId = catDrink.CategoryId, UnitId = uChai.UnitId, CostPrice = 4_500, SellingPrice = 10_000, MinStockAlert = 20, IsInventoryItem = true },
                new Product { ProductCode = "COCA", ProductName = "Nước Ngọt Coca Cola Original", CategoryId = catDrink.CategoryId, UnitId = uLon.UnitId, CostPrice = 9_500, SellingPrice = 16_000, MinStockAlert = 15, IsInventoryItem = true },

                // Cà phê & Pha chế
                new Product { ProductCode = "CF_DEN", ProductName = "Cà Phê Đen Đá Pha Phin", CategoryId = catCoffee.CategoryId, UnitId = uLy.UnitId, CostPrice = 6_000, SellingPrice = 22_000, MinStockAlert = 0, IsInventoryItem = false },
                new Product { ProductCode = "CF_SUA", ProductName = "Cà Phê Sữa Đá Sài Gòn", CategoryId = catCoffee.CategoryId, UnitId = uLy.UnitId, CostPrice = 8_000, SellingPrice = 25_000, MinStockAlert = 0, IsInventoryItem = false },
                new Product { ProductCode = "CF_MUOI", ProductName = "Cà Phê Muối J-Lounge Kem Béo", CategoryId = catCoffee.CategoryId, UnitId = uLy.UnitId, CostPrice = 9_000, SellingPrice = 32_000, MinStockAlert = 0, IsInventoryItem = false },
                new Product { ProductCode = "TRA_DAO", ProductName = "Trà Đào Cam Sả Tươi", CategoryId = catCoffee.CategoryId, UnitId = uLy.UnitId, CostPrice = 10_000, SellingPrice = 35_000, MinStockAlert = 0, IsInventoryItem = false },

                // Bia
                new Product { ProductCode = "HEINEKEN", ProductName = "Bia Heineken Silver (Lon Bạc)", CategoryId = catBeer.CategoryId, UnitId = uLon.UnitId, CostPrice = 18_000, SellingPrice = 28_000, MinStockAlert = 24, IsInventoryItem = true },
                new Product { ProductCode = "TIGER_CRYSTAL", ProductName = "Bia Tiger Crystal (Lon)", CategoryId = catBeer.CategoryId, UnitId = uLon.UnitId, CostPrice = 16_500, SellingPrice = 25_000, MinStockAlert = 24, IsInventoryItem = true },

                // Đồ ăn nhanh
                new Product { ProductCode = "MI_BO_TRUNG", ProductName = "Mì Xào Bò Trứng Ốp La", CategoryId = catFood.CategoryId, UnitId = uDia.UnitId, CostPrice = 20_000, SellingPrice = 45_000, MinStockAlert = 0, IsInventoryItem = false },
                new Product { ProductCode = "KHO_BO", ProductName = "Khô Bò Vắt Chanh Ớt", CategoryId = catFood.CategoryId, UnitId = uDia.UnitId, CostPrice = 28_000, SellingPrice = 50_000, MinStockAlert = 10, IsInventoryItem = true },
                new Product { ProductCode = "HUONG_DUONG", ProductName = "Đĩa Hướng Dương Tẩm Vị", CategoryId = catFood.CategoryId, UnitId = uDia.UnitId, CostPrice = 6_000, SellingPrice = 15_000, MinStockAlert = 15, IsInventoryItem = true },
                new Product { ProductCode = "KHAN_LANH", ProductName = "Khăn Lạnh Cao Cấp Hương Quế", CategoryId = catFood.CategoryId, UnitId = uCai.UnitId, CostPrice = 800, SellingPrice = 3_000, MinStockAlert = 50, IsInventoryItem = true },

                // Phụ kiện bida
                new Product { ProductCode = "GANG_TAY", ProductName = "Găng Tay Bida Cuetec 3 Ngón", CategoryId = catGear.CategoryId, UnitId = uCai.UnitId, CostPrice = 40_000, SellingPrice = 70_000, MinStockAlert = 8, IsInventoryItem = true },
                new Product { ProductCode = "LO_XANH", ProductName = "Lơ Xanh Master USA Chính Hãng", CategoryId = catGear.CategoryId, UnitId = uVien.UnitId, CostPrice = 12_000, SellingPrice = 25_000, MinStockAlert = 10, IsInventoryItem = true },
                new Product { ProductCode = "DAU_CO", ProductName = "Đầu Cơ Kamui Black Nhật Bản", CategoryId = catGear.CategoryId, UnitId = uCai.UnitId, CostPrice = 120_000, SellingPrice = 180_000, MinStockAlert = 5, IsInventoryItem = true }
            };

            context.Products.AddRange(products);
            context.SaveChanges();

            // Khởi tạo bảng tồn kho InventoryItem
            foreach (var p in products.Where(x => x.IsInventoryItem))
            {
                context.InventoryItems.Add(new InventoryItem
                {
                    ProductId = p.ProductId,
                    QuantityOnHand = 48,
                    LastRestockedAt = DateTime.Now
                });
            }
            context.SaveChanges();
        }
        else
        {
            var autoBackupSetting = context.SystemSettings.Find("AutoBackupIntervalMinutes");
            if (autoBackupSetting == null)
            {
                context.SystemSettings.Add(new SystemSetting { SettingKey = "AutoBackupIntervalMinutes", SettingValue = "30", Description = "Khoảng thời gian tự động sao lưu CSDL (phút)" });
                context.SaveChanges();
            }
        }

        // Khởi tạo bảng giá theo Khung Giờ nếu chưa có
        if (!context.TimeSlotPricings.Any())
        {
            var firstType = context.TableTypes.FirstOrDefault();
            if (firstType != null)
            {
                context.TimeSlotPricings.AddRange(
                    new TimeSlotPricing { TableTypeId = firstType.TableTypeId, SlotName = "Khung Giờ Sáng (08:00 - 12:00)", StartHour = 8, StartMinute = 0, EndHour = 12, EndMinute = 0, HourlyRate = 50_000, IsActive = true },
                    new TimeSlotPricing { TableTypeId = firstType.TableTypeId, SlotName = "Khung Giờ Chiều (12:00 - 18:00)", StartHour = 12, StartMinute = 0, EndHour = 18, EndMinute = 0, HourlyRate = 60_000, IsActive = true },
                    new TimeSlotPricing { TableTypeId = firstType.TableTypeId, SlotName = "Khung Giờ Tối / Vàng (18:00 - 24:00)", StartHour = 18, StartMinute = 0, EndHour = 24, EndMinute = 0, HourlyRate = 70_000, IsActive = true },
                    new TimeSlotPricing { TableTypeId = firstType.TableTypeId, SlotName = "Khung Giờ Đêm Khuya (00:00 - 08:00)", StartHour = 0, StartMinute = 0, EndHour = 8, EndMinute = 0, HourlyRate = 80_000, IsActive = true }
                );
                context.SaveChanges();
            }
        }

        // Khởi tạo Gói Combo bida + F&B nếu chưa có
        if (!context.PlayCombos.Any())
        {
            var pSting = context.Products.FirstOrDefault(p => p.ProductCode == "STING");
            var pTraDao = context.Products.FirstOrDefault(p => p.ProductCode == "TRA_DAO");
            var pMiBo = context.Products.FirstOrDefault(p => p.ProductCode == "MI_BO_TRUNG");

            var c1 = new PlayCombo
            {
                ComboName = "Combo 3 Tiếng + 2 Nước",
                Price = 150_000,
                DurationMinutes = 180,
                IncludedItems = "2x Nước Tăng Lực Sting Dâu",
                Description = "Gói 3 giờ chơi bida tặng 2 lon nước ngọt/nước ép, siêu tiết kiệm",
                IsActive = true
            };
            var c2 = new PlayCombo
            {
                ComboName = "Combo Sinh Viên 2h + 2 Trà Đá",
                Price = 70_000,
                DurationMinutes = 120,
                IncludedItems = "2x Trà Đào Cam Sả Tươi",
                Description = "Gói chơi 2 tiếng giá học sinh sinh viên",
                IsActive = true
            };
            var c3 = new PlayCombo
            {
                ComboName = "Combo Cày Đêm 4h + 2 Sting + 1 Mì",
                Price = 220_000,
                DurationMinutes = 240,
                IncludedItems = "2x Nước Tăng Lực Sting Dâu, 1x Mì Xào Bò Trứng Ốp La",
                Description = "Gói chơi 4 tiếng xuyên đêm kèm đồ ăn khuya",
                IsActive = true
            };

            context.PlayCombos.AddRange(c1, c2, c3);
            context.SaveChanges();

            if (pSting != null) c1.Items.Add(new PlayComboItem { ComboId = c1.ComboId, ProductId = pSting.ProductId, Quantity = 2 });
            if (pTraDao != null) c2.Items.Add(new PlayComboItem { ComboId = c2.ComboId, ProductId = pTraDao.ProductId, Quantity = 2 });
            if (pSting != null) c3.Items.Add(new PlayComboItem { ComboId = c3.ComboId, ProductId = pSting.ProductId, Quantity = 2 });
            if (pMiBo != null) c3.Items.Add(new PlayComboItem { ComboId = c3.ComboId, ProductId = pMiBo.ProductId, Quantity = 1 });
            context.SaveChanges();
        }
        else if (!context.PlayComboItems.Any())
        {
            var pSting = context.Products.FirstOrDefault(p => p.ProductCode == "STING");
            var pTraDao = context.Products.FirstOrDefault(p => p.ProductCode == "TRA_DAO");
            var pMiBo = context.Products.FirstOrDefault(p => p.ProductCode == "MI_BO_TRUNG");

            var allCombos = context.PlayCombos.ToList();
            foreach (var combo in allCombos)
            {
                string name = combo.ComboName.ToLower();
                if (name.Contains("sinh viên") || name.Contains("trà đá"))
                {
                    if (pTraDao != null) context.PlayComboItems.Add(new PlayComboItem { ComboId = combo.ComboId, ProductId = pTraDao.ProductId, Quantity = 2 });
                    combo.IncludedItems = "2x Trà Đào Cam Sả Tươi";
                }
                else if (name.Contains("cày đêm") || name.Contains("mì"))
                {
                    if (pSting != null) context.PlayComboItems.Add(new PlayComboItem { ComboId = combo.ComboId, ProductId = pSting.ProductId, Quantity = 2 });
                    if (pMiBo != null) context.PlayComboItems.Add(new PlayComboItem { ComboId = combo.ComboId, ProductId = pMiBo.ProductId, Quantity = 1 });
                    combo.IncludedItems = "2x Nước Tăng Lực Sting Dâu, 1x Mì Xào Bò Trứng Ốp La";
                }
                else
                {
                    if (pSting != null) context.PlayComboItems.Add(new PlayComboItem { ComboId = combo.ComboId, ProductId = pSting.ProductId, Quantity = 2 });
                    combo.IncludedItems = "2x Nước Tăng Lực Sting Dâu";
                }
            }
            context.SaveChanges();
        }

        // Đảm bảo tất cả sản phẩm đều có ảnh minh họa chuẩn, đẹp và rõ nét
        try
        {
            Services.Implementations.ProductImageGenerator.EnsureDefaultProductImages(context);
        }
        catch { }
    }
}
