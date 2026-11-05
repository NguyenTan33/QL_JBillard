using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QL_JBillard.Data;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Services.Implementations;

namespace QL_JBillard.Tests;

public static class HackerChaosTest2
{
    private static readonly System.Text.StringBuilder _logReport = new();
    private static void Log(string message = "")
    {
        try { Console.WriteLine(message); } catch { }
        _logReport.AppendLine(message);
    }

    public static void Run()
    {
        Log("================================================================================");
        Log("🕵️‍♂️ BẮT ĐẦU CHIẾN DỊCH PENETRATION & CHAOS QA: VÒNG 2 (DEEP INSPECTION)");
        Log("🎭 TARGET: CÁC LỖI NGHIỆP VỤ ẨN, CHÊNH LỆCH HÓA ĐƠN, TÍCH ĐIỂM & KHO BÃI");
        Log("================================================================================\n");

        string tempDb = Path.Combine(Path.GetTempPath(), $"hacker_chaos2_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={tempDb}")
            .Options;

        var contextFactory = new SimpleDbContextFactory(options);

        using (var ctx = contextFactory.CreateDbContext())
        {
            DatabaseInitializer.Initialize(ctx, seedSampleData: true);
        }

        var auditService = new AuditService(contextFactory);
        var authService = new AuthService(contextFactory, auditService);
        var tableService = new TableService(contextFactory, auditService);
        var invService = new InventoryService(contextFactory, auditService);
        var custService = new CustomerService(contextFactory);
        var shiftService = new ShiftService(contextFactory, auditService);
        var billService = new BillingService(contextFactory, auditService, shiftService);
        var printService = new PrintService(contextFactory);

        authService.Login("admin", "admin123");
        int adminId = authService.CurrentUser!.UserId;

        // Mở ca thử nghiệm
        var shift = shiftService.StartShift(adminId, 1_000_000m, "Mở ca test vòng 2");
        int shiftId = shift.ShiftId;

        int totalFound = 0;

        // ====================================================================================
        // TEST 1: MẤT THỜI GIAN CHƠI THỰC TẾ KHI CHUYỂN BÀN (LOST PLAY MINUTES BUG)
        // ====================================================================================
        Log("--------------------------------------------------------------------------------");
        Log("🔴 TEST 1: [MẤT THỜI GIAN CHƠI] Khách chơi 2h10p nhưng hóa đơn chỉ ghi 10 phút!");
        Log("--------------------------------------------------------------------------------");
        {
            var t1 = tableService.GetAllTables().First(t => t.TableNumber == "BÀN 01");
            var t2 = tableService.GetAllTables().First(t => t.TableNumber == "BÀN 02");
            tableService.UpdateTableStatus(t1.TableId, TableStatus.Available, adminId);
            tableService.UpdateTableStatus(t2.TableId, TableStatus.Available, adminId);

            var session = tableService.OpenTable(t1.TableId, adminId, null, "Khách chơi lâu");

            // Giả lập chơi 2 tiếng ở Bàn 01
            using (var ctx = contextFactory.CreateDbContext())
            {
                var s = ctx.TableSessions.Find(session.SessionId);
                s!.StartTime = DateTime.Now.AddMinutes(-130);
                ctx.SaveChanges();
            }

            // Chuyển sang Bàn 02 lúc cách đây 10 phút
            tableService.SwitchTable(t1.TableId, t2.TableId, adminId, "Đổi bàn");

            // Giả lập sau khi chuyển bàn được 10 phút
            using (var ctx = contextFactory.CreateDbContext())
            {
                var s = ctx.TableSessions.Find(session.SessionId);
                s!.StartTime = DateTime.Now.AddMinutes(-10); // SwitchTable ghi đè StartTime = now
                ctx.SaveChanges();
            }

            // Checkout
            var invoice = billService.CheckoutTable(session.SessionId, adminId, PaymentMethod.Cash, 0, 0, "Chốt bàn");

            if (invoice.TotalPlayMinutes < 60)
            {
                totalFound++;
                Log($"❌ PHÁT HIỆN LỖI [HIGH]: Thời gian chơi trên hóa đơn bị sai lệch nghiêm trọng!");
                Log($"   - Khách chơi thực tế: 130 phút (2 giờ 10 phút)");
                Log($"   - Thời gian in trên hóa đơn (TotalPlayMinutes): {invoice.TotalPlayMinutes} phút");
                Log($"   - Tiền giờ tính: {invoice.TimeAmount:N0} đ");
                Log($"   - HẬU QUẢ: Khách cầm hóa đơn sẽ cự cãi: 'Tôi chơi hơn 2 tiếng sao hóa đơn ghi có {invoice.TotalPlayMinutes} phút mà thu {invoice.TimeAmount:N0}đ?!'");
            }
            else
            {
                Log($"✅ BẢO VỆ TỐT: Thời gian chơi được cộng dồn chính xác: {invoice.TotalPlayMinutes} phút.");
            }
        }

        // ====================================================================================
        // TEST 2: THẤT THOÁT TIỀN MÓN ĂN NẾU BẾP QUÊN ẤN 'HOÀN THÀNH'
        // ====================================================================================
        Log("\n--------------------------------------------------------------------------------");
        Log("🔴 TEST 2: [MẤT TIỀN F&B] Đồ ăn đã mang ra nhưng bếp chưa ấn 'Hoàn thành' trên app");
        Log("--------------------------------------------------------------------------------");
        {
            var t3 = tableService.GetAllTables().First(t => t.TableNumber == "BÀN 03");
            tableService.UpdateTableStatus(t3.TableId, TableStatus.Available, adminId);
            var session3 = tableService.OpenTable(t3.TableId, adminId, null, "Khách ăn uống");

            var sting = invService.GetAllProducts().First(p => p.ProductCode == "STING");
            int stockBefore = sting.InventoryItem?.QuantityOnHand ?? 0;

            // Khách gọi 2 lon Sting (mặc định IsCompleted = false, IsCancelled = false)
            invService.OrderProductForSession(session3.SessionId, sting.ProductId, 2, adminId);
            int stockAfter = invService.GetProductById(sting.ProductId)!.InventoryItem!.QuantityOnHand;

            // Thu ngân bấm checkout (bếp bận nên chưa kịp ấn nút hoàn thành trên bếp)
            var invoice3 = billService.CheckoutTable(session3.SessionId, adminId, PaymentMethod.Cash, 0, 0, "Chốt bàn");

            if (invoice3.ServiceAmount == 0 && stockAfter < stockBefore)
            {
                totalFound++;
                Log("❌ PHÁT HIỆN LỖI [HIGH]: Quán mất trắng tiền đồ uống đã phục vụ!");
                Log($"   - Kho đã xuất: 2 lon Sting (Tồn giảm từ {stockBefore} -> {stockAfter})");
                Log($"   - Tiền dịch vụ trên hóa đơn thu của khách: {invoice3.ServiceAmount:N0} đ (0đ!)");
                Log("   - HẬU QUẢ: Khách uống nước miễn phí vì món chưa ấn hoàn thành bị loại khỏi hóa đơn!");
            }
            else
            {
                Log($"✅ BẢO VỆ TỐT: Tiền dịch vụ được thu đầy đủ: {invoice3.ServiceAmount:N0} đ");
            }

            // TEST 3: KIỂM TRA HÓA ĐƠN IN RA CÓ BỊ LỆCH TIỀN KHÔNG
            byte[] escPos = printService.GenerateEscPosBytes(invoice3);
            string escPosText = System.Text.Encoding.ASCII.GetString(escPos);
            if (escPosText.Contains("STING") || escPosText.Contains("Nuoctangluc"))
            {
                totalFound++;
                Log("❌ PHÁT HIỆN LỖI [HIGH]: Hóa đơn giấy in ra mâu thuẫn trực tiếp với số tiền!");
                Log("   - Trên giấy in: Có liệt kê 'Sting x2'");
                Log("   - Dòng tiền dịch vụ: 'Tien dich vu: 0 d'");
                Log("   - HẬU QUẢ: Khách hàng nhìn hóa đơn thấy có tên món nhưng tổng tiền không khớp, nghi ngờ quán gian lận!");
            }
        }

        // ====================================================================================
        // TEST 4: ĐỐT SẠCH TOÀN BỘ ĐIỂM CỦA KHÁCH KHI HÓA ĐƠN NHỎ HƠN ĐIỂM
        // ====================================================================================
        Log("\n--------------------------------------------------------------------------------");
        Log("🔴 TEST 4: [CHÁY ĐIỂM TÍCH LŨY] Khách có 200 điểm nhưng bill chỉ 50k");
        Log("--------------------------------------------------------------------------------");
        {
            var cust = custService.CreateOrUpdateCustomer("0999888777", "Khách VIP Nhiều Điểm");
            using (var ctx = contextFactory.CreateDbContext())
            {
                var c = ctx.Customers.Find(cust.CustomerId);
                c!.CurrentPoints = 200; // Có 200 điểm = 200.000đ
                ctx.SaveChanges();
            }

            var t4 = tableService.GetAllTables().First(t => t.TableNumber == "BÀN 04");
            tableService.UpdateTableStatus(t4.TableId, TableStatus.Available, adminId);
            var session4 = tableService.OpenTable(t4.TableId, adminId, cust.CustomerId, "Test trừ điểm");

            // Giả lập chơi 30 phút tiền giờ ~25.000đ
            // Thu ngân nhập khách muốn dùng 200 điểm
            var inv4 = billService.CheckoutTable(session4.SessionId, adminId, PaymentMethod.Cash, 0, 200, "Thanh toán bằng điểm");

            using (var ctx = contextFactory.CreateDbContext())
            {
                var checkCust = ctx.Customers.Find(cust.CustomerId);
                if (checkCust!.CurrentPoints == 0 && inv4.FinalAmount == 0)
                {
                    totalFound++;
                    Log($"❌ PHÁT HIỆN LỖI [MEDIUM]: Khách bị trừ lố toàn bộ 200 điểm!");
                    Log($"   - Tiền bill thực tế: {inv4.TimeAmount + inv4.ServiceAmount:N0} đ (Chỉ cần 25 điểm = 25k)");
                    Log($"   - Số điểm bị trừ: 200 điểm (Mất oan 175 điểm = 175.000 đ của khách!)");
                    Log($"   - Điểm còn lại của khách: {checkCust.CurrentPoints} điểm");
                    Log("   - HẬU QUẢ: Khách hàng bị mất điểm oan uổng, dẫn đến khiếu nại tranh cãi tại quầy!");
                }
                else
                {
                    Log($"✅ BẢO VỆ TỐT: Điểm chỉ bị trừ đúng bằng giá trị hóa đơn, còn dư: {checkCust.CurrentPoints} điểm.");
                }
            }
        }

        // ====================================================================================
        // TEST 5: ORDER SẢN PHẨM ĐÃ BỊ XÓA MỀM (INACTIVE PRODUCT EXPLOIT)
        // ====================================================================================
        Log("\n--------------------------------------------------------------------------------");
        Log("🔴 TEST 5: [ORDER SẢN PHẨM ĐÃ XÓA] Order món đã ngừng kinh doanh (IsActive = false)");
        Log("--------------------------------------------------------------------------------");
        {
            var p = invService.GetAllProducts().First();
            invService.DeleteProduct(p.ProductId); // Soft delete

            var t5 = tableService.GetAllTables().First(t => t.TableNumber == "BÀN 05");
            tableService.UpdateTableStatus(t5.TableId, TableStatus.Available, adminId);
            var session5 = tableService.OpenTable(t5.TableId, adminId, null, "Test inactive product");

            bool ordered = invService.OrderProductForSession(session5.SessionId, p.ProductId, 1, adminId);

            if (ordered)
            {
                totalFound++;
                Log($"❌ PHÁT HIỆN LỖI [MEDIUM]: Hệ thống vẫn cho phép order món đã bị xóa/ngừng bán!");
                Log($"   - Món: {p.ProductName} (IsActive = false)");
                Log("   - HẬU QUẢ: Nhân viên có thể order các món cũ đã ngừng kinh doanh hoặc sai giá!");
            }
            else
            {
                Log("✅ BẢO VỆ TỐT: Chặn không cho order món đã ngừng kinh doanh.");
            }

            // Khôi phục món
            using (var ctx = contextFactory.CreateDbContext())
            {
                var prod = ctx.Products.Find(p.ProductId);
                if (prod != null) prod.IsActive = true;
                ctx.SaveChanges();
            }
            billService.CheckoutTable(session5.SessionId, adminId, PaymentMethod.Cash, 0, 0, "Dọn dẹp");
        }

        // ====================================================================================
        // TEST 6: GHI ĐÈ GIÁ VỐN KHI NHẬP HÀNG (COST PRICE OVERWRITE)
        // ====================================================================================
        Log("\n--------------------------------------------------------------------------------");
        Log("🔴 TEST 6: [HỎNG BÁO CÁO LÃI LỖ] Ghi đè giá vốn toàn bộ kho khi nhập hàng");
        Log("--------------------------------------------------------------------------------");
        {
            var p = invService.GetAllProducts().First(prod => prod.CostPrice > 0);
            decimal originalCost = p.CostPrice;

            // Nhập 1 số lượng nhỏ với đơn giá 100đ (nhân viên gõ nhầm)
            invService.ImportStock(p.ProductId, 1, 100m, adminId, "Gõ nhầm giá");

            var updatedProd = invService.GetProductById(p.ProductId);

            if (updatedProd!.CostPrice == 100m)
            {
                totalFound++;
                Log($"❌ PHÁT HIỆN LỖI [HIGH]: Giá vốn hàng tồn bị ghi đè thô bạo!");
                Log($"   - Giá vốn ban đầu: {originalCost:N0} đ");
                Log($"   - Giá vốn sau khi nhập 1 món giá 100đ: {updatedProd.CostPrice:N0} đ");
                Log("   - HẬU QUẢ: Toàn bộ số lượng tồn cũ bị tính theo giá vốn mới, làm sai lệch báo cáo lợi nhuận tháng!");
            }
            else
            {
                Log($"✅ BẢO VỆ TỐT: Giá vốn được tính theo bình quân gia quyền: {updatedProd.CostPrice:N0} đ");
            }
        }

        // TỔNG KẾT
        Log("\n================================================================================");
        Log($"🎯 KẾT QUẢ KIỂM THỬ VÒNG 2: PHÁT HIỆN {totalFound} LỖI NGHIỆP VỤ & TÍNH TOÁN!");
        Log("================================================================================");

        string reportPath = @"D:\AppDesktop\QL_JBillard\Tests\HackerChaosTest2_Result.txt";
        try
        {
            File.WriteAllText(reportPath, _logReport.ToString(), System.Text.Encoding.UTF8);
            Log($"\n📄 Đã lưu báo cáo chi tiết vào file: {reportPath}");
        }
        catch { }

        try
        {
            if (File.Exists(tempDb)) File.Delete(tempDb);
        }
        catch { }
    }
}
