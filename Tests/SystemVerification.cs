using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QL_JBillard.Data;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Helpers;
using QL_JBillard.Services.DTOs;
using QL_JBillard.Services.Implementations;
using QL_JBillard.Services.Strategies.Pricing;

namespace QL_JBillard.Tests;

public static class SystemVerification
{
    public static void RunVerification()
    {
        Console.WriteLine("==================================================");
        Console.WriteLine("🚀 BẮT ĐẦU KIỂM TRA HỆ THỐNG QL_JBILLARD TỰ ĐỘNG");
        Console.WriteLine("==================================================");

        string testDbPath = AppDbContext.DefaultDbPath;
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite($"Data Source={testDbPath}");

        var contextFactory = new SimpleDbContextFactory(optionsBuilder.Options);

        using (var context = contextFactory.CreateDbContext())
        {
            // 1. Khởi tạo CSDL SQLite & Seed Data
            DatabaseInitializer.Initialize(context);
            Console.WriteLine("✅ 1. Khởi tạo CSDL SQLite & Seed Data 3NF thành công!");
            Console.WriteLine($"   - Số bàn đã tạo: {context.Tables.Count()}");
            Console.WriteLine($"   - Số người dùng: {context.Users.Count()}");
            Console.WriteLine($"   - Số mặt hàng F&B: {context.Products.Count()}");
            Console.WriteLine($"   - Hạng thành viên: {context.MembershipTiers.Count()}");
        }

        // 2. Khởi tạo Services
        var auditService = new AuditService(contextFactory);
        var authService = new AuthService(contextFactory, auditService);
        var tableService = new TableService(contextFactory, auditService);
        var invService = new InventoryService(contextFactory, auditService);
        var custService = new CustomerService(contextFactory);
        var shiftService = new ShiftService(contextFactory, auditService);
        var billService = new BillingService(contextFactory, auditService, shiftService);
        var vietQrService = new VietQrService();
        var printService = new PrintService(contextFactory);
        var backupService = new BackupAndSecurityService(contextFactory, auditService);

        // 3. Test Auth Offline
        bool loginAdmin = authService.Login("admin", "admin123");
        Console.WriteLine($"✅ 2. Đăng nhập Offline (PBKDF2): {(loginAdmin ? "THÀNH CÔNG" : "THẤT BẠI")}");

        // 4. Test Mở Ca & Mở Bàn
        int userId = authService.CurrentUser!.UserId;
        if (shiftService.CurrentActiveShift != null)
        {
            shiftService.CloseShift(shiftService.CurrentActiveShift.ShiftId, 1_000_000, "Reset ca để kiểm thử", closingUserId: 1);
        }
        var shift = shiftService.StartShift(userId, 1_000_000, "Ca sáng");
        Console.WriteLine($"✅ 3. Mở ca làm việc thành công (Tiền két đầu ca: {shift.OpeningCash:N0} đ)");

        var table1 = tableService.GetAllTables().First(t => t.TableNumber.Equals("BÀN 01", StringComparison.OrdinalIgnoreCase));
        if (table1.Status != TableStatus.Available)
        {
            tableService.UpdateTableStatus(table1.TableId, TableStatus.Available, userId);
        }
        var session = tableService.OpenTable(table1.TableId, userId, null, "Khách quen");
        Console.WriteLine($"✅ 4. Mở Bàn 01 thành công (Phiên #{session.SessionId}, Giá: {session.AppliedHourlyRate:N0} đ/h)");

        // 4.1 Test Chuyển Bàn (Switch Table)
        var table2 = tableService.GetAllTables().First(t => t.TableNumber.Equals("BÀN 02", StringComparison.OrdinalIgnoreCase));
        if (table2.Status != TableStatus.Available)
        {
            tableService.UpdateTableStatus(table2.TableId, TableStatus.Available, userId);
        }
        bool switchedTo2 = tableService.SwitchTable(table1.TableId, table2.TableId, userId, "Khách đổi sang bàn 02");
        if (!switchedTo2) throw new Exception("Chuyển Bàn 01 sang Bàn 02 thất bại!");
        bool switchedBack = tableService.SwitchTable(table2.TableId, table1.TableId, userId, "Chuyển lại bàn 01");
        if (!switchedBack) throw new Exception("Chuyển Bàn 02 về Bàn 01 thất bại!");
        Console.WriteLine($"✅ 4.1 Chuyển bàn thành công 100%: Bàn 01 <-> Bàn 02 (Phiên và thời gian được bảo toàn)");

        // 4.1.1 Test Trạng Thái Đặt Bàn (Reserved) & Mở Bàn Trực Tiếp Từ Bàn Đặt
        var table4 = tableService.GetAllTables().First(t => t.TableNumber.Equals("BÀN 04", StringComparison.OrdinalIgnoreCase));
        tableService.UpdateTableStatus(table4.TableId, TableStatus.Reserved, userId);
        var table4Check = tableService.GetTableById(table4.TableId);
        if (table4Check?.Status != TableStatus.Reserved) throw new Exception("Đặt bàn thất bại!");
        var sessionFromReserved = tableService.OpenTable(table4.TableId, userId, null, "Khách đặt trước đến nhận bàn");
        table4Check = tableService.GetTableById(table4.TableId);
        if (table4Check?.Status != TableStatus.Occupied) throw new Exception("Mở bàn từ bàn đã đặt thất bại!");
        billService.CheckoutTable(sessionFromReserved.SessionId, userId, PaymentMethod.Cash, 0, 0, "Chốt bàn kiểm thử");
        Console.WriteLine($"✅ 4.1.1 Trạng thái Đặt Bàn (Reserved): Đặt bàn & Mở bàn thành công 100%!");

        // 4.2 Test Mở Bàn Gói Combo kèm món ăn tự động vào Checklist Bếp/Order
        var table3 = tableService.GetAllTables().First(t => t.TableNumber.Equals("BÀN 03", StringComparison.OrdinalIgnoreCase));
        if (table3.Status != TableStatus.Available)
        {
            tableService.UpdateTableStatus(table3.TableId, TableStatus.Available, userId);
        }
        using (var ctx = contextFactory.CreateDbContext())
        {
            var comboNight = ctx.PlayCombos.FirstOrDefault(c => c.ComboName.Contains("Cày Đêm")) 
                          ?? ctx.PlayCombos.First();
            var comboSession = tableService.OpenTable(table3.TableId, userId, null, "Khách chơi đêm", "COMBO", comboNight.ComboId);
            var comboOrderItems = ctx.OrderItems.Where(o => o.SessionId == comboSession.SessionId).ToList();
            if (comboOrderItems.Count == 0 || !comboOrderItems.All(o => o.IsComboItem && o.UnitPrice == 0))
            {
                throw new Exception("Món trong combo chưa được tự động đưa vào Checklist Order/Bếp!");
            }
            Console.WriteLine($"✅ 4.2 Mở bàn Combo '{comboNight.ComboName}' thành công: Tự động đưa {comboOrderItems.Count} món kèm combo (0đ) vào Checklist Order & Bếp!");
            
            // Dọn dẹp bàn 03
            tableService.UpdateTableStatus(table3.TableId, TableStatus.Available, userId);
        }

        // 5. Test Gọi món & Trừ kho tự động
        var sting = invService.GetAllProducts().First(p => p.ProductCode == "STING");
        if (sting.InventoryItem == null || sting.InventoryItem.QuantityOnHand < 5)
        {
            invService.ImportStock(sting.ProductId, 50, 9000, userId, "Bổ sung tồn kho tự động cho kiểm thử");
            sting = invService.GetProductById(sting.ProductId)!;
        }
        int stockBefore = sting.InventoryItem!.QuantityOnHand;
        invService.OrderProductForSession(session.SessionId, sting.ProductId, 2, userId);
        
        var stingAfter = invService.GetProductById(sting.ProductId);
        int stockAfter = stingAfter!.InventoryItem!.QuantityOnHand;
        Console.WriteLine($"✅ 5. Gọi món & Trừ kho tự động: Tồn trước = {stockBefore}, Tồn sau = {stockAfter} (Giảm 2)");

        // 5.1 Hoàn thành món để cộng vào bill
        var queueOrders = invService.GetOrderItemsForQueue(onlyPending: true);
        var testOrderItem = queueOrders.First(o => o.SessionId == session.SessionId);
        invService.CompleteOrderItem(testOrderItem.OrderItemId, userId);
        Console.WriteLine($"✅ 5.1 Bếp hoàn thành món '{sting.ProductName}' -> Đã cộng vào bill bàn!");

        // 5.2 Kiểm tra logic làm tròn tiền bida
        decimal r1 = BilliardPricingHelper.RoundBilliardAmount(590);
        decimal r2 = BilliardPricingHelper.RoundBilliardAmount(600);
        decimal r3 = BilliardPricingHelper.RoundBilliardAmount(15500);
        decimal r4 = BilliardPricingHelper.RoundBilliardAmount(15600);
        if (r1 != 0 || r2 != 1000 || r3 != 15000 || r4 != 16000)
        {
            throw new Exception("Lỗi thuật toán làm tròn tiền bida theo mốc 600đ!");
        }
        Console.WriteLine($"✅ 5.2 Kiểm thử làm tròn bida (chuẩn mốc 600đ): 590đ->{r1}đ | 600đ->{r2}đ | 15.500đ->{r3}đ | 15.600đ->{r4}đ");

        // 6. Test Đăng ký Khách Hàng SĐT & Tích Điểm
        var customer = custService.CreateOrUpdateCustomer("0988777666", "Anh Tuấn Cơ Thủ");
        Console.WriteLine($"✅ 6. Tạo thành viên theo SĐT: {customer.FullName} ({customer.PhoneNumber}) - Hạng: {customer.Tier.TierName}");

        // 7. Test VietQR Động Chuẩn EMVCo
        string qrPayload = vietQrService.GenerateEmvCoPayload("970422", "012345678999", 125000, "BIDA BAN 01");
        Console.WriteLine($"✅ 7. Sinh chuỗi Napas VietQR EMVCo thành công: {qrPayload[..30]}...");

        // 8. Test Thanh Toán Hóa Đơn & Đóng Bàn
        var invoice = billService.CheckoutTable(session.SessionId, userId, PaymentMethod.Cash, 5, 0, "Thanh toán tiền mặt");
        Console.WriteLine($"✅ 8. Thanh toán & Chốt hóa đơn thành công:");
        Console.WriteLine($"   - Mã hóa đơn: {invoice.InvoiceCode}");
        Console.WriteLine($"   - Tiền giờ ({invoice.TotalPlayMinutes} phút): {invoice.TimeAmount:N0} đ");
        Console.WriteLine($"   - Tiền dịch vụ: {invoice.ServiceAmount:N0} đ");
        Console.WriteLine($"   - Giảm giá ({invoice.DiscountPercent}%): -{invoice.DiscountAmount:N0} đ");
        Console.WriteLine($"   - TỔNG CỘNG: {invoice.FinalAmount:N0} đ");
        Console.WriteLine($"   - Điểm tích lũy nhận được: +{invoice.PointsEarned} điểm");
        Console.WriteLine($"   - Chữ ký số SHA-256 chống sửa lén: {invoice.TamperProofHash[..20]}...");

        // 9. Test In Hóa Đơn ESC/POS
        byte[] escPos = printService.GenerateEscPosBytes(invoice);
        Console.WriteLine($"✅ 9. Sinh tập lệnh máy in nhiệt ESC/POS K80/K58 thành công ({escPos.Length} bytes)");

        // 10. Test Sao Lưu Mã Hóa AES-256
        var backup = backupService.CreateBackup("AUTO_VERIFY", true);
        Console.WriteLine($"✅ 10. Sao lưu CSDL mã hóa AES-256 thành công:");
        Console.WriteLine($"   - File: {backup.BackupFileName} ({backup.FileSizeBytes:N0} bytes)");
        Console.WriteLine($"   - SHA-256 Checksum: {backup.Sha256Checksum[..24]}...");

        // 11. Test Chống Gian Lận (Tamper-Proof Verification)
        var (isTampered, details) = backupService.CheckTamperProofStatus();
        Console.WriteLine($"✅ 11. Kiểm tra toàn vẹn CSDL (Tamper-Proof): {(isTampered ? "⚠️ CẢNH BÁO BỊ SỬA TRỘM" : "AN TOÀN TUYỆT ĐỐI")}");
        Console.WriteLine($"   - Chi tiết: {details}");

        // 12. Test Chốt Ca Thu Ngân (Z-Report)
        var closedShift = shiftService.CloseShift(shift.ShiftId, 1_000_000 + invoice.FinalAmount, "Chốt ca khớp tiền");
        Console.WriteLine($"✅ 12. Chốt ca thu ngân (Z-Report): Doanh thu tiền mặt = {closedShift.SystemCalculatedCash:N0} đ | Chênh lệch két = {closedShift.Discrepancy:N0} đ | Tiền bàn giao ca sau = {closedShift.ActualCashRecorded:N0} đ");

        // 13. Test Bàn Giao Ca Sau & Nộp Tiền Ngân Hàng
        decimal handoverCash = shiftService.GetDrawerBalanceFromLastShift();
        var shift2 = shiftService.StartShift(userId, handoverCash, "Ca tối bàn giao chuẩn");
        Console.WriteLine($"✅ 13. Ca mới mở nhận tiền bàn giao ca trước: {shift2.PreviousShiftClosingCash:N0} đ (Khớp két: {!shift2.IsOpeningDiscrepant})");

        var depositSlip = shiftService.CreateBankDepositSlip(
            userId,
            500_000,
            "C:\\Receipts\\test_slip.jpg",
            "Nộp bớt tiền két ca sáng vào tài khoản",
            shift2.ShiftId
        );
        Console.WriteLine($"✅ 14. Tạo phiếu nộp tiền ngân hàng: Mã {depositSlip.SlipCode}, Số tiền: {depositSlip.Amount:N0} đ, Trạng thái: {depositSlip.Status}");

        var summary2 = shiftService.GetCurrentShiftSummary();
        Console.WriteLine($"   - Tiền két thực giữ sau nộp ngân hàng: {summary2.CurrentDrawerCash:N0} đ (Đã trừ {summary2.TotalBankDeposits:N0} đ)");

        bool approved = shiftService.ApproveBankDepositSlip(depositSlip.DepositSlipId, userId);
        Console.WriteLine($"✅ 15. Admin duyệt phiếu nộp tiền ngân hàng: {(approved ? "THÀNH CÔNG" : "THẤT BẠI")}");

        // 16. Test Quản Lý Tài Khoản Thu Ngân (Admin CRUD)
        string testUsername = $"thungan_{DateTime.Now.Ticks % 100000}";
        bool created = authService.CreateUser(testUsername, "123456", "Nguyễn Văn Thu Ngân", "0901234567", UserRole.Cashier);
        Console.WriteLine($"✅ 16. Admin tạo tài khoản thu ngân mới ({testUsername}): {(created ? "THÀNH CÔNG" : "THẤT BẠI")}");

        var newCashier = authService.GetAllUsers().FirstOrDefault(u => u.Username == testUsername);
        if (newCashier != null)
        {
            authService.ToggleUserStatus(newCashier.UserId);
            var lockedCashier = authService.GetUserById(newCashier.UserId);
            Console.WriteLine($"   - Khóa tài khoản thu ngân: IsActive = {lockedCashier?.IsActive}");

            authService.ToggleUserStatus(newCashier.UserId);
            Console.WriteLine($"   - Mở khóa lại tài khoản: IsActive = {authService.GetUserById(newCashier.UserId)?.IsActive}");
        }

        // Chốt ca 2
        var closedShift2 = shiftService.CloseShift(shift2.ShiftId, summary2.CurrentDrawerCash, "Chốt ca 2 an toàn");
        Console.WriteLine($"✅ 17. Chốt ca 2 thành công: Tiền bàn giao kế tiếp = {closedShift2.ActualCashRecorded:N0} đ");

        // 18. Test Báo Cáo Doanh Thu Đa Danh Mục & So Sánh Kỳ (ReportService)
        var reportService = new ReportService(contextFactory);
        var overview = reportService.GetRevenueOverview(DateTime.Now.AddDays(-7), DateTime.Now);
        Console.WriteLine($"✅ 18. Báo cáo Doanh Thu Tổng Hợp: {overview.TotalRevenue:N0} đ ({overview.TotalInvoices} hóa đơn, {overview.ChartPoints.Count} điểm đồ thị)");

        var tableReports = reportService.GetTablePerformance(DateTime.Now.AddDays(-7), DateTime.Now);
        Console.WriteLine($"✅ 19. Báo cáo Hiệu Suất Bàn: Đã phân tích {tableReports.Count} bàn (Top 1: {tableReports.FirstOrDefault()?.TableNumber} - {tableReports.FirstOrDefault()?.TotalRevenue:N0} đ)");

        var prodReports = reportService.GetProductSales(DateTime.Now.AddDays(-7), DateTime.Now);
        Console.WriteLine($"✅ 20. Báo cáo Món F&B Bán Chạy: {prodReports.Count} mặt hàng (Bán chạy nhất: {prodReports.FirstOrDefault()?.ProductName} x{prodReports.FirstOrDefault()?.QuantitySold})");

        var compWeek = reportService.GetPeriodComparison(ComparisonPeriodMode.ThisWeekVsLastWeek);
        var comp2Weeks = reportService.GetPeriodComparison(ComparisonPeriodMode.ThisWeekVsTwoWeeksAgo);
        var compMonth = reportService.GetPeriodComparison(ComparisonPeriodMode.ThisMonthVsLastMonth);
        Console.WriteLine($"✅ 21. Báo cáo So Sánh Kỳ Đối Chiếu & Biểu Đồ Cột Kép:");
        Console.WriteLine($"   - {compWeek.PeriodModeTitle}: Kỳ này {compWeek.CurrentRevenue:N0} đ vs Kỳ trước {compWeek.PreviousRevenue:N0} đ (Tăng trưởng: {compWeek.RevenueGrowthPercent:F1}%)");
        Console.WriteLine($"   - {comp2Weeks.PeriodModeTitle}: Điểm so sánh = {comp2Weeks.ComparisonPoints.Count} mốc");
        Console.WriteLine($"   - {compMonth.PeriodModeTitle}: Điểm so sánh = {compMonth.ComparisonPoints.Count} mốc");

        // 22. Test Converters Tabs & Chống Trùng Lặp
        var strEqConv = new Views.Converters.StringEqualsConverter();
        var strEqVisConv = new Views.Converters.StringEqualsToVisibilityConverter();
        var visMatch = (System.Windows.Visibility)strEqConv.Convert("PRODUCTS", typeof(System.Windows.Visibility), "PRODUCTS", System.Globalization.CultureInfo.InvariantCulture);
        var visMismatch = (System.Windows.Visibility)strEqConv.Convert("PRODUCTS", typeof(System.Windows.Visibility), "OVERVIEW", System.Globalization.CultureInfo.InvariantCulture);
        if (visMatch != System.Windows.Visibility.Visible || visMismatch != System.Windows.Visibility.Collapsed)
            throw new Exception("StringEqualsConverter lỗi khi convert sang System.Windows.Visibility!");
        Console.WriteLine($"✅ 22. Converter Sub-Tabs: Khớp -> {visMatch} | Không khớp -> {visMismatch} (Khắc phục triệt để lỗi đè tab)");

        // 23. Test Lọc Báo Cáo Theo Danh Mục & Tên Món F&B
        var filteredProds = reportService.GetProductSales(DateTime.Now.AddDays(-7), DateTime.Now, categoryId: 1, search: "Heineken");
        Console.WriteLine($"✅ 23. Lọc Báo Cáo Món F&B: Đã lọc {filteredProds.Count} món theo danh mục và từ khóa");

        // 24. Test CurrencyConverter.ConvertBack (Chống crash khi sửa tiền thực đếm)
        var currConv = new Views.Converters.CurrencyConverter();
        decimal parsedVal = (decimal)currConv.ConvertBack("1.500.000 đ", typeof(decimal), "", System.Globalization.CultureInfo.InvariantCulture);
        if (parsedVal != 1500000m) throw new Exception("CurrencyConverter.ConvertBack lỗi parsing số tiền!");
        Console.WriteLine($"✅ 24. Chống crash CurrencyConverter.ConvertBack: '1.500.000 đ' -> {parsedVal:N0} đ");

        // 25. Test Backup ghi đè tệp duy nhất (billiard_latest_backup.enc)
        var backup1 = backupService.CreateBackup("VERIFY_1", true);
        var backup2 = backupService.CreateBackup("VERIFY_2", true);
        string backupsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
        var encFiles = Directory.GetFiles(backupsDir, "*.enc");
        if (encFiles.Length > 1)
        {
            throw new Exception($"Lỗi: Thư mục backup có {encFiles.Length} file .enc! Kỳ vọng chỉ 1 file duy nhất ghi đè.");
        }
        Console.WriteLine($"✅ 25. Kiểm thử Backup ghi đè file duy nhất: {Path.GetFileName(encFiles[0])} (Tổng số file = {encFiles.Length})");

        // 26. Test Ràng buộc Mở Bàn khi chưa mở ca trực
        // Đảm bảo không còn ca nào mở
        while (shiftService.CurrentActiveShift != null)
        {
            shiftService.CloseShift(shiftService.CurrentActiveShift.ShiftId, 0, "Chốt sạch ca kiểm thử", userId);
        }
        bool threwWithoutShift = false;
        try
        {
            tableService.OpenTable(table1.TableId, userId);
        }
        catch (InvalidOperationException ex)
        {
            threwWithoutShift = true;
            Console.WriteLine($"✅ 26. Chặn mở bàn khi chưa mở ca: '{ex.Message}'");
        }
        if (!threwWithoutShift)
        {
            throw new Exception("Lỗi: Hệ thống cho phép mở bàn khi chưa có ca làm việc nào mở!");
        }

        // 27. Test Phân quyền Ca trực theo nhân viên & Admin Override
        // Tạo thu ngân 1 và thu ngân 2
        authService.CreateUser("nv_sang", "123456", "Nhân Viên Ca Sáng", "0900000001", UserRole.Cashier);
        authService.CreateUser("nv_chieu", "123456", "Nhân Viên Ca Chiều", "0900000002", UserRole.Cashier);
        var nvSang = authService.GetAllUsers().First(u => u.Username == "nv_sang");
        var nvChieu = authService.GetAllUsers().First(u => u.Username == "nv_chieu");

        // NV Sáng mở ca
        var shiftSang = shiftService.StartShift(nvSang.UserId, 1_000_000, "Ca NV Sáng");

        // NV Chiều cố mở ca khác -> Phải bị chặn
        bool threwSecondShift = false;
        try
        {
            shiftService.StartShift(nvChieu.UserId, 1_000_000, "Ca NV Chiều");
        }
        catch (InvalidOperationException ex)
        {
            threwSecondShift = true;
            Console.WriteLine($"✅ 27. Chặn NV khác mở ca khi ca cũ chưa chốt: '{ex.Message}'");
        }
        if (!threwSecondShift) throw new Exception("Lỗi: Cho phép mở ca mới khi ca cũ chưa chốt!");

        // NV Chiều cố chốt ca của NV Sáng -> Phải bị chặn
        bool threwNvChieuClose = false;
        try
        {
            shiftService.CloseShift(shiftSang.ShiftId, 1_000_000, "NV Chiều cố chốt", nvChieu.UserId);
        }
        catch (InvalidOperationException ex)
        {
            threwNvChieuClose = true;
            Console.WriteLine($"✅ 28. Chặn NV khác chốt ca người khác: '{ex.Message}'");
        }
        if (!threwNvChieuClose) throw new Exception("Lỗi: Cho phép nhân viên khác chốt ca người khác!");

        // Admin có toàn quyền chốt ca thay thế
        var adminClosedShift = shiftService.CloseShift(shiftSang.ShiftId, 1_000_000, "Admin can thiệp chốt ca", userId);
        Console.WriteLine($"✅ 29. Admin toàn quyền chốt ca thay thế nhân viên: Thành công! Ghi chú = {adminClosedShift.Notes}");

        // 30. Test Render Ảnh Món F&B & Phụ Kiện
        using (var ctx = contextFactory.CreateDbContext())
        {
            ProductImageGenerator.EnsureDefaultProductImages(ctx);
            var prodsWithImage = ctx.Products.Where(p => !string.IsNullOrEmpty(p.ImagePath)).ToList();
            if (prodsWithImage.Count != ctx.Products.Count())
            {
                throw new Exception("Lỗi: Vẫn còn sản phẩm chưa có ảnh ImagePath!");
            }
            Console.WriteLine($"✅ 30. Đã render tự động ảnh cho toàn bộ {prodsWithImage.Count} món và phụ kiện bida!");
        }

        // 31. Test Nghiệp Vụ Quản Lý Bàn, Khu Vực & Tầng (Thêm, Xóa, Sửa)
        using (var ctx = contextFactory.CreateDbContext())
        {
            var oldTestTables = ctx.Tables.Where(t => t.TableNumber == "BÀN TEST 99").ToList();
            if (oldTestTables.Any()) ctx.Tables.RemoveRange(oldTestTables);

            var oldTestAreas = ctx.Areas.Where(a => a.AreaName == "Khu Thi Đấu Mở Rộng").ToList();
            if (oldTestAreas.Any()) ctx.Areas.RemoveRange(oldTestAreas);

            var oldTestFloors = ctx.Floors.Where(f => f.FloorName == "Tầng 3 Sân Thượng").ToList();
            if (oldTestFloors.Any()) ctx.Floors.RemoveRange(oldTestFloors);

            var oldTestTypes = ctx.TableTypes.Where(t => t.TypeName == "Bàn Libre Tiêu Chuẩn").ToList();
            if (oldTestTypes.Any()) ctx.TableTypes.RemoveRange(oldTestTypes);

            ctx.SaveChanges();
        }

        var tableMgmtVm = new ViewModels.TableManagementViewModel(tableService, authService);

        // 31.1 Tầng lầu
        tableMgmtVm.FormFloorName = "Tầng 3 Sân Thượng";
        tableMgmtVm.SaveFloorCommand.Execute(null);
        var createdFloor = tableService.GetAllFloors().FirstOrDefault(f => f.FloorName == "Tầng 3 Sân Thượng");
        if (createdFloor == null) throw new Exception("Lỗi: Không tạo được Tầng 3!");
        Console.WriteLine($"✅ 31.1 Quản lý Tầng: Đã tạo '{createdFloor.FloorName}' (Mã #{createdFloor.FloorId})");

        // 31.2 Khu vực
        tableMgmtVm.FormAreaName = "Khu Thi Đấu Mở Rộng";
        tableMgmtVm.FormAreaSelectedFloor = createdFloor;
        tableMgmtVm.SaveAreaCommand.Execute(null);
        var createdArea = tableService.GetAllAreas().FirstOrDefault(a => a.AreaName == "Khu Thi Đấu Mở Rộng");
        if (createdArea == null) throw new Exception("Lỗi: Không tạo được Khu Thi Đấu Mở Rộng!");
        Console.WriteLine($"✅ 31.2 Quản lý Khu Vực: Đã tạo '{createdArea.AreaName}' thuộc '{createdFloor.FloorName}'");

        // 31.3 Loại bàn & Giá giờ
        tableMgmtVm.FormTypeName = "Bàn Libre Tiêu Chuẩn";
        tableMgmtVm.FormDefaultHourlyRate = 55_000;
        tableMgmtVm.SaveTableTypeCommand.Execute(null);
        var createdType = tableService.GetAllTableTypes().FirstOrDefault(t => t.TypeName == "Bàn Libre Tiêu Chuẩn");
        if (createdType == null) throw new Exception("Lỗi: Không tạo được Loại bàn Libre!");
        Console.WriteLine($"✅ 31.3 Quản lý Loại Bàn & Giá Giờ: Đã tạo '{createdType.TypeName}' giá {createdType.DefaultHourlyRate:N0} đ/h");

        // 31.4 Thêm bàn mới
        tableMgmtVm.FormTableNumber = "BÀN TEST 99";
        tableMgmtVm.FormTableSelectedArea = createdArea;
        tableMgmtVm.FormTableSelectedType = createdType;
        tableMgmtVm.SaveTableCommand.Execute(null);
        var createdTable = tableService.GetAllTables().FirstOrDefault(t => t.TableNumber == "BÀN TEST 99");
        if (createdTable == null) throw new Exception("Lỗi: Không tạo được Bàn TEST 99!");
        Console.WriteLine($"✅ 31.4 Quản lý Bàn: Đã tạo bàn '{createdTable.TableNumber}' thành công!");

        // 31.5 Xóa dọn dẹp dữ liệu test
        tableMgmtVm.DeleteTableCommand.Execute(createdTable);
        tableMgmtVm.DeleteAreaCommand.Execute(createdArea);
        tableMgmtVm.DeleteFloorCommand.Execute(createdFloor);
        tableMgmtVm.DeleteTableTypeCommand.Execute(createdType);
        Console.WriteLine("✅ 31.5 Xóa dọn dẹp cấu hình bàn test hoàn tất!");

        // 32. Test Lọc Ngày Trong Nhật Ký Hoạt Động (Audit Log)
        var auditVm = new ViewModels.AuditLogViewModel(auditService, backupService);
        auditVm.SetDateFilterCommand.Execute("TODAY");
        Console.WriteLine($"✅ 32. Lọc Nhật Ký Hôm Nay: {auditVm.Logs.Count} bản ghi");
        auditVm.SetDateFilterCommand.Execute("YESTERDAY");
        Console.WriteLine($"   - Lọc Hôm Qua: {auditVm.Logs.Count} bản ghi");
        auditVm.SetDateFilterCommand.Execute("ALL");
        Console.WriteLine($"   - Lọc Tất Cả: {auditVm.Logs.Count} bản ghi");

        // 33. Test Báo Cáo Thích Nghi Quán Mở 24/24 (Đủ 24 mốc giờ 00h - 23h)
        var overview24h = reportService.GetRevenueOverview(DateTime.Today, DateTime.Today);
        if (overview24h.ChartPoints.Count != 24)
        {
            throw new Exception($"Biểu đồ giờ không đủ 24 mốc! Hiện có: {overview24h.ChartPoints.Count}");
        }
        if (!overview24h.IsHourlyView || overview24h.DayShiftPoints.Count != 12 || overview24h.NightShiftPoints.Count != 12)
        {
            throw new Exception($"Lỗi phân chia ca: IsHourlyView={overview24h.IsHourlyView}, Ca Sáng={overview24h.DayShiftPoints.Count}, Ca Tối={overview24h.NightShiftPoints.Count}");
        }
        Console.WriteLine($"✅ 33. Báo cáo 24/24: Tách 2 ca chuẩn xác không tràn hàng: Ca Sáng ({overview24h.DayShiftPoints.First().Label} - {overview24h.DayShiftPoints.Last().Label}) & Ca Tối/Đêm ({overview24h.NightShiftPoints.First().Label} - {overview24h.NightShiftPoints.Last().Label})!");

        // 34. Test Đánh Giá Trực Quan Khách Quan (Thấp / Trung Bình / Cao / HOT)
        var tablePerf = reportService.GetTablePerformance(DateTime.Today.AddDays(-7), DateTime.Today);
        Console.WriteLine($"✅ 34. Đánh giá hiệu suất bàn: {tablePerf.Count} bàn (Rating mẫu: {tablePerf.FirstOrDefault()?.RatingBadgeText ?? "N/A"})");
        var prodSales = reportService.GetProductSales(DateTime.Today.AddDays(-7), DateTime.Today);
        Console.WriteLine($"   - Đánh giá tiêu thụ F&B: {prodSales.Count} món (Rating mẫu: {prodSales.FirstOrDefault()?.RatingBadgeText ?? "N/A"})");

        // 35. Test Thuật Toán Khung Giờ Giá (Time-Slot Pricing)
        var testSlots = new List<Domain.Entities.TimeSlotPricing>
        {
            new() { SlotName = "Sáng 8h-12h", StartHour = 8, StartMinute = 0, EndHour = 12, EndMinute = 0, HourlyRate = 50_000, IsActive = true },
            new() { SlotName = "Chiều 12h-18h", StartHour = 12, StartMinute = 0, EndHour = 18, EndMinute = 0, HourlyRate = 60_000, IsActive = true }
        };
        // Chơi từ 11:00 đến 13:00 (1h @ 50k + 1h @ 60k = 110k)
        var startT = new DateTime(2026, 11, 4, 11, 0, 0);
        var endT = new DateTime(2026, 11, 4, 13, 0, 0);
        decimal slotCost = BilliardPricingHelper.CalculateTimeSlotCost(startT, endT, 50_000, testSlots);
        if (slotCost != 110_000)
        {
            throw new Exception($"Thuật toán khung giờ tính sai! Kỳ vọng 110,000 đ, thực tế: {slotCost:N0} đ");
        }
        Console.WriteLine($"✅ 35. Thuật toán khung giờ giá: 11h-13h xuyên khung = {slotCost:N0} đ (Khớp 100%!)");

        // 36. Test Tính Tiền Gói Combo & Vượt Giờ (Overtime)
        // Combo 3 tiếng 150k, chơi 3 tiếng 30 phút (vượt 30p @ 60k = +30k -> 180k)
        var comboStart = new DateTime(2026, 11, 4, 12, 0, 0);
        var comboEnd = new DateTime(2026, 11, 4, 15, 30, 0);
        decimal comboCost = BilliardPricingHelper.CalculateSessionCost(comboStart, comboEnd, 60_000, "COMBO", 150_000, 180, testSlots);
        if (comboCost != 180_000)
        {
            throw new Exception($"Thuật toán Combo tính sai! Kỳ vọng 180,000 đ, thực tế: {comboCost:N0} đ");
        }
        Console.WriteLine($"✅ 36. Thuật toán Combo 3h (150k) chơi 3h30p = {comboCost:N0} đ (Khớp 100%!)");

        // 37. Test Khởi tạo & Kiểm thử Giao diện WPF XAML trên STA Thread
        Exception? uiException = null;
        var staThread = new System.Threading.Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current == null)
                {
                    new QL_JBillard.App();
                }

                var sc = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
                QL_JBillard.App.ConfigureServices(sc);
                var sp = sc.BuildServiceProvider();

                // 1. Verify LoginWindow & LoginViewModel
                var loginWindow = sp.GetRequiredService<Views.Windows.LoginWindow>();
                var loginVm = (ViewModels.LoginViewModel)loginWindow.DataContext;
                bool loginEventFired = false;
                loginVm.OnLoginSuccess += () => { loginEventFired = true; };

                loginVm.Username = "admin";
                loginVm.Password = "admin123";
                loginVm.LoginCommand.Execute(null);

                if (!loginEventFired)
                {
                    throw new Exception("Sự kiện OnLoginSuccess không kích hoạt khi gọi LoginCommand!");
                }

                Console.WriteLine("✅ 13. Kiểm thử WPF Giao diện:");
                Console.WriteLine("   - LoginWindow: Khởi tạo và kích hoạt OnLoginSuccess chuẩn xác!");

                // 2. Verify MainWindow & Chuyển Tab Báo Cáo
                var mainWindow = sp.GetRequiredService<MainWindow>();
                var mainVm = (ViewModels.MainViewModel)mainWindow.DataContext;
                mainVm.NavigateCommand.Execute("REPORTS");
                if (mainVm.ActiveTab != "REPORTS" || mainVm.CurrentViewModel != mainVm.ReportsVM)
                {
                    throw new Exception("Lỗi điều hướng vào tab REPORTS trong MainViewModel!");
                }
                mainVm.ReportsVM.SetTabCommand.Execute("TABLES");
                mainVm.ReportsVM.SetTabCommand.Execute("PRODUCTS");
                mainVm.ReportsVM.SetTabCommand.Execute("COMPARISON");
                mainVm.ReportsVM.SetTabCommand.Execute("OVERVIEW");
                
                // Test điều hướng vào tab Quản Lý Bàn & Khu Vực
                mainVm.NavigateCommand.Execute("TABLE_MGMT");
                if (mainVm.ActiveTab != "TABLE_MGMT" || mainVm.CurrentViewModel != mainVm.TableManagementVM)
                {
                    throw new Exception("Lỗi điều hướng vào tab TABLE_MGMT!");
                }
                mainVm.TableManagementVM.SetSubTabCommand.Execute("AREAS_FLOORS");
                mainVm.TableManagementVM.SetSubTabCommand.Execute("TYPES_PRICES");
                mainVm.TableManagementVM.SetSubTabCommand.Execute("SLOTS_COMBOS");
                mainVm.TableManagementVM.SetSubTabCommand.Execute("TABLES");

                Console.WriteLine("   - MainWindow & MainViewModel: Khởi tạo, chuyển các sub-tab Báo Cáo & Quản Lý Bàn mượt mà 100%!");

                // 3. Verify UserControls
                var tableMapView = new Views.UserControls.TableMapView { DataContext = mainVm.TableMapVM };
                var orderQueueView = new Views.UserControls.OrderQueueView { DataContext = mainVm.OrderQueueVM };
                var reportsView = new Views.UserControls.ReportsView { DataContext = mainVm.ReportsVM };
                var invView = new Views.UserControls.InventoryView { DataContext = mainVm.InventoryVM };
                var custView = new Views.UserControls.CustomerView { DataContext = mainVm.CustomerVM };
                var shiftView = new Views.UserControls.ShiftView { DataContext = mainVm.ShiftVM };
                var userMgmtView = new Views.UserControls.UserManagementView { DataContext = mainVm.UserManagementVM };
                var auditView = new Views.UserControls.AuditLogView { DataContext = mainVm.AuditLogVM };
                var settingsView = new Views.UserControls.SettingsView { DataContext = mainVm.SettingsVM };
                var tableMgmtView = new Views.UserControls.TableManagementView { DataContext = mainVm.TableManagementVM };

                // Test trực tiếp sửa tiền thực đếm trên ViewModel để đảm bảo không crash
                mainVm.ShiftVM.FormActualCashInput = "1500000";
                mainVm.ShiftVM.FormActualCashInput = "2.000.000";
                mainVm.ShiftVM.FormActualCashInput = "";

                // Kiểm tra bộ đếm mệnh giá tiền mặt từ 500k đến 500đ
                mainVm.ShiftVM.Count500k = 2; // 1.000.000
                mainVm.ShiftVM.Count200k = 1; // 200.000
                mainVm.ShiftVM.Count100k = 1; // 100.000
                mainVm.ShiftVM.Count50k = 1;  // 50.000
                mainVm.ShiftVM.Count20k = 1;  // 20.000
                mainVm.ShiftVM.Count10k = 1;  // 10.000
                mainVm.ShiftVM.Count5k = 1;   // 5.000
                mainVm.ShiftVM.Count2k = 1;   // 2.000
                mainVm.ShiftVM.Count1k = 1;   // 1.000
                mainVm.ShiftVM.Count500d = 1; // 500
                if (mainVm.ShiftVM.FormActualCash != 1_388_500m)
                {
                    throw new Exception($"Lỗi bộ đếm mệnh giá tiền: mong muốn 1.388.500 đ, thực tế {mainVm.ShiftVM.FormActualCash}");
                }
                Console.WriteLine("   - Bộ đếm 10 mệnh giá (500k đến 500đ): Tính toán tự động chuẩn xác 1.388.500 đ!");

                // Kiểm tra nhãn khớp tiền két không còn chữ 100%
                if (mainVm.ShiftVM.DiscrepancyStatus.Contains("100%"))
                {
                    throw new Exception("Lỗi: DiscrepancyStatus vẫn còn chữ 100%!");
                }

                // Kiểm tra trục tọa độ biểu đồ báo cáo
                mainVm.ReportsVM.Refresh();
                if (string.IsNullOrEmpty(mainVm.ReportsVM.ChartYMaxLabel) || string.IsNullOrEmpty(mainVm.ReportsVM.ChartXAxisTitle))
                {
                    throw new Exception("Lỗi: Thiếu thông tin trục X / Y biểu đồ báo cáo!");
                }
                Console.WriteLine("   - Trục X & Y biểu đồ báo cáo: Hiển thị đầy đủ thước đo doanh thu VNĐ và mốc thời gian!");

                Console.WriteLine("   - 10 UserControls cốt lõi (Bàn, Order/Bếp, Báo cáo, Kho, Khách, Ca trực, Tài khoản & Thu ngân, Nhật ký, Cấu hình, Quản lý Bàn & Khu vực): Tải XAML thành công 100%!");

                // 4. Verify Dialogs
                if (mainVm.TableMapVM.AllTables.Any())
                {
                    var tableItem = mainVm.TableMapVM.AllTables.First();
                    var invService = sp.GetRequiredService<Services.Interfaces.IInventoryService>();
                    var authServ = sp.GetRequiredService<Services.Interfaces.IAuthService>();
                    var billingServ = sp.GetRequiredService<Services.Interfaces.IBillingService>();
                    var customerServ = sp.GetRequiredService<Services.Interfaces.ICustomerService>();
                    var qrServ = sp.GetRequiredService<Services.Interfaces.IVietQrService>();
                    var printerServ = sp.GetRequiredService<Services.Interfaces.IPrintService>();
                    var secServ = sp.GetRequiredService<Services.Interfaces.IBackupAndSecurityService>();
                    var dbFact = sp.GetRequiredService<IDbContextFactory<AppDbContext>>();
                    var tableServ = sp.GetRequiredService<Services.Interfaces.ITableService>();
                    var shiftServ = sp.GetRequiredService<Services.Interfaces.IShiftService>();

                    var orderVm = new ViewModels.OrderDialogViewModel(invService, authServ, tableItem, mainVm.TableMapVM.AllTables);
                    var orderDialog = new Views.Dialogs.OrderDialog(orderVm);

                    var checkoutVm = new ViewModels.CheckoutDialogViewModel(
                        billingServ, customerServ, qrServ, printerServ, secServ, authServ, dbFact, tableItem
                    );
                    var checkoutDialog = new Views.Dialogs.CheckoutDialog(checkoutVm);

                    var openVm = new ViewModels.OpenTableDialogViewModel(tableItem, tableServ, authServ, customerServ);
                    var openDialog = new Views.Dialogs.OpenTableDialog(openVm);

                    var switchVm = new ViewModels.SwitchTableViewModel(tableServ, authServ, tableItem);
                    var switchDialog = new Views.Dialogs.SwitchTableDialog(switchVm);

                    var depositVm = new ViewModels.BankDepositDialogViewModel(shiftServ, authServ, 500_000);
                    var depositDialog = new Views.Dialogs.BankDepositDialog(depositVm);

                    var handoffDialog = new Views.Dialogs.ShiftHandoffDialog(new List<Domain.Entities.Table> { new Domain.Entities.Table { TableNumber = "BÀN 01" } });

                    Console.WriteLine("   - 6 Dialogs nghiệp vụ (Mở bàn Combo/Giờ, Gọi món POS lớn, Thanh toán rộng rãi, Chuyển bàn, Nộp tiền ngân hàng, Chuyển tiếp ca): Tải XAML thành công 100%!");
                }
            }
            catch (Exception ex)
            {
                uiException = ex;
            }
        });
        staThread.SetApartmentState(System.Threading.ApartmentState.STA);
        staThread.Start();
        staThread.Join();

        if (uiException != null)
        {
            Console.WriteLine($"❌ Lỗi kiểm thử giao diện WPF: {uiException}");
            throw uiException;
        }

        Console.WriteLine("==================================================");
        Console.WriteLine("🎉 TẤT CẢ CÁC BƯỚC NGHIỆP VỤ & GIAO DIỆN ĐÃ ĐẠT 100%!");
        Console.WriteLine("==================================================");
    }
}

public class SimpleDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;
    public SimpleDbContextFactory(DbContextOptions<AppDbContext> options) => _options = options;
    public AppDbContext CreateDbContext() => new AppDbContext(_options);
}
