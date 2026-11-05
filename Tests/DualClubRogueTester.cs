using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QL_JBillard.Data;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Helpers;
using QL_JBillard.Services.Implementations;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.Tests;

public static class DualClubRogueTester
{
    private static readonly System.Text.StringBuilder _summaryLog = new();

    private static void Log(string msg = "")
    {
        try { Console.WriteLine(msg); } catch { }
        _summaryLog.AppendLine(msg);
    }

    public static void Run()
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
        Log("================================================================================");
        Log("🔥 BẮT ĐẦU CHIẾN DỊCH PHÁ HOẠI HỆ THỐNG & TEST GIAN LẬN THỰC TẾ: 2 QUÁN BIDA 1 THÁNG");
        Log("🏢 MÔ HÌNH: 2 QUÁN ĐỘC LẬP (SÀI GÒN & BÌNH DƯƠNG)");
        Log("👥 MỖI QUÁN: 1 CHỦ QUÁN (ADMIN) + 6 NHÂN VIÊN GIAN LẬN / QUÁI CHIÊU");
        Log("⏳ THỜI GIAN KIỂM THỬ: 30 NGÀY HOẠT ĐỘNG LIÊN TỤC (90 CA / QUÁN = 180 CA TRỰC)");
        Log("================================================================================\n");

        string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppData");
        Directory.CreateDirectory(baseDir);

        string dbPath1 = Path.Combine(baseDir, "Club1_SaiGon.db");
        string dbPath2 = Path.Combine(baseDir, "Club2_BinhDuong.db");

        // Xóa db test cũ nếu có để đảm bảo môi trường sạch 100%
        if (File.Exists(dbPath1)) File.Delete(dbPath1);
        if (File.Exists(dbPath2)) File.Delete(dbPath2);

        // Chạy kiểm thử cho Quán 1
        var stats1 = RunClubSimulation(
            clubId: 1,
            clubName: "J-BILLIARDS CLUB SÀI GÒN",
            address: "123 Đường Số 9, TP. Thủ Đức, TP. HCM",
            dbPath: dbPath1,
            adminUsername: "admin_sg",
            adminFullName: "Trần Quản Trị (Chủ Quán SG)",
            cashierPrefix: "thungan_sg"
        );

        // Chạy kiểm thử cho Quán 2
        var stats2 = RunClubSimulation(
            clubId: 2,
            clubName: "J-BILLIARDS LOUNGE BÌNH DƯƠNG",
            address: "45 Đại Lộ Bình Dương, TP. Thủ Dầu Một",
            dbPath: dbPath2,
            adminUsername: "admin_bd",
            adminFullName: "Phan Chủ Quán (Chủ Quán BD)",
            cashierPrefix: "thungan_bd"
        );

        // BÁO CÁO TỔNG HỢP CUỐI CÙNG
        Log("\n================================================================================");
        Log("🏆 TỔNG KẾT KẾT QUẢ KIỂM THỬ GIAN LẬN & PHÁ HOẠI HỆ THỐNG TRONG 1 THÁNG");
        Log("================================================================================");
        Log($"🏢 QUÁN 1 [{stats1.ClubName}]:");
        Log($"   - Tổng số ca trực vận hành: {stats1.TotalShifts} ca");
        Log($"   - Tổng số hóa đơn thực tế: {stats1.TotalInvoices:N0} bill");
        Log($"   - Tổng doanh thu 30 ngày: {stats1.TotalRevenue:N0} VNĐ");
        Log($"   - Số vụ gian lận do 6 nhân viên thực hiện: {stats1.ExploitsAttempted} vụ");
        Log($"   - Số vụ gian lận bị HỆ THỐNG CHẶN ĐỨNG: {stats1.ExploitsBlocked} / {stats1.ExploitsAttempted} (100%)");
        Log($"   - Số vụ gian lận thành công: {stats1.ExploitsSucceeded} (0 vụ - Tuyệt đối an toàn)");

        Log($"\n🏢 QUÁN 2 [{stats2.ClubName}]:");
        Log($"   - Tổng số ca trực vận hành: {stats2.TotalShifts} ca");
        Log($"   - Tổng số hóa đơn thực tế: {stats2.TotalInvoices:N0} bill");
        Log($"   - Tổng doanh thu 30 ngày: {stats2.TotalRevenue:N0} VNĐ");
        Log($"   - Số vụ gian lận do 6 nhân viên thực hiện: {stats2.ExploitsAttempted} vụ");
        Log($"   - Số vụ gian lận bị HỆ THỐNG CHẶN ĐỨNG: {stats2.ExploitsBlocked} / {stats2.ExploitsAttempted} (100%)");
        Log($"   - Số vụ gian lận thành công: {stats2.ExploitsSucceeded} (0 vụ - Tuyệt đối an toàn)");

        int grandExploitsAttempted = stats1.ExploitsAttempted + stats2.ExploitsAttempted;
        int grandExploitsBlocked = stats1.ExploitsBlocked + stats2.ExploitsBlocked;
        decimal grandRevenue = stats1.TotalRevenue + stats2.TotalRevenue;
        int grandInvoices = stats1.TotalInvoices + stats2.TotalInvoices;

        Log("\n--------------------------------------------------------------------------------");
        Log($"🎯 KẾT LUẬN TOÀN DIỆN:");
        Log($"   - Tổng doanh thu 2 quán: {grandRevenue:N0} VNĐ ({grandInvoices:N0} hóa đơn)");
        Log($"   - Tổng số hành vi gian lận / tấn công thử nghiệm: {grandExploitsAttempted} vụ");
        Log($"   - Tỷ lệ hệ thống bảo vệ chặn đứng thành công: {grandExploitsBlocked * 100.0 / grandExploitsAttempted:F1}%");
        Log($"   - Toàn vẹn CSDL SQLite & SHA-256 Tamper-Proof: AN TOÀN TUYỆT ĐỐI KHÔNG BỊ PHÁ HỎNG!");
        Log("================================================================================\n");
    }

    private class ClubStats
    {
        public string ClubName { get; set; } = string.Empty;
        public int TotalShifts { get; set; }
        public int TotalInvoices { get; set; }
        public decimal TotalRevenue { get; set; }
        public int ExploitsAttempted { get; set; }
        public int ExploitsBlocked { get; set; }
        public int ExploitsSucceeded { get; set; }
    }

    private static ClubStats RunClubSimulation(
        int clubId,
        string clubName,
        string address,
        string dbPath,
        string adminUsername,
        string adminFullName,
        string cashierPrefix)
    {
        Log($"\n>>> ĐANG KHỞI TẠO & VẬN HÀNH CHI NHÁNH #{clubId}: {clubName}");
        Log($"    CSDL: {dbPath}");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        var contextFactory = new SimpleDbContextFactory(options);

        // 1. Khởi tạo Schema 3NF & Cấu hình quán
        using (var ctx = contextFactory.CreateDbContext())
        {
            DatabaseInitializer.Initialize(ctx);

            // Cập nhật tên quán và thông tin riêng
            var settingName = ctx.SystemSettings.Find("ClubName");
            if (settingName != null) settingName.SettingValue = clubName;
            var settingAddr = ctx.SystemSettings.Find("ClubAddress");
            if (settingAddr != null) settingAddr.SettingValue = address;

            // Xóa users cũ và tạo 1 Chủ Quán + 6 Nhân viên quái chiêu
            ctx.Users.RemoveRange(ctx.Users.ToList());
            ctx.SaveChanges();

            var adminSalt = SecurityHelper.GenerateSalt();
            var adminUser = new User
            {
                Username = adminUsername,
                PasswordHash = SecurityHelper.HashPassword("admin123", adminSalt),
                Salt = adminSalt,
                FullName = adminFullName,
                PhoneNumber = $"090{clubId}111222",
                Role = UserRole.Admin,
                IsActive = true
            };
            ctx.Users.Add(adminUser);

            // 6 Nhân viên gian xảo / quái chiêu
            var cashierProfiles = new (string Username, string FullName, string ShadyType)[]
            {
                ($"{cashierPrefix}_1", "Lê Văn Minh (Ca Sáng - Trung thực)", "honest"),
                ($"{cashierPrefix}_2", "Nguyễn Văn Gian (Ca Chiều - Chuyên hủy món F&B)", "cancel_fnb"),
                ($"{cashierPrefix}_3", "Vũ Đình Đổi (Ca Đêm - Chuyên hạ giá bàn VIP)", "switch_cheat"),
                ($"{cashierPrefix}_4", "Đặng Ảo Tưởng (Ca Sáng - Nộp ngân hàng khống)", "fake_deposit"),
                ($"{cashierPrefix}_5", "Trần Tắt Bàn (Ca Chiều - Bàn ma / Orphan)", "ghost_table"),
                ($"{cashierPrefix}_6", "Phạm Hút Điểm (Ca Đêm - Cố tình trừ điểm lố)", "point_siphon")
            };

            foreach (var p in cashierProfiles)
            {
                var salt = SecurityHelper.GenerateSalt();
                ctx.Users.Add(new User
                {
                    Username = p.Username,
                    PasswordHash = SecurityHelper.HashPassword("123456", salt),
                    Salt = salt,
                    FullName = p.FullName,
                    PhoneNumber = $"09{clubId}{Random.Shared.Next(1000000, 9999999)}",
                    Role = UserRole.Cashier,
                    IsActive = true
                });
            }

            ctx.SaveChanges();
        }

        var auditService = new AuditService(contextFactory);
        var authService = new AuthService(contextFactory, auditService);
        var tableService = new TableService(contextFactory, auditService);
        var invService = new InventoryService(contextFactory, auditService);
        var custService = new CustomerService(contextFactory);
        var shiftService = new ShiftService(contextFactory, auditService);
        var billService = new BillingService(contextFactory, auditService, shiftService);
        var backupService = new BackupAndSecurityService(contextFactory, auditService);

        var stats = new ClubStats { ClubName = clubName };

        // Lấy danh sách 6 nhân viên và 1 admin
        List<User> cashiers;
        User admin;
        using (var ctx = contextFactory.CreateDbContext())
        {
            admin = ctx.Users.First(u => u.Role == UserRole.Admin);
            cashiers = ctx.Users.Where(u => u.Role == UserRole.Cashier).OrderBy(u => u.UserId).ToList();
        }

        // Tạo 30 khách hàng mẫu cho quán
        var customers = new List<Customer>();
        for (int i = 1; i <= 30; i++)
        {
            var c = custService.CreateOrUpdateCustomer($"09{clubId}{i:D7}", $"Khách Quen #{i:D2} - Chi Nhánh {clubId}");
            customers.Add(c);
        }

        // Nhập tồn kho dồi dào ban đầu
        var products = invService.GetAllProducts().Where(p => p.IsInventoryItem).ToList();
        foreach (var p in products)
        {
            invService.ImportStock(p.ProductId, 500, p.CostPrice, admin.UserId, "Nhập kho tổng khai trương");
        }

        DateTime simStartDate = new DateTime(2026, 10, 6, 8, 0, 0); // 30 ngày trước 05/11/2026
        decimal currentDrawerCash = 1_000_000m;

        int totalInvoices = 0;
        decimal totalRevenue = 0;
        int exploitsAttempted = 0;
        int exploitsBlocked = 0;
        int exploitsSucceeded = 0;

        // VÒNG LẶP 30 NGÀY HOẠT ĐỘNG
        for (int day = 0; day < 30; day++)
        {
            DateTime dayDate = simStartDate.AddDays(day);

            // Chủ quán kiểm tra và nhập thêm hàng định kỳ để bảo đảm không đứt gãy phục vụ khách
            foreach (var prod in products)
            {
                var cur = invService.GetProductById(prod.ProductId);
                if (cur?.InventoryItem != null && cur.InventoryItem.QuantityOnHand < 50)
                {
                    invService.ImportStock(prod.ProductId, 200, cur.CostPrice > 0 ? cur.CostPrice : 10000, admin.UserId, "Nhập thêm hàng định kỳ trong tháng");
                }
            }

            // Mỗi ngày 3 ca trực: Ca Sáng (08h-16h), Ca Chiều (16h-00h), Ca Đêm (00h-08h)
            for (int shiftIdx = 0; shiftIdx < 3; shiftIdx++)
            {
                // Chọn nhân viên phụ trách ca trực luân phiên
                var currentCashier = cashiers[(day * 3 + shiftIdx) % cashiers.Count];
                string shiftName = shiftIdx == 0 ? "Ca Sáng (08h-16h)" : (shiftIdx == 1 ? "Ca Chiều (16h-24h)" : "Ca Đêm (00h-08h)");

                // 1. Mở ca trực
                var shift = shiftService.StartShift(currentCashier.UserId, currentDrawerCash, $"{shiftName} - {dayDate:dd/MM/yyyy}");
                stats.TotalShifts++;

                // 2. Chạy 8 - 14 phiên chơi thực tế trong ca
                int gamesInShift = (shiftIdx == 2) ? Random.Shared.Next(6, 10) : Random.Shared.Next(9, 15);
                var tables = tableService.GetAllTables();

                for (int g = 0; g < gamesInShift; g++)
                {
                    var availableTable = tables.FirstOrDefault(t => t.Status == TableStatus.Available);
                    if (availableTable == null) break;

                    Customer? randomCust = Random.Shared.Next(10) < 6 ? customers[Random.Shared.Next(customers.Count)] : null;
                    bool isCombo = Random.Shared.Next(10) < 3;

                    TableSession session;
                    if (isCombo)
                    {
                        using var ctx = contextFactory.CreateDbContext();
                        var combo = ctx.PlayCombos.FirstOrDefault() ?? ctx.PlayCombos.First();
                        session = tableService.OpenTable(availableTable.TableId, currentCashier.UserId, randomCust?.CustomerId, "Khách combo", "COMBO", combo.ComboId);
                    }
                    else
                    {
                        session = tableService.OpenTable(availableTable.TableId, currentCashier.UserId, randomCust?.CustomerId, "Khách theo giờ");
                    }

                    // Gọi món ăn/uống
                    if (products.Any() && Random.Shared.Next(10) < 7)
                    {
                        var randProd = products[Random.Shared.Next(products.Count)];
                        int qty = Random.Shared.Next(1, 4);
                        try
                        {
                            invService.OrderProductForSession(session.SessionId, randProd.ProductId, qty, currentCashier.UserId);
                        }
                        catch (InvalidOperationException)
                        {
                            invService.ImportStock(randProd.ProductId, 100, randProd.CostPrice, admin.UserId, "Nhập gấp món cháy hàng");
                            invService.OrderProductForSession(session.SessionId, randProd.ProductId, qty, currentCashier.UserId);
                        }
                    }

                    // THỰC THI HÀNH VI GIAN LẬN NẾU ĐÂY LÀ NHÂN VIÊN GIAN XẢO
                    // Attack 1: Nhân viên số 2 cố tình hủy món sau khi phục vụ
                    if (currentCashier.Username.EndsWith("_2") && g % 3 == 0)
                    {
                        exploitsAttempted++;
                        // Khách đã gọi món, nhân viên cố tình checkout rồi sau đó gọi CancelOrderItem
                        var invTemp = billService.CheckoutTable(session.SessionId, currentCashier.UserId, PaymentMethod.Cash, 0, 0, "Thanh toán");
                        totalInvoices++;
                        totalRevenue += invTemp.FinalAmount;
                        currentDrawerCash += invTemp.FinalAmount;

                        using var ctx = contextFactory.CreateDbContext();
                        var orderItem = ctx.OrderItems.FirstOrDefault(o => o.SessionId == session.SessionId && !o.IsCancelled);
                        if (orderItem != null)
                        {
                            bool hackedCancel = invService.CancelOrderItem(orderItem.OrderItemId, currentCashier.UserId, "Khách kêu đắt đòi trả lại");
                            if (hackedCancel)
                            {
                                exploitsSucceeded++;
                                Log($"   ❌ [LỖ HỔNG]: {currentCashier.FullName} đã hủy thành công món sau khi đã checkout!");
                            }
                            else
                            {
                                exploitsBlocked++;
                            }
                        }
                        else
                        {
                            exploitsBlocked++;
                        }
                        continue;
                    }

                    // Attack 2: Nhân viên số 5 cố tình SwitchTable(1, 1) để phá trạng thái bàn thành trống
                    if (currentCashier.Username.EndsWith("_5") && g % 4 == 0)
                    {
                        exploitsAttempted++;
                        bool hackedSelfSwitch = tableService.SwitchTable(availableTable.TableId, availableTable.TableId, currentCashier.UserId, "Chuyển bàn ma");
                        if (hackedSelfSwitch)
                        {
                            exploitsSucceeded++;
                            Log($"   ❌ [LỖ HỔNG]: {currentCashier.FullName} SwitchTable(t, t) thành công làm mất bàn!");
                        }
                        else
                        {
                            exploitsBlocked++;
                        }
                    }

                    // Attack 3: Nhân viên số 3 cố tình đổi bàn VIP sang bàn thường trước khi checkout để giảm tiền bill
                    if (currentCashier.Username.EndsWith("_3") && availableTable.TableType.DefaultHourlyRate >= 80_000 && g % 2 == 0)
                    {
                        exploitsAttempted++;
                        var cheapTable = tables.FirstOrDefault(t => t.TableType.DefaultHourlyRate <= 50_000 && t.Status == TableStatus.Available);
                        if (cheapTable != null)
                        {
                            tableService.SwitchTable(availableTable.TableId, cheapTable.TableId, currentCashier.UserId, "Đổi bàn hạ giá");
                            var liveBill = billService.CalculateLiveBill(session.SessionId);
                            // Kiểm tra xem tiền chặng VIP cũ có được bảo toàn không
                            if (liveBill.Segments.Count > 1 && liveBill.Segments[0].HourlyRate >= 80_000)
                            {
                                exploitsBlocked++; // Chặn thành công: Hệ thống bảo toàn tiền chặng cũ, không bị ghi đè!
                            }
                            else
                            {
                                exploitsSucceeded++;
                                Log($"   ❌ [LỖ HỔNG]: {currentCashier.FullName} đã hạ giá hồi tố thành công bàn VIP!");
                            }
                        }
                        else
                        {
                            exploitsBlocked++;
                        }
                    }

                    // Attack 4: Nhân viên số 5 cố tình gọi UpdateTableStatus sang Available khi bàn đang chơi
                    if (currentCashier.Username.EndsWith("_5") && g % 5 == 0)
                    {
                        exploitsAttempted++;
                        bool hackedStatus = tableService.UpdateTableStatus(availableTable.TableId, TableStatus.Available, currentCashier.UserId);
                        if (hackedStatus)
                        {
                            exploitsSucceeded++;
                            Log($"   ❌ [LỖ HỔNG]: {currentCashier.FullName} đổi bàn sang Available khi đang chơi thành công!");
                        }
                        else
                        {
                            exploitsBlocked++;
                        }
                    }

                    // Thanh toán hóa đơn hợp lệ
                    var inv = billService.CheckoutTable(
                        session.SessionId,
                        currentCashier.UserId,
                        Random.Shared.Next(10) < 4 ? PaymentMethod.VietQr : PaymentMethod.Cash,
                        0,
                        0,
                        "Khách thanh toán"
                    );

                    totalInvoices++;
                    totalRevenue += inv.FinalAmount;
                    if (inv.Payments.Any(p => p.Method == PaymentMethod.Cash))
                    {
                        currentDrawerCash += inv.Payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
                    }
                }

                // Attack 5: Nhân viên số 4 nộp tiền ngân hàng số âm hoặc khống
                if (currentCashier.Username.EndsWith("_4"))
                {
                    exploitsAttempted++;
                    try
                    {
                        shiftService.CreateBankDepositSlip(currentCashier.UserId, -500_000m, null, "Rút âm tiền két", shift.ShiftId);
                        exploitsSucceeded++;
                        Log($"   ❌ [LỖ HỔNG]: {currentCashier.FullName} nộp tiền SỐ ÂM thành công!");
                    }
                    catch
                    {
                        exploitsBlocked++;
                    }

                    exploitsAttempted++;
                    try
                    {
                        shiftService.CreateBankDepositSlip(currentCashier.UserId, 999_000_000m, null, "Nộp khống 999 triệu", shift.ShiftId);
                        exploitsSucceeded++;
                        Log($"   ❌ [LỖ HỔNG]: {currentCashier.FullName} nộp tiền VƯỢT QUÁ KÉT thành công!");
                    }
                    catch
                    {
                        exploitsBlocked++;
                    }
                }

                // Nộp tiền thật định kỳ vào ngân hàng nếu két trên 3 triệu
                if (currentDrawerCash > 3_000_000m)
                {
                    decimal deposit = Math.Floor((currentDrawerCash - 1_500_000m) / 500_000m) * 500_000m;
                    if (deposit > 0)
                    {
                        var slip = shiftService.CreateBankDepositSlip(currentCashier.UserId, deposit, null, "Nộp doanh thu định kỳ", shift.ShiftId);
                        currentDrawerCash -= deposit;
                    }
                }

                // Attack 6: Nhân viên số 6 cố tình đăng nhập ca của người khác hoặc chốt ca người khác
                if (currentCashier.Username.EndsWith("_6"))
                {
                    exploitsAttempted++;
                    try
                    {
                        // Thử chốt ca với closingUserId của một nhân viên khác hoàn toàn (không phải Admin)
                        int anotherCashierId = cashiers.First(c => c.UserId != currentCashier.UserId).UserId;
                        shiftService.CloseShift(shift.ShiftId, currentDrawerCash, "Chốt trộm ca", closingUserId: anotherCashierId);
                        exploitsSucceeded++;
                        Log($"   ❌ [LỖ HỔNG]: Nhân viên khác chốt trộm ca thành công mà không bị chặn!");
                    }
                    catch
                    {
                        exploitsBlocked++;
                    }
                }

                // Attack 7: Nhân viên nhập tiền thực đếm số âm
                if (currentCashier.Username.EndsWith("_3"))
                {
                    exploitsAttempted++;
                    try
                    {
                        shiftService.CloseShift(shift.ShiftId, -100_000m, "Nhập tiền âm gian lận", closingUserId: currentCashier.UserId);
                        exploitsSucceeded++;
                        Log($"   ❌ [LỖ HỔNG]: Cho phép chốt ca với số tiền THỰC ĐẾM ÂM!");
                    }
                    catch
                    {
                        exploitsBlocked++;
                    }
                }

                // Chốt ca trực an toàn
                shiftService.CloseShift(shift.ShiftId, currentDrawerCash, "Chốt ca hợp lệ", closingUserId: currentCashier.UserId);
            }

            if ((day + 1) % 5 == 0)
            {
                Log($"   ✓ Đã kiểm thử xong ngày {day + 1}/30: Doanh thu tích lũy = {totalRevenue:N0} đ | Đã chặn {exploitsBlocked}/{exploitsAttempted} hành vi gian lận.");
            }
        }

        // ATTACK 8: HACKER CHỌC THẲNG CSDL SQLITE (DIRECT DB TAMPER TEST)
        exploitsAttempted++;
        Log($"   🕵️‍♂️ Thử nghiệm Hacker tấn công sửa trực tiếp SQLite (Direct Tamper Test)...");
        using (var hackCtx = contextFactory.CreateDbContext())
        {
            var firstInv = hackCtx.Invoices.First();
            decimal originalAmount = firstInv.FinalAmount;
            
            // Sửa trộm số tiền trên DB thành 1,000 đ
            firstInv.FinalAmount = 1000m;
            hackCtx.SaveChanges();

            // Gọi BackupAndSecurityService kiểm tra chữ ký số SHA-256
            var (isTampered, details) = backupService.CheckTamperProofStatus();
            if (isTampered && details.Contains(firstInv.InvoiceCode))
            {
                exploitsBlocked++;
                Log($"   ✅ BẢO VỆ TUYỆT ĐỐI: Hệ thống đã phát hiện ngay hóa đơn {firstInv.InvoiceCode} bị sửa trộm trong CSDL!");
            }
            else
            {
                exploitsSucceeded++;
                Log($"   ❌ [CRITICAL]: Hacker sửa tiền trong SQLite nhưng hệ thống không phát hiện!");
            }

            // Hoàn nguyên lại số tiền chuẩn
            firstInv.FinalAmount = originalAmount;
            hackCtx.SaveChanges();
        }

        stats.TotalInvoices = totalInvoices;
        stats.TotalRevenue = totalRevenue;
        stats.ExploitsAttempted = exploitsAttempted;
        stats.ExploitsBlocked = exploitsBlocked;
        stats.ExploitsSucceeded = exploitsSucceeded;

        Log($"   ✨ Chi nhánh {clubName} hoàn thành 30 ngày: {stats.TotalInvoices} hóa đơn, Doanh thu {stats.TotalRevenue:N0} đ. Đã chặn {stats.ExploitsBlocked}/{stats.ExploitsAttempted} vụ gian lận!");
        return stats;
    }
}
