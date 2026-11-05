using Microsoft.EntityFrameworkCore;
using QL_JBillard.Data;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Domain.Models;
using QL_JBillard.Helpers;
using QL_JBillard.Services.DTOs;
using QL_JBillard.Services.Interfaces;
using QL_JBillard.Services.Strategies.Pricing;

namespace QL_JBillard.Services.Implementations;

public class BillingService : IBillingService
{
    private static readonly object _checkoutLock = new();
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IAuditService _auditService;
    private readonly IShiftService? _shiftService;
    private readonly IPricingStrategy _defaultPricingStrategy;

    public BillingService(
        IDbContextFactory<AppDbContext> contextFactory, 
        IAuditService auditService,
        IShiftService? shiftService = null)
    {
        _contextFactory = contextFactory;
        _auditService = auditService;
        _shiftService = shiftService;
        _defaultPricingStrategy = new StandardPricingStrategy();
    }

    public LiveBillDto CalculateLiveBill(int sessionId, IPricingStrategy? strategy = null)
    {
        if (sessionId <= 0)
        {
            return new LiveBillDto
            {
                SessionId = 0,
                StartTime = DateTime.Now,
                CurrentTime = DateTime.Now,
                TimeCost = 0,
                ServiceCost = 0,
                OrderItems = new List<OrderItem>()
            };
        }

        using var context = _contextFactory.CreateDbContext();
        var session = context.TableSessions
                             .Include(s => s.Table)
                             .ThenInclude(t => t.TableType)
                             .Include(s => s.Customer)
                             .ThenInclude(c => c!.Tier)
                             .Include(s => s.OrderItems.Where(o => !o.IsCancelled))
                             .ThenInclude(o => o.Product)
                             .FirstOrDefault(s => s.SessionId == sessionId);

        if (session == null)
            throw new InvalidOperationException($"Không tìm thấy phiên chơi #{sessionId}");

        var effectiveStrategy = strategy ?? _defaultPricingStrategy;
        var now = session.EndTime ?? DateTime.Now;

        // 1. Tính tiền giờ hỗ trợ Khung Giờ Giá và Gói Combo
        var timeSlots = context.TimeSlotPricings
                               .Where(s => s.TableTypeId == session.Table.TableTypeId && s.IsActive)
                               .ToList();

        decimal currentTableCost = strategy != null
            ? strategy.CalculateCost(session.StartTime, now, session.AppliedHourlyRate, session.Table.TableType.MinPlayMinutes)
            : BilliardPricingHelper.CalculateSessionCost(
                session.StartTime,
                now,
                session.AppliedHourlyRate,
                session.PlayMode,
                session.ComboPrice,
                session.ComboDurationMinutes,
                timeSlots
            );

        // Bảo toàn tiền giờ tích lũy từ các lần chuyển bàn trước đó (Prorated Billing)
        decimal timeCost = currentTableCost + session.TimeCost;

        // Xây dựng danh sách các chặng bàn chi tiết (Hỗ trợ chuyển bàn lệch giá)
        var segments = session.GetSegments();
        var allSegments = new List<TableSessionSegment>(segments);
        int currentSegmentMinutes = (int)Math.Max(1, Math.Round((now - session.StartTime).TotalMinutes));
        allSegments.Add(new TableSessionSegment
        {
            TableId = session.TableId,
            TableNumber = session.Table?.TableNumber ?? $"Bàn {session.TableId}",
            TableTypeName = session.Table?.TableType?.TypeName ?? "Tiêu chuẩn",
            HourlyRate = session.AppliedHourlyRate,
            StartTime = session.StartTime,
            EndTime = now,
            PlayMinutes = currentSegmentMinutes,
            Cost = currentTableCost
        });

        // 2. Tính tiền dịch vụ F&B (Toàn bộ các món đã gọi và chưa hủy)
        decimal serviceCost = session.OrderItems.Where(o => !o.IsCancelled).Sum(o => o.TotalAmount);

        // 3. Giảm giá theo hạng thành viên (nếu có khách hàng đăng ký)
        decimal discountPercent = 0;
        if (session.Customer != null && session.Customer.Tier != null)
        {
            discountPercent = session.Customer.Tier.DiscountPercentHour;
        }

        decimal discountAmount = timeCost * (discountPercent / 100m);

        return new LiveBillDto
        {
            SessionId = session.SessionId,
            TableId = session.TableId,
            TableNumber = session.Table?.TableNumber ?? $"Bàn {session.TableId}",
            TableTypeName = session.Table?.TableType?.TypeName ?? "Tiêu chuẩn",
            HourlyRate = session.AppliedHourlyRate,
            AppliedComboName = session.ComboName,
            PlayMode = session.PlayMode ?? "HOURLY",
            ComboPrice = session.ComboPrice,
            ComboDurationMinutes = session.ComboDurationMinutes,
            StartTime = session.StartTime,
            CurrentTime = now,
            AccumulatedMinutes = session.AccumulatedPlayMinutes,
            TimeCost = timeCost,
            ServiceCost = serviceCost,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            Customer = session.Customer,
            AvailableCustomerPoints = session.Customer?.CurrentPoints ?? 0,
            OrderItems = session.OrderItems.ToList(),
            Segments = allSegments
        };
    }

    public Invoice CheckoutTable(
        int sessionId,
        int cashierUserId,
        PaymentMethod paymentMethod,
        decimal discountPercent,
        int pointsUsed,
        string? notes,
        string? bankRef = null,
        IPricingStrategy? strategy = null)
    {
        lock (_checkoutLock)
        {
            using var context = _contextFactory.CreateDbContext();
            using var transaction = context.Database.BeginTransaction();

            var session = context.TableSessions
                                 .Include(s => s.Table)
                                 .ThenInclude(t => t.TableType)
                                 .Include(s => s.Customer)
                                 .ThenInclude(c => c!.Tier)
                                 .Include(s => s.OrderItems.Where(o => !o.IsCancelled))
                                 .ThenInclude(o => o.Product)
                                 .FirstOrDefault(s => s.SessionId == sessionId);

            if (session == null)
                throw new InvalidOperationException($"Không tìm thấy phiên chơi #{sessionId}");

            if (session.Status != SessionStatus.Active)
                throw new InvalidOperationException($"Phiên chơi #{sessionId} không ở trạng thái Active (có thể đã được thanh toán trước đó)!");

            var now = DateTime.Now;
            var effectiveStrategy = strategy ?? _defaultPricingStrategy;

            // Tính tiền giờ kết thúc hỗ trợ Khung Giờ Giá & Gói Combo
            var timeSlots = context.TimeSlotPricings
                                   .Where(s => s.TableTypeId == session.Table.TableTypeId && s.IsActive)
                                   .ToList();

            decimal segmentCost = strategy != null
                ? strategy.CalculateCost(session.StartTime, now, session.AppliedHourlyRate, session.Table.TableType.MinPlayMinutes)
                : BilliardPricingHelper.CalculateSessionCost(
                    session.StartTime,
                    now,
                    session.AppliedHourlyRate,
                    session.PlayMode,
                    session.ComboPrice,
                    session.ComboDurationMinutes,
                    timeSlots
                );

            // Bảo toàn tiền giờ tích lũy từ các lần chuyển bàn trước đó (Prorated Billing)
            decimal timeCost = session.TimeCost + segmentCost;

            int currentSegmentMinutes = (int)Math.Max(1, Math.Round((now - session.StartTime).TotalMinutes));
            int playMinutes = session.AccumulatedPlayMinutes + currentSegmentMinutes;

            // Cập nhật chặng cuối cùng vào TransferHistoryJson nếu phiên có chuyển bàn
            var finalSegments = session.GetSegments();
            if (finalSegments.Count > 0)
            {
                finalSegments.Add(new TableSessionSegment
                {
                    TableId = session.TableId,
                    TableNumber = session.Table?.TableNumber ?? $"Bàn {session.TableId}",
                    TableTypeName = session.Table?.TableType?.TypeName ?? "Tiêu chuẩn",
                    HourlyRate = session.AppliedHourlyRate,
                    StartTime = session.StartTime,
                    EndTime = now,
                    PlayMinutes = currentSegmentMinutes,
                    Cost = segmentCost
                });
                session.SetSegments(finalSegments);
            }

            session.EndTime = now;
            session.TotalPlayMinutes = playMinutes;
            session.TimeCost = timeCost;
            session.Status = SessionStatus.Completed;

            // Tự động hoàn thành tất cả các món ăn/thức uống chưa hủy khi thanh toán để bảo vệ doanh thu quán
            foreach (var item in session.OrderItems.Where(o => !o.IsCancelled && !o.IsCompleted))
            {
                item.IsCompleted = true;
                item.CompletedAt = now;
                item.CompletedByUserId = cashierUserId;
            }

            // Tiền dịch vụ (Tính toàn bộ các món đã phục vụ chưa hủy)
            decimal serviceCost = session.OrderItems.Where(o => !o.IsCancelled).Sum(o => o.TotalAmount);
            decimal subTotal = timeCost + serviceCost;

            // Giảm giá thủ công hoặc theo hạng (Giới hạn [0, 100]% chống chiết khấu âm)
            decimal safeDiscountPercent = Math.Clamp(discountPercent, 0m, 100m);
            decimal discountAmount = Math.Round(subTotal * (safeDiscountPercent / 100m) / 1000m, MidpointRounding.AwayFromZero) * 1000m;

            // Khấu trừ điểm tích lũy: 1 điểm = 1,000đ (Chặn số âm và giới hạn tối đa bằng số tiền bill cần trả)
            int safePointsUsed = Math.Max(0, pointsUsed);
            decimal pointDiscount = 0;
            int pointsActuallyUsed = 0;
            if (safePointsUsed > 0 && session.Customer != null)
            {
                decimal remainingToPay = Math.Max(0, subTotal - discountAmount);
                int maxPointsNeeded = (int)Math.Ceiling(remainingToPay / 1000m);
                pointsActuallyUsed = Math.Min(safePointsUsed, Math.Min(session.Customer.CurrentPoints, maxPointsNeeded));
                pointDiscount = Math.Min(remainingToPay, pointsActuallyUsed * 1000m);
                session.Customer.CurrentPoints -= pointsActuallyUsed;
            }

            decimal finalAmount = Math.Max(0, subTotal - discountAmount - pointDiscount);

            // Tính điểm tích lũy cho lần chơi này (1% tổng tiền trả)
            int pointsEarned = 0;
            if (session.Customer != null)
            {
                decimal earnRate = session.Customer.Tier?.PointEarnRate ?? 0.01m;
                pointsEarned = (int)(finalAmount * earnRate / 1000m);
                session.Customer.CurrentPoints += pointsEarned;
                session.Customer.TotalPointsAccumulated += pointsEarned;
                session.Customer.TotalSpent += finalAmount;
                session.Customer.LastVisitAt = now;

                // Kiểm tra tự động thăng hạng khách hàng
                CheckAndUpgradeTier(context, session.Customer);
            }

            // Tạo mã hóa đơn an toàn, tránh race condition trùng mã
            string baseCode = $"HD{now:yyyyMMdd}-";
            int seq = context.Invoices.Count(i => i.CreatedAt.Date == now.Date) + 1;
            string invoiceCode = $"{baseCode}{seq:D4}";
            while (context.Invoices.Any(i => i.InvoiceCode == invoiceCode))
            {
                seq++;
                invoiceCode = $"{baseCode}{seq:D4}";
            }

            // Tính mã băm bảo mật chống sửa trộm file SQLite (Tamper-Proof Hash)
            string timeStr = now.ToString("yyyy-MM-dd HH:mm:ss");
            long roundedFinal = (long)Math.Round(finalAmount);
            string tamperPayload = $"{invoiceCode}_{roundedFinal}_{timeStr}_{sessionId}_{cashierUserId}";
            string tamperHash = SecurityHelper.ComputeSha256(tamperPayload);

            // Xác định ca trực hiện tại để gắn ShiftId cho hóa đơn (Tránh mất bill khỏi doanh thu ca)
            int? activeShiftId = _shiftService?.CurrentActiveShift?.ShiftId;
            if (!activeShiftId.HasValue)
            {
                var openShift = context.Shifts.OrderByDescending(s => s.ShiftId).FirstOrDefault(s => s.Status == ShiftStatus.Open);
                activeShiftId = openShift?.ShiftId;
            }

            var invoice = new Invoice
            {
                InvoiceCode = invoiceCode,
                SessionId = sessionId,
                CustomerId = session.CustomerId,
                CashierUserId = cashierUserId,
                ShiftId = activeShiftId,
                AppliedComboName = session.ComboName,
                TotalPlayMinutes = playMinutes,
                TimeAmount = timeCost,
                ServiceAmount = serviceCost,
                DiscountPercent = safeDiscountPercent,
                DiscountAmount = discountAmount,
                PointsUsed = pointsActuallyUsed,
                PointsDiscountAmount = pointDiscount,
                FinalAmount = finalAmount,
                PointsEarned = pointsEarned,
                CreatedAt = now,
                Notes = notes,
                TamperProofHash = tamperHash
            };

            context.Invoices.Add(invoice);
            context.SaveChanges();

            // Thêm bản ghi phương thức thanh toán (Chuẩn 3NF)
            var payment = new InvoicePayment
            {
                InvoiceId = invoice.InvoiceId,
                Method = paymentMethod,
                Amount = finalAmount,
                TransactionReference = bankRef,
                CreatedAt = now
            };
            context.InvoicePayments.Add(payment);

            // Trả trạng thái bàn về Trống
            var table = context.Tables.Find(session.TableId);
            if (table != null)
            {
                table.Status = TableStatus.Available;
                table.CurrentSessionId = null;
            }

            context.SaveChanges();
            transaction.Commit();

            // Ghi Audit Log kiểm toán
            _auditService.Log(cashierUserId, "INVOICE_CHECKOUT", "Invoice", invoice.InvoiceId.ToString(), null, new
            {
                InvoiceCode = invoiceCode,
                Table = session.Table?.TableNumber ?? $"Bàn {session.TableId}",
                FinalAmount = finalAmount,
                PaymentMethod = paymentMethod.ToString()
            });

            return invoice;
        }
    }

    private void CheckAndUpgradeTier(AppDbContext context, Customer customer)
    {
        var tiers = context.MembershipTiers.AsEnumerable().OrderByDescending(t => t.MinSpentThreshold).ToList();
        foreach (var tier in tiers)
        {
            if (customer.TotalSpent >= tier.MinSpentThreshold)
            {
                customer.TierId = tier.TierId;
                break;
            }
        }
    }
}
