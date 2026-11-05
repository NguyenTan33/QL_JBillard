using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QL_JBillard.Data;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Helpers;
using QL_JBillard.Services.Implementations;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.Tests;

public static class HackerChaosTest
{
    private static readonly System.Text.StringBuilder _logReport = new();
    private static void Log(string message = "")
    {
        try { Console.WriteLine(message); } catch { }
        _logReport.AppendLine(message);
    }

    public static void Run()
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
        Log("================================================================================");
        Log("🕵️‍♂️ BẮT ĐẦU CHIẾN DỊCH PENETRATION TESTING & CHAOS QA: QL_JBILLARD");
        Log("🎭 ROLE: HACKER / NHÂN VIÊN GIAN LẬN / CHỦ QUÁN KHẮC NGHIỆT");
        Log("================================================================================\n");

        string tempDb = Path.Combine(Path.GetTempPath(), $"hacker_chaos_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={tempDb}")
            .Options;

        var contextFactory = new SimpleDbContextFactory(options);

        // Khởi tạo Database và Seed
        using (var ctx = contextFactory.CreateDbContext())
        {
            DatabaseInitializer.Initialize(ctx);
        }

        var auditService = new AuditService(contextFactory);
        var authService = new AuthService(contextFactory, auditService);
        var tableService = new TableService(contextFactory, auditService);
        var invService = new InventoryService(contextFactory, auditService);
        var custService = new CustomerService(contextFactory);
        var shiftService = new ShiftService(contextFactory, auditService);
        var billService = new BillingService(contextFactory, auditService, shiftService);
        var backupService = new BackupAndSecurityService(contextFactory, auditService);

        // Đăng nhập tài khoản thu ngân
        authService.Login("admin", "admin123");
        authService.CreateUser("thungan_gianlan", "123456", "Vũ Trọng Gian", "0911222333", UserRole.Cashier);
        authService.Logout();

        authService.Login("thungan_gianlan", "123456");
        int cashierId = authService.CurrentUser!.UserId;

        // Mở ca làm việc ban đầu: 1.000.000đ
        var shift = shiftService.StartShift(cashierId, 1_000_000m, "Mở ca thử nghiệm");
        int shiftId = shift.ShiftId;

        int totalExploitsTested = 0;
        int successfulExploits = 0;

        // ====================================================================================
        // TEST 1: GIAN LẬN BÀN VÔ HÌNH (INVISIBLE TABLE / ORPHAN SESSION FRAUD)
        // Kịch bản: Khách chơi 2 tiếng. Thu ngân gọi UpdateTableStatus -> Available.
        // Bàn trống ngay lập tức, phiên chơi bị mồ côi, không hóa đơn, tiền bỏ túi riêng!
        // ====================================================================================
        totalExploitsTested++;
        Log("--------------------------------------------------------------------------------");
        Log("🔴 TEST 1: [GIAN LẬN BÀN VÔ HÌNH] Xóa dấu vết phiên chơi để đút túi tiền giờ");
        Log("--------------------------------------------------------------------------------");
        {
            var table1 = tableService.GetAllTables().First(t => t.TableNumber == "BÀN 01");
            var session1 = tableService.OpenTable(table1.TableId, cashierId, null, "Khách chơi 2 tiếng");

            // Nhân viên chuyển thẳng trạng thái bàn về Available mà KHÔNG checkout
            bool hackedStatus = tableService.UpdateTableStatus(table1.TableId, TableStatus.Available, cashierId);

            using var ctx = contextFactory.CreateDbContext();
            var checkTable = ctx.Tables.Find(table1.TableId);
            var checkSession = ctx.TableSessions.Find(session1.SessionId);
            var invoiceCount = ctx.Invoices.Count(i => i.SessionId == session1.SessionId);

            if (checkTable!.Status == TableStatus.Available && 
                checkTable.CurrentSessionId == null && 
                checkSession!.Status == SessionStatus.Active && 
                invoiceCount == 0)
            {
                successfulExploits++;
                Log("❌ LỖ HỔNG XÁC NHẬN [CRITICAL]: Thu ngân xóa sổ bàn đang chơi thành công!");
                Log("   - Trạng thái bàn trên giao diện: BÀN TRỐNG (Available)");
                Log("   - Phiên chơi trong CSDL: BỊ MỒ CÔI (Vẫn Active, không ai quản lý)");
                Log("   - Số hóa đơn sinh ra: 0 HÓA ĐƠN");
                Log("   - HẬU QUẢ: Khách trả 100k-200k tiền mặt -> Thu ngân đút túi 100%, chủ quán không biết gì!");
            }
            else
            {
                Log("✅ BẢO VỆ TỐT: Hệ thống đã chặn hoặc xử lý an toàn.");
            }
        }

        // ====================================================================================
        // TEST 2: HOÁN ĐỔI GIÁ BÀN VIP SANG BÀN THƯỜNG (RETROACTIVE RATE REWRITE)
        // Kịch bản: Khách đánh bàn VIP 100k/h trong 3 tiếng (= 300k).
        // Thu ngân chuyển sang Bàn Thường 40k/h ngay trước khi checkout.
        // Hệ thống áp dụng 40k cho toàn bộ 3 tiếng (= 120k). Thu ngân ăn chặn 180k!
        // ====================================================================================
        totalExploitsTested++;
        Log("\n--------------------------------------------------------------------------------");
        Log("🔴 TEST 2: [HOÁN ĐỔI GIÁ BÀN VIP] Hạ giá giờ hồi tố để ăn tiền chênh lệch");
        Log("--------------------------------------------------------------------------------");
        {
            using var ctx = contextFactory.CreateDbContext();
            var types = ctx.TableTypes.OrderBy(t => t.TableTypeId).ToList();
            var vipType = types.FirstOrDefault(t => t.TypeName.Contains("VIP")) ?? types.First();
            vipType.DefaultHourlyRate = 100_000m;
            var stdType = types.FirstOrDefault(t => t.TypeName.Contains("Thường") || t.TypeName.Contains("Lỗ Tiêu Chuẩn")) ?? types.Last();
            stdType.DefaultHourlyRate = 40_000m;
            ctx.SaveChanges();

            var vipTable = ctx.Tables.First(t => t.TableTypeId == vipType.TableTypeId);
            var stdTable = ctx.Tables.First(t => t.TableTypeId == stdType.TableTypeId && t.TableId != vipTable.TableId);
            
            // Đặt cả 2 bàn về Available
            tableService.UpdateTableStatus(vipTable.TableId, TableStatus.Available, cashierId);
            tableService.UpdateTableStatus(stdTable.TableId, TableStatus.Available, cashierId);

            // Khách mở bàn VIP
            var vipSession = tableService.OpenTable(vipTable.TableId, cashierId, null, "Khách VIP đánh 3 tiếng");
            
            // Giả lập chơi 3 tiếng trước
            using (var fixCtx = contextFactory.CreateDbContext())
            {
                var s = fixCtx.TableSessions.Find(vipSession.SessionId);
                s!.StartTime = DateTime.Now.AddHours(-3);
                fixCtx.SaveChanges();
            }

            // Ngay trước khi thanh toán, thu ngân chuyển từ bàn VIP sang bàn Thường
            bool switched = tableService.SwitchTable(vipTable.TableId, stdTable.TableId, cashierId, "Đổi bàn");
            
            // Checkout tại bàn mới
            var inv = billService.CheckoutTable(vipSession.SessionId, cashierId, PaymentMethod.Cash, 0, 0, "Chốt bàn");

            if (inv.TimeAmount == 120_000m)
            {
                successfulExploits++;
                Log("❌ LỖ HỔNG XÁC NHẬN [CRITICAL]: Giá bàn VIP bị hồi tố thành bàn thường!");
                Log($"   - Giá đáng lẽ phải thu (VIP 100k/h x 3h): 300,000 đ");
                Log($"   - Giá thực tế hóa đơn xuất ra: {inv.TimeAmount:N0} đ");
                Log($"   - Số tiền thu ngân có thể ăn chặn: 180,000 đ");
                Log("   - HẬU QUẢ: SwitchTable không phân đoạn thời gian (prorate) mà ghi đè AppliedHourlyRate từ đầu!");
            }
            else
            {
                Log($"✅ BẢO VỆ TỐT: Tiền giờ được bảo toàn hoặc tính lũy tiến: {inv.TimeAmount:N0} đ");
            }
        }

        // ====================================================================================
        // TEST 3: GIAN LẬN NỘP TIỀN NGÂN HÀNG (PHANTOM BANK DEPOSIT)
        // Kịch bản: Nộp tiền số âm, nộp quá tiền két, và phiếu Pending vẫn trừ tiền két!
        // ====================================================================================
        totalExploitsTested++;
        Log("\n--------------------------------------------------------------------------------");
        Log("🔴 TEST 3: [RÚT RUỘT KÉT] Nộp tiền số âm / khống & Phiếu chưa duyệt");
        Log("--------------------------------------------------------------------------------");
        {
            // 3A: Nộp số tiền âm
            try
            {
                var negativeSlip = shiftService.CreateBankDepositSlip(cashierId, -500_000m, null, "Rút âm tiền két", shiftId);
                successfulExploits++;
                Log($"❌ LỖ HỔNG [HIGH]: Hệ thống cho phép nộp tiền SỐ ÂM ({negativeSlip.Amount:N0} đ)!");
            }
            catch (Exception ex)
            {
                Log($"✅ BẢO VỆ: Chặn số âm: {ex.Message}");
            }

            // 3B: Nộp tiền vượt quá két
            try
            {
                var hugeSlip = shiftService.CreateBankDepositSlip(cashierId, 100_000_000m, null, "Nộp khống 100 triệu", shiftId);
                var summary = shiftService.GetCurrentShiftSummary();
                successfulExploits++;
                Log($"❌ LỖ HỔNG [HIGH]: Két chỉ có 1 triệu nhưng cho nộp khống 100 triệu! (Tiền két hiện tại: {summary.CurrentDrawerCash:N0} đ)");
            }
            catch (Exception ex)
            {
                Log($"✅ BẢO VỆ: Chặn nộp quá số dư két: {ex.Message}");
            }

            // 3C: Phiếu tạo mới bởi thu ngân phải là Pending (chờ duyệt), không được tự động Approved
            try
            {
                var validSlip = shiftService.CreateBankDepositSlip(cashierId, 100_000m, null, "Nộp tiền hợp lệ trong ca", shiftId);
                if (validSlip.Status == "Approved")
                {
                    successfulExploits++;
                    Log("❌ LỖ HỔNG [MEDIUM]: Phiếu nộp tiền tự động 'Approved' ngay khi thu ngân tạo mà không cần Admin duyệt!");
                }
                else
                {
                    Log($"✅ BẢO VỆ TỐT: Phiếu nộp tiền của thu ngân ở trạng thái [{validSlip.Status}] chờ Admin duyệt.");
                }
            }
            catch (Exception ex)
            {
                Log($"Lỗi tạo phiếu: {ex.Message}");
            }
        }

        // ====================================================================================
        // TEST 4: CHIẾT KHẤU ÂM & TĂNG TIỀN HÓA ĐƠN
        // Kịch bản: Thu ngân nhập discountPercent = -50% để đôn tiền hóa đơn
        // ====================================================================================
        totalExploitsTested++;
        Log("\n--------------------------------------------------------------------------------");
        Log("🔴 TEST 4: [CHIẾT KHẤU BẤT HỢP LỆ] Nhập chiết khấu âm hoặc > 100%");
        Log("--------------------------------------------------------------------------------");
        {
            var table2 = tableService.GetAllTables().First(t => t.TableNumber == "BÀN 02");
            tableService.UpdateTableStatus(table2.TableId, TableStatus.Available, cashierId);
            var session2 = tableService.OpenTable(table2.TableId, cashierId, null, "Test chiết khấu");

            // Checkout với chiết khấu âm -50%
            var invoiceNeg = billService.CheckoutTable(session2.SessionId, cashierId, PaymentMethod.Cash, -50m, 0, "Chiết khấu âm");

            if (invoiceNeg.DiscountAmount < 0)
            {
                successfulExploits++;
                Log($"❌ LỖ HỔNG [HIGH]: Hệ thống chấp nhận chiết khấu âm ({invoiceNeg.DiscountPercent}%)!");
                Log($"   - Tiền giảm giá: {invoiceNeg.DiscountAmount:N0} đ (BỊ ÂM)");
                Log($"   - Tiền khách phải trả bị đôn lên: {invoiceNeg.FinalAmount:N0} đ");
                Log("   - HẬU QUẢ: Thu ngân có thể nhập số âm để đôn khống tiền bill của khách!");
            }
            else
            {
                Log("✅ BẢO VỆ TỐT: Chiết khấu âm bị chặn hoặc chuẩn hóa.");
            }
        }

        // ====================================================================================
        // TEST 5: TẠO HÀNG TỒN KHO ẢO BẰNG HỦY MÓN TRONG COMBO (INFINITE STOCK EXPLOIT)
        // Kịch bản: Mở bàn Combo -> Món kèm 0đ được tạo. Gọi CancelOrderItem -> Kho được hoàn!
        // Thu ngân có thể lặp lại nhiều lần để tạo hàng tồn kho ảo hoặc lấy trộm đồ mà kho không lệch!
        // ====================================================================================
        totalExploitsTested++;
        Log("\n--------------------------------------------------------------------------------");
        Log("🔴 TEST 5: [TRỘM KHO COMBO] Phù phép hàng tồn kho bằng cách hủy món Combo");
        Log("--------------------------------------------------------------------------------");
        {
            var sting = invService.GetAllProducts().First(p => p.ProductCode == "STING");
            int stockBefore = sting.InventoryItem?.QuantityOnHand ?? 0;

            var table3 = tableService.GetAllTables().First(t => t.TableNumber == "BÀN 03");
            tableService.UpdateTableStatus(table3.TableId, TableStatus.Available, cashierId);

            using var ctx = contextFactory.CreateDbContext();
            var combo = ctx.PlayCombos.First();
            var comboSession = tableService.OpenTable(table3.TableId, cashierId, null, "Khách combo", "COMBO", combo.ComboId);

            int stockAfterOpen = invService.GetProductById(sting.ProductId)!.InventoryItem!.QuantityOnHand;

            // Tìm order item vừa sinh ra từ combo
            var comboItem = ctx.OrderItems.FirstOrDefault(o => o.SessionId == comboSession.SessionId && o.ProductId == sting.ProductId);
            if (comboItem != null)
            {
                // Thu ngân bấm hủy món kèm combo
                invService.CancelOrderItem(comboItem.OrderItemId, cashierId, "Khách không uống");

                int stockAfterCancel = invService.GetProductById(sting.ProductId)!.InventoryItem!.QuantityOnHand;

                if (stockAfterCancel > stockAfterOpen)
                {
                    successfulExploits++;
                    Log("❌ LỖ HỔNG [HIGH]: Hủy món combo làm tăng tồn kho nhưng tiền gói combo không đổi!");
                    Log($"   - Tồn ban đầu: {stockBefore} lon");
                    Log($"   - Tồn sau mở combo: {stockAfterOpen} lon");
                    Log($"   - Tồn sau khi hủy món: {stockAfterCancel} lon");
                    Log("   - HẬU QUẢ: Nhân viên có thể mở combo, bấm hủy món để lấy Sting uống hoặc tuồn ra ngoài bán mà kho vẫn cân!");
                }
                else
                {
                    Log("✅ BẢO VỆ: Không hoàn kho hoặc kiểm soát an toàn.");
                }
            }
            else
            {
                Log("ℹ️ Combo không có món Sting để kiểm thử.");
            }
            
            // Dọn dẹp bàn 03
            tableService.UpdateTableStatus(table3.TableId, TableStatus.Available, cashierId);
        }

        // ====================================================================================
        // TEST 6: PHÁ VỠ CHUỖI BĂM AUDIT LOG KHI 2 THAO TÁC GHI ĐỒNG THỜI (RACE CONDITION)
        // Kịch bản: 2 luồng ghi audit cùng lúc -> Đọc cùng PreviousHash -> Chuỗi bị chẻ nhánh (fork).
        // VerifyChainIntegrity() sẽ báo CSDL bị xâm phạm giả tạo!
        // ====================================================================================
        totalExploitsTested++;
        Log("\n--------------------------------------------------------------------------------");
        Log("🔴 TEST 6: [PHÁ HỎNG CHUỖI BĂM AUDIT] Race condition khi ghi log đồng thời");
        Log("--------------------------------------------------------------------------------");
        {
            // Bắn 10 logs đồng thời bằng Task.WhenAll
            var tasks = new List<Task>();
            for (int i = 0; i < 8; i++)
            {
                int index = i;
                tasks.Add(Task.Run(() =>
                {
                    auditService.Log(cashierId, $"CONCURRENT_ACTION_{index}", "TestEntity", index.ToString(), null, new { Value = index });
                }));
            }
            Task.WaitAll(tasks.ToArray());

            var (isValid, tamperedIndex, message) = auditService.VerifyChainIntegrity();

            if (!isValid)
            {
                successfulExploits++;
                Log("❌ LỖ HỔNG XÁC NHẬN [CRITICAL]: Chuỗi Audit Hash bị vỡ nát khi ghi đồng thời!");
                Log($"   - Kết quả xác thực toàn vẹn: FALSE");
                Log($"   - Vị trí vỡ chuỗi: Bản ghi #{tamperedIndex}");
                Log($"   - Thông điệp lỗi: {message}");
                Log("   - HẬU QUẢ: Tính năng Tamper-Proof bị 'báo động giả' liên tục, khiến chủ quán tưởng CSDL bị hacker tấn công!");
            }
            else
            {
                Log("✅ BẢO VỆ TỐT: Chuỗi Hash toàn vẹn tuyệt đối.");
            }
        }

        // ====================================================================================
        // TEST 7: DOUBLE CHECKOUT RACE CONDITION (BẤM THANH TOÁN 2 LẦN LIÊN TIẾP CỰC NHANH)
        // Kịch bản: Thu ngân double-click hoặc 2 máy cùng checkout 1 phiên chơi.
        // ====================================================================================
        totalExploitsTested++;
        Log("\n--------------------------------------------------------------------------------");
        Log("🔴 TEST 7: [DOUBLE CHECKOUT] Bấm thanh toán đồng thời");
        Log("--------------------------------------------------------------------------------");
        {
            var table4 = tableService.GetAllTables().First(t => t.TableNumber == "BÀN 04");
            tableService.UpdateTableStatus(table4.TableId, TableStatus.Available, cashierId);
            var session4 = tableService.OpenTable(table4.TableId, cashierId, null, "Double checkout test");

            int successCheckoutCount = 0;
            int failedCheckoutCount = 0;

            Parallel.For(0, 2, i =>
            {
                try
                {
                    billService.CheckoutTable(session4.SessionId, cashierId, PaymentMethod.Cash, 0, 0, $"Checkout thread {i}");
                    System.Threading.Interlocked.Increment(ref successCheckoutCount);
                }
                catch
                {
                    System.Threading.Interlocked.Increment(ref failedCheckoutCount);
                }
            });

            using var ctx = contextFactory.CreateDbContext();
            int invoicesForSession = ctx.Invoices.Count(i => i.SessionId == session4.SessionId);

            if (invoicesForSession > 1 || successCheckoutCount > 1)
            {
                successfulExploits++;
                Log($"❌ LỖ HỔNG XÁC NHẬN [CRITICAL]: Sinh ra {invoicesForSession} HÓA ĐƠN cho cùng 1 phiên chơi!");
                Log("   - HẬU QUẢ: Doanh thu ca bị tính gấp đôi, số liệu báo cáo sai lệch nghiêm trọng!");
            }
            else
            {
                Log($"✅ BẢO VỆ TỐT: Chỉ 1 yêu cầu checkout thành công ({successCheckoutCount}), yêu cầu còn lại bị chặn ({failedCheckoutCount}).");
            }
        }

        // ====================================================================================
        // TEST 8: LEO THANG ĐẶC QUYỀN (PRIVILEGE ESCALATION TẠI SERVICE LAYER)
        // Kịch bản: Thu ngân gọi thẳng AuthService.UpdateUser để nâng mình lên Admin!
        // ====================================================================================
        totalExploitsTested++;
        Log("\n--------------------------------------------------------------------------------");
        Log("🔴 TEST 8: [LEO THANG ĐẶC QUYỀN] Thu ngân tự phong mình thành Admin");
        Log("--------------------------------------------------------------------------------");
        {
            // Thu ngân gọi UpdateUser để đổi Role thành Admin
            bool updated = authService.UpdateUser(cashierId, "Vũ Trọng Gian (Đã Hack)", "0911222333", UserRole.Admin, true);

            using var ctx = contextFactory.CreateDbContext();
            var checkUser = ctx.Users.Find(cashierId);

            if (checkUser!.Role == UserRole.Admin)
            {
                successfulExploits++;
                Log("❌ LỖ HỔNG XÁC NHẬN [CRITICAL]: Service Layer hoàn toàn KHÔNG kiểm tra quyền của người gọi!");
                Log($"   - Quyền ban đầu: Cashier");
                Log($"   - Quyền sau khi hack: {checkUser.Role}");
                Log("   - HẬU QUẢ: Bất kỳ đoạn mã nào (hoặc plugin, script ngoài) cũng có thể tự cấp quyền Admin!");
            }
            else
            {
                Log("✅ BẢO VỆ TỐT: Role không bị thay đổi.");
            }
        }

        // ====================================================================================
        // TEST 9: XÓA SẠCH FILE SAO LƯU CỦA CHỦ QUÁN TRONG CleanOldBackups (DATA LOSS)
        // Kịch bản: Chủ quán copy các file backup dự phòng vào thư mục Backups.
        // CleanOldBackups tự ý xóa tất cả các file không có tên chuẩn!
        // ====================================================================================
        totalExploitsTested++;
        Log("\n--------------------------------------------------------------------------------");
        Log("🔴 TEST 9: [MẤT MÁT DỮ LIỆU] Hệ thống tự động xóa sạch backup của người dùng");
        Log("--------------------------------------------------------------------------------");
        {
            string backupFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
            if (!Directory.Exists(backupFolder)) Directory.CreateDirectory(backupFolder);

            string userImportantBackup = Path.Combine(backupFolder, "backup_chu_quan_luu_rieng_truoc_khi_nang_cap.db");
            File.WriteAllText(userImportantBackup, "SQLite format 3 test data");

            // Kích hoạt CreateBackup -> CleanOldBackups chạy
            backupService.CreateBackup("CHAOS_TEST", false);

            if (!File.Exists(userImportantBackup))
            {
                successfulExploits++;
                Log("❌ LỖ HỔNG XÁC NHẬN [HIGH]: Hệ thống đã xóa sạch file backup thủ công của người dùng!");
                Log($"   - File bị xóa: {Path.GetFileName(userImportantBackup)}");
                Log("   - HẬU QUẢ: Nếu chủ quán copy file backup của ngày hôm trước vào để lưu trữ, phần mềm sẽ âm thầm XÓA MẤT!");
            }
            else
            {
                Log("✅ BẢO VỆ TỐT: File backup của người dùng không bị xóa.");
                try { File.Delete(userImportantBackup); } catch { }
            }
        }

        // ====================================================================================
        // TEST 10: ƯU TIÊN KHUNG GIỜ GIÁ BỊ LỖI THỨ TỰ (TIME-SLOT PRIORITY INVERSION)
        // Kịch bản: Khung giờ 'Cả ngày 50k' tạo trước, 'Cao điểm tối 80k' tạo sau.
        // Thuật toán match slot đầu tiên nên khung cao điểm KHÔNG BAO GIỜ được áp dụng!
        // ====================================================================================
        totalExploitsTested++;
        Log("\n--------------------------------------------------------------------------------");
        Log("🔴 TEST 10: [LỖ TIỀN GIỜ] Đảo lộn độ ưu tiên khung giờ giá (Time-Slot Priority)");
        Log("--------------------------------------------------------------------------------");
        {
            var testSlots = new List<TimeSlotPricing>
            {
                // Slot 1: Tạo trước - Cả ngày (00:00 - 24:00) giá 50.000đ
                new() { SlotName = "Cả ngày", StartHour = 0, StartMinute = 0, EndHour = 24, EndMinute = 0, HourlyRate = 50_000, IsActive = true },
                // Slot 2: Tạo sau - Cao điểm tối (18:00 - 22:00) giá 80.000đ
                new() { SlotName = "Cao điểm tối", StartHour = 18, StartMinute = 0, EndHour = 22, EndMinute = 0, HourlyRate = 80_000, IsActive = true }
            };

            // Khách chơi lúc 19:00 - 21:00 (2 tiếng cao điểm, đáng lẽ phải là 160.000đ)
            var startT = new DateTime(2026, 11, 4, 19, 0, 0);
            var endT = new DateTime(2026, 11, 4, 21, 0, 0);
            decimal cost = BilliardPricingHelper.CalculateTimeSlotCost(startT, endT, 50_000, testSlots);

            if (cost == 100_000m)
            {
                successfulExploits++;
                Log("❌ LỖ HỔNG XÁC NHẬN [HIGH]: Khung giờ cao điểm (80k) bị bỏ qua, chỉ tính giá thường (50k)!");
                Log($"   - Giá đáng lẽ phải thu (2h @ 80k): 160,000 đ");
                Log($"   - Giá thực tế bị tính (2h @ 50k): {cost:N0} đ");
                Log($"   - Thất thoát doanh thu: 60,000 đ");
                Log("   - HẬU QUẢ: Do duyệt foreach activeSlots và lấy break ngay ở match đầu tiên mà không có trọng số ưu tiên!");
            }
            else
            {
                Log($"✅ BẢO VỆ TỐT: Tính đúng giá cao điểm: {cost:N0} đ");
            }
        }

        // ====================================================================================
        // TEST 11: LỘ KHÓA MÃ HÓA CỨNG (HARDCODED ENCRYPTION KEY IN SOURCE)
        // ====================================================================================
        totalExploitsTested++;
        Log("\n--------------------------------------------------------------------------------");
        Log("🔴 TEST 11: [AN TOÀN BẢO MẬT] Khóa mã hóa CSDL cứng trong mã nguồn");
        Log("--------------------------------------------------------------------------------");
        {
            // Kiểm tra trong file BackupAndSecurityService có chứa chuỗi pass cứng không
            string serviceFile = Path.Combine("D:\\AppDesktop\\QL_JBillard\\Services\\Implementations\\BackupAndSecurityService.cs");
            if (File.Exists(serviceFile))
            {
                string content = File.ReadAllText(serviceFile);
                if (content.Contains("JBillard@Encrypted#2026_Backup_SecPass!"))
                {
                    successfulExploits++;
                    Log("❌ LỖ HỔNG XÁC NHẬN [CRITICAL]: Hardcoded AES Key trong mã nguồn!");
                    Log("   - Key: 'JBillard@Encrypted#2026_Backup_SecPass!'");
                    Log("   - HẬU QUẢ: Dùng công cụ dịch ngược (ILSpy, dnSpy) 3 giây là giải mã toàn bộ dữ liệu nhạy cảm!");
                }
                else
                {
                    Log("✅ BẢO VỆ TỐT: Khóa mã hóa an toàn dựa trên Machine Entropy động, không còn chuỗi hardcode trong mã nguồn.");
                }
            }
        }

        // TỔNG KẾT
        Log("\n================================================================================");
        Log($"🎯 KẾT QUẢ TỔNG KẾT: {successfulExploits}/{totalExploitsTested} KỊCH BẢN TẤN CÔNG THÀNH CÔNG RỰC RỠ!");
        Log("================================================================================");

        string reportPath = @"D:\AppDesktop\QL_JBillard\Tests\HackerChaosTest_Result.txt";
        try
        {
            File.WriteAllText(reportPath, _logReport.ToString(), System.Text.Encoding.UTF8);
            Log($"\n📄 Đã lưu báo cáo chi tiết vào file: {reportPath}");
        }
        catch { }

        // Dọn dẹp temp db
        try
        {
            if (File.Exists(tempDb)) File.Delete(tempDb);
        }
        catch { }
    }
}
