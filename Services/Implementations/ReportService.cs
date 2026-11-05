using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QL_JBillard.Data;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Services.DTOs;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.Services.Implementations;

public class ReportService : IReportService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public ReportService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public RevenueOverviewDto GetRevenueOverview(DateTime fromDate, DateTime toDate)
    {
        DateTime start = fromDate.Date;
        DateTime end = toDate.Date.AddDays(1).AddTicks(-1);

        using var context = _contextFactory.CreateDbContext();

        var invoices = context.Invoices
                              .Where(i => i.CreatedAt >= start && i.CreatedAt <= end)
                              .ToList();

        var dto = new RevenueOverviewDto
        {
            TotalRevenue = invoices.Sum(i => i.FinalAmount),
            TimeRevenue = invoices.Sum(i => i.TimeAmount),
            ServiceRevenue = invoices.Sum(i => i.ServiceAmount),
            TotalDiscounts = invoices.Sum(i => i.DiscountAmount + i.PointsDiscountAmount),
            TotalInvoices = invoices.Count,
            TotalPlayMinutes = invoices.Sum(i => i.TotalPlayMinutes)
        };

        // Tạo chuỗi điểm biểu đồ
        var points = new List<DailyChartPointDto>();
        int days = (end.Date - start.Date).Days + 1;

        if (days <= 1)
        {
            dto.IsHourlyView = true;
            // Theo toàn bộ 24 giờ trong ngày (thích nghi hoàn hảo câu lạc bộ mở 24/24 từ 00h đến 23h)
            for (int h = 0; h <= 23; h++)
            {
                var hourInvs = invoices.Where(i => i.CreatedAt.Hour == h).ToList();
                points.Add(new DailyChartPointDto
                {
                    Date = start.AddHours(h),
                    Label = $"{h:D2}h",
                    TimeRevenue = hourInvs.Sum(i => i.TimeAmount),
                    ServiceRevenue = hourInvs.Sum(i => i.ServiceAmount),
                    TotalRevenue = hourInvs.Sum(i => i.FinalAmount)
                });
            }

            // Phân chia 2 ca chuẩn xác cho quán bida 24/24:
            // 1. Ca Sáng & Chiều (06h - 17h: đúng 12 mốc giờ)
            dto.DayShiftPoints = points.Where(p => p.Date.Hour >= 6 && p.Date.Hour <= 17)
                                       .OrderBy(p => p.Date.Hour)
                                       .ToList();

            // 2. Ca Tối & Đêm (18h - 05h sáng hôm sau: đúng 12 mốc giờ)
            var nightLate = points.Where(p => p.Date.Hour >= 18 && p.Date.Hour <= 23).OrderBy(p => p.Date.Hour).ToList();
            var nightEarly = points.Where(p => p.Date.Hour >= 0 && p.Date.Hour <= 5).OrderBy(p => p.Date.Hour).ToList();
            dto.NightShiftPoints = nightLate.Concat(nightEarly).ToList();
        }
        else if (days <= 31)
        {
            dto.IsHourlyView = false;
            // Theo từng ngày
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
            {
                var dayInvs = invoices.Where(i => i.CreatedAt.Date == d).ToList();
                points.Add(new DailyChartPointDto
                {
                    Date = d,
                    Label = d.ToString("dd/MM"),
                    TimeRevenue = dayInvs.Sum(i => i.TimeAmount),
                    ServiceRevenue = dayInvs.Sum(i => i.ServiceAmount),
                    TotalRevenue = dayInvs.Sum(i => i.FinalAmount)
                });
            }

            // Nếu khoảng ngày từ 16 đến 31 ngày (ví dụ theo tháng): chia làm 2 nửa tháng (15 ngày đầu & các ngày còn lại)
            if (days > 15)
            {
                dto.IsSplitMonthView = true;
                dto.FirstHalfMonthPoints = points.Take(15).ToList();
                dto.SecondHalfMonthPoints = points.Skip(15).ToList();
                dto.FirstHalfTitle = $"📅 NỬA THÁNG ĐẦU ({points[0].Label} - {points[14].Label})";
                dto.SecondHalfTitle = $"📅 NỬA THÁNG SAU ({points[15].Label} - {points.Last().Label})";
            }
            else
            {
                dto.IsSplitMonthView = false;
            }
        }
        else
        {
            dto.IsHourlyView = false;
            dto.IsSplitMonthView = false;
            // Khoảng dài: gom theo từng tuần hoặc tháng
            var grouped = invoices.GroupBy(i => new { i.CreatedAt.Year, i.CreatedAt.Month })
                                  .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month);
            foreach (var g in grouped)
            {
                points.Add(new DailyChartPointDto
                {
                    Date = new DateTime(g.Key.Year, g.Key.Month, 1),
                    Label = $"Thg {g.Key.Month}/{g.Key.Year}",
                    TimeRevenue = g.Sum(i => i.TimeAmount),
                    ServiceRevenue = g.Sum(i => i.ServiceAmount),
                    TotalRevenue = g.Sum(i => i.FinalAmount)
                });
            }
        }

        decimal maxVal = points.Any() ? points.Max(p => p.TotalRevenue) : 0;
        foreach (var p in points)
        {
            p.NormalizedHeight = (maxVal > 0 && p.TotalRevenue > 0) ? Math.Max(4.0, (double)(p.TotalRevenue / maxVal) * 85.0) : 0;
        }

        dto.ChartPoints = points;
        return dto;
    }

    public List<TablePerformanceDto> GetTablePerformance(DateTime fromDate, DateTime toDate, int? areaId = null, string? search = null)
    {
        DateTime start = fromDate.Date;
        DateTime end = toDate.Date.AddDays(1).AddTicks(-1);

        using var context = _contextFactory.CreateDbContext();

        var tableQuery = context.Tables
                                .Include(t => t.TableType)
                                .Include(t => t.Area)
                                .AsQueryable();

        if (areaId.HasValue && areaId.Value > 0)
        {
            tableQuery = tableQuery.Where(t => t.AreaId == areaId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string kw = search.Trim().ToLower();
            tableQuery = tableQuery.Where(t => t.TableNumber.ToLower().Contains(kw));
        }

        var tables = tableQuery.ToList();

        var sessions = context.TableSessions
                              .Include(s => s.Invoice)
                              .Include(s => s.OrderItems)
                              .Where(s => s.EndTime.HasValue && s.EndTime >= start && s.EndTime <= end)
                              .ToList();

        var list = new List<TablePerformanceDto>();

        foreach (var table in tables)
        {
            var tableSessions = sessions.Where(s => s.TableId == table.TableId).ToList();
            var item = new TablePerformanceDto
            {
                TableId = table.TableId,
                TableNumber = table.TableNumber,
                TableTypeName = table.TableType?.TypeName ?? "Bàn",
                AreaName = table.Area?.AreaName ?? "Khu vực",
                TotalSessions = tableSessions.Count,
                TotalPlayMinutes = tableSessions.Sum(s => s.TotalPlayMinutes),
                TimeRevenue = tableSessions.Where(s => s.Invoice != null).Sum(s => s.Invoice!.TimeAmount),
                ServiceRevenue = tableSessions.Where(s => s.Invoice != null).Sum(s => s.Invoice!.ServiceAmount)
            };
            list.Add(item);
        }

        decimal maxRevenue = list.Any() ? list.Max(t => t.TotalRevenue) : 0;
        foreach (var t in list)
        {
            t.RelativePercentage = maxRevenue > 0 ? (double)(t.TotalRevenue / maxRevenue) * 100.0 : 0;
            if (t.TotalRevenue <= 0 && t.TotalSessions <= 0)
            {
                t.Rating = "THẤP";
                t.RatingBadgeText = "○ 0%";
                t.RatingBadgeBg = "#F1F5F9";
                t.RatingBadgeBorder = "#E2E8F0";
                t.RatingBadgeFg = "#64748B";
                t.RatingBarColor = "#CBD5E1";
            }
            else if (t.RelativePercentage >= 70)
            {
                t.Rating = "HOT";
                t.RatingBadgeText = $"★ HOT ({t.RelativePercentage:N0}%)";
                t.RatingBadgeBg = "#FEF2F2";
                t.RatingBadgeBorder = "#FECACA";
                t.RatingBadgeFg = "#DC2626";
                t.RatingBarColor = "#EF4444";
            }
            else if (t.RelativePercentage >= 40)
            {
                t.Rating = "CAO";
                t.RatingBadgeText = $"▲ Cao ({t.RelativePercentage:N0}%)";
                t.RatingBadgeBg = "#ECFDF5";
                t.RatingBadgeBorder = "#A7F3D0";
                t.RatingBadgeFg = "#059669";
                t.RatingBarColor = "#10B981";
            }
            else if (t.RelativePercentage >= 15)
            {
                t.Rating = "TRUNG BÌNH";
                t.RatingBadgeText = $"● T.Bình ({t.RelativePercentage:N0}%)";
                t.RatingBadgeBg = "#FFFBEB";
                t.RatingBadgeBorder = "#FDE68A";
                t.RatingBadgeFg = "#D97706";
                t.RatingBarColor = "#F59E0B";
            }
            else
            {
                t.Rating = "THẤP";
                t.RatingBadgeText = $"▼ Thấp ({t.RelativePercentage:N0}%)";
                t.RatingBadgeBg = "#F1F5F9";
                t.RatingBadgeBorder = "#CBD5E1";
                t.RatingBadgeFg = "#64748B";
                t.RatingBarColor = "#94A3B8";
            }
        }

        return list.OrderByDescending(t => t.TotalRevenue).ToList();
    }

    public List<ProductSalesDto> GetProductSales(DateTime fromDate, DateTime toDate, int? categoryId = null, string? search = null)
    {
        DateTime start = fromDate.Date;
        DateTime end = toDate.Date.AddDays(1).AddTicks(-1);

        using var context = _contextFactory.CreateDbContext();

        var query = context.OrderItems
                           .Include(o => o.Product)
                           .ThenInclude(p => p.Category)
                           .Where(o => o.IsCompleted && !o.IsCancelled && o.OrderedAt >= start && o.OrderedAt <= end);

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(o => o.Product != null && o.Product.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string kw = search.Trim().ToLower();
            query = query.Where(o => o.Product != null && (o.Product.ProductName.ToLower().Contains(kw) || o.Product.ProductCode.ToLower().Contains(kw)));
        }

        var items = query.ToList();

        var grouped = items.GroupBy(o => o.ProductId).ToList();
        var list = new List<ProductSalesDto>();
        decimal grandTotal = items.Sum(o => o.TotalAmount);

        foreach (var g in grouped)
        {
            var prod = g.First().Product;
            int qty = g.Sum(x => x.Quantity);
            decimal totalRev = g.Sum(x => x.TotalAmount);

            list.Add(new ProductSalesDto
            {
                ProductId = g.Key,
                ProductName = prod?.ProductName ?? "Sản phẩm",
                CategoryName = prod?.Category?.CategoryName ?? "F&B",
                QuantitySold = qty,
                SellingPrice = prod?.SellingPrice ?? 0,
                TotalRevenue = totalRev,
                PercentageOfTotal = grandTotal > 0 ? (double)(totalRev / grandTotal) * 100.0 : 0
            });
        }

        int maxQty = list.Any() ? list.Max(p => p.QuantitySold) : 0;
        foreach (var p in list)
        {
            p.RelativePercentage = maxQty > 0 ? (double)p.QuantitySold / maxQty * 100.0 : 0;
            if (p.QuantitySold <= 0)
            {
                p.Rating = "THẤP";
                p.RatingBadgeText = "○ 0%";
                p.RatingBadgeBg = "#F1F5F9";
                p.RatingBadgeBorder = "#E2E8F0";
                p.RatingBadgeFg = "#64748B";
                p.RatingBarColor = "#CBD5E1";
            }
            else if (p.RelativePercentage >= 70)
            {
                p.Rating = "HOT";
                p.RatingBadgeText = $"★ HOT ({p.RelativePercentage:N0}%)";
                p.RatingBadgeBg = "#FEF2F2";
                p.RatingBadgeBorder = "#FECACA";
                p.RatingBadgeFg = "#DC2626";
                p.RatingBarColor = "#EF4444";
            }
            else if (p.RelativePercentage >= 40)
            {
                p.Rating = "CAO";
                p.RatingBadgeText = $"▲ Cao ({p.RelativePercentage:N0}%)";
                p.RatingBadgeBg = "#ECFDF5";
                p.RatingBadgeBorder = "#A7F3D0";
                p.RatingBadgeFg = "#059669";
                p.RatingBarColor = "#10B981";
            }
            else if (p.RelativePercentage >= 15)
            {
                p.Rating = "TRUNG BÌNH";
                p.RatingBadgeText = $"● T.Bình ({p.RelativePercentage:N0}%)";
                p.RatingBadgeBg = "#FFFBEB";
                p.RatingBadgeBorder = "#FDE68A";
                p.RatingBadgeFg = "#D97706";
                p.RatingBarColor = "#F59E0B";
            }
            else
            {
                p.Rating = "THẤP";
                p.RatingBadgeText = $"▼ Thấp ({p.RelativePercentage:N0}%)";
                p.RatingBadgeBg = "#F1F5F9";
                p.RatingBadgeBorder = "#CBD5E1";
                p.RatingBadgeFg = "#64748B";
                p.RatingBarColor = "#94A3B8";
            }
        }

        return list.OrderByDescending(p => p.QuantitySold).ThenByDescending(p => p.TotalRevenue).ToList();
    }

    public PeriodComparisonDto GetPeriodComparison(ComparisonPeriodMode mode)
    {
        DateTime now = DateTime.Now;
        DateTime currentStart, currentEnd, prevStart, prevEnd;
        string modeTitle, currLabel, prevLabel;
        var points = new List<ComparisonBarPointDto>();

        switch (mode)
        {
            case ComparisonPeriodMode.TodayVsYesterday:
                modeTitle = "Hôm Nay so với Hôm Qua";
                currLabel = $"Hôm Nay ({now:dd/MM})";
                prevLabel = $"Hôm Qua ({now.AddDays(-1):dd/MM})";
                currentStart = now.Date;
                currentEnd = now.Date.AddDays(1).AddTicks(-1);
                prevStart = now.Date.AddDays(-1);
                prevEnd = now.Date.AddTicks(-1);

                // 4 khung giờ
                string[] blocks = { "Sáng (08-12h)", "Chiều (12-17h)", "Tối (17-21h)", "Đêm (21-24h)" };
                int[,] hours = { { 8, 12 }, { 12, 17 }, { 17, 21 }, { 21, 24 } };

                using (var ctx = _contextFactory.CreateDbContext())
                {
                    var currInvs = ctx.Invoices.Where(i => i.CreatedAt >= currentStart && i.CreatedAt <= currentEnd).ToList();
                    var prevInvs = ctx.Invoices.Where(i => i.CreatedAt >= prevStart && i.CreatedAt <= prevEnd).ToList();

                    for (int b = 0; b < 4; b++)
                    {
                        int h1 = hours[b, 0], h2 = hours[b, 1];
                        decimal cVal = currInvs.Where(i => i.CreatedAt.Hour >= h1 && i.CreatedAt.Hour < h2).Sum(i => i.FinalAmount);
                        decimal pVal = prevInvs.Where(i => i.CreatedAt.Hour >= h1 && i.CreatedAt.Hour < h2).Sum(i => i.FinalAmount);
                        points.Add(new ComparisonBarPointDto { Label = blocks[b], ValueCurrent = cVal, ValuePrevious = pVal });
                    }
                }
                break;

            case ComparisonPeriodMode.ThisWeekVsLastWeek:
            case ComparisonPeriodMode.ThisWeekVsTwoWeeksAgo:
                int daysBack = (mode == ComparisonPeriodMode.ThisWeekVsLastWeek) ? 7 : 14;
                int diffToMonday = (int)now.DayOfWeek == 0 ? 6 : (int)now.DayOfWeek - 1;
                DateTime currentMonday = now.Date.AddDays(-diffToMonday);
                currentStart = currentMonday;
                currentEnd = currentMonday.AddDays(7).AddTicks(-1);

                DateTime prevMonday = currentMonday.AddDays(-daysBack);
                prevStart = prevMonday;
                prevEnd = prevMonday.AddDays(7).AddTicks(-1);

                modeTitle = mode == ComparisonPeriodMode.ThisWeekVsLastWeek 
                    ? "Tuần Này so với Tuần Trước" 
                    : "Tuần Này so với 2 Tuần Trước";
                currLabel = $"Tuần này ({currentStart:dd/MM} - {currentStart.AddDays(6):dd/MM})";
                prevLabel = $"Kỳ trước ({prevStart:dd/MM} - {prevStart.AddDays(6):dd/MM})";

                string[] dayNames = { "Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7", "Chủ Nhật" };

                using (var ctx = _contextFactory.CreateDbContext())
                {
                    var currInvs = ctx.Invoices.Where(i => i.CreatedAt >= currentStart && i.CreatedAt <= currentEnd).ToList();
                    var prevInvs = ctx.Invoices.Where(i => i.CreatedAt >= prevStart && i.CreatedAt <= prevEnd).ToList();

                    for (int i = 0; i < 7; i++)
                    {
                        var cDate = currentStart.AddDays(i).Date;
                        var pDate = prevStart.AddDays(i).Date;
                        decimal cVal = currInvs.Where(inv => inv.CreatedAt.Date == cDate).Sum(inv => inv.FinalAmount);
                        decimal pVal = prevInvs.Where(inv => inv.CreatedAt.Date == pDate).Sum(inv => inv.FinalAmount);
                        points.Add(new ComparisonBarPointDto { Label = dayNames[i], ValueCurrent = cVal, ValuePrevious = pVal });
                    }
                }
                break;

            case ComparisonPeriodMode.ThisMonthVsLastMonth:
            default:
                modeTitle = "Tháng Này so với Tháng Trước";
                currentStart = new DateTime(now.Year, now.Month, 1);
                currentEnd = currentStart.AddMonths(1).AddTicks(-1);

                DateTime lastMonth = currentStart.AddMonths(-1);
                prevStart = new DateTime(lastMonth.Year, lastMonth.Month, 1);
                prevEnd = currentStart.AddTicks(-1);

                currLabel = $"Tháng {now.Month}/{now.Year}";
                prevLabel = $"Tháng {lastMonth.Month}/{lastMonth.Year}";

                using (var ctx = _contextFactory.CreateDbContext())
                {
                    var currInvs = ctx.Invoices.Where(i => i.CreatedAt >= currentStart && i.CreatedAt <= currentEnd).ToList();
                    var prevInvs = ctx.Invoices.Where(i => i.CreatedAt >= prevStart && i.CreatedAt <= prevEnd).ToList();

                    // Chia 4 tuần
                    for (int w = 1; w <= 4; w++)
                    {
                        int dStart = (w - 1) * 7 + 1;
                        int dEnd = w == 4 ? 31 : w * 7;
                        decimal cVal = currInvs.Where(i => i.CreatedAt.Day >= dStart && i.CreatedAt.Day <= dEnd).Sum(i => i.FinalAmount);
                        decimal pVal = prevInvs.Where(i => i.CreatedAt.Day >= dStart && i.CreatedAt.Day <= dEnd).Sum(i => i.FinalAmount);
                        points.Add(new ComparisonBarPointDto { Label = $"Tuần {w}", ValueCurrent = cVal, ValuePrevious = pVal });
                    }
                }
                break;
        }

        // Tính tổng
        using var context = _contextFactory.CreateDbContext();
        var cInvoices = context.Invoices.Where(i => i.CreatedAt >= currentStart && i.CreatedAt <= currentEnd).ToList();
        var pInvoices = context.Invoices.Where(i => i.CreatedAt >= prevStart && i.CreatedAt <= prevEnd).ToList();

        decimal maxBar = points.Any() ? points.Max(p => Math.Max(p.ValueCurrent, p.ValuePrevious)) : 0;
        foreach (var p in points)
        {
            p.HeightCurrent = maxBar > 0 ? (double)(p.ValueCurrent / maxBar) * 140.0 : 0;
            p.HeightPrevious = maxBar > 0 ? (double)(p.ValuePrevious / maxBar) * 140.0 : 0;
        }

        return new PeriodComparisonDto
        {
            PeriodModeTitle = modeTitle,
            CurrentPeriodLabel = currLabel,
            PreviousPeriodLabel = prevLabel,
            CurrentRevenue = cInvoices.Sum(i => i.FinalAmount),
            PreviousRevenue = pInvoices.Sum(i => i.FinalAmount),
            CurrentTimeRevenue = cInvoices.Sum(i => i.TimeAmount),
            PreviousTimeRevenue = pInvoices.Sum(i => i.TimeAmount),
            CurrentServiceRevenue = cInvoices.Sum(i => i.ServiceAmount),
            PreviousServiceRevenue = pInvoices.Sum(i => i.ServiceAmount),
            CurrentInvoices = cInvoices.Count,
            PreviousInvoices = pInvoices.Count,
            ComparisonPoints = points
        };
    }

    public List<DetailedRevenueRowDto> GetDetailedRevenueData(DateTime fromDate, DateTime toDate)
    {
        DateTime start = fromDate.Date;
        DateTime end = toDate.Date.AddDays(1).AddTicks(-1);

        using var context = _contextFactory.CreateDbContext();

        var invoices = context.Invoices
            .Include(i => i.CashierUser)
            .Include(i => i.Customer)
                .ThenInclude(c => c!.Tier)
            .Include(i => i.Payments)
            .Include(i => i.Session)
                .ThenInclude(s => s.Table)
                    .ThenInclude(t => t.TableType)
            .Include(i => i.Session)
                .ThenInclude(s => s.Table)
                    .ThenInclude(t => t.Area)
                        .ThenInclude(a => a.Floor)
            .Include(i => i.Session)
                .ThenInclude(s => s.OrderItems)
                    .ThenInclude(o => o.Product)
                        .ThenInclude(p => p.Category)
            .Where(i => i.CreatedAt >= start && i.CreatedAt <= end)
            .OrderByDescending(i => i.CreatedAt)
            .ToList();

        var result = new List<DetailedRevenueRowDto>();

        foreach (var inv in invoices)
        {
            var session = inv.Session;
            var table = session?.Table;
            var cust = inv.Customer ?? session?.Customer;

            string custType = cust != null ? "Hội Viên" : "Khách Vãng Lai";
            string custName = cust?.FullName ?? "Khách Vãng Lai";
            string custPhone = cust?.PhoneNumber ?? "";
            string tierName = cust?.Tier?.TierName ?? (cust != null ? "Thường" : "Không");

            string paymentMethods = inv.Payments.Any()
                ? string.Join(", ", inv.Payments.Select(p => p.Method switch
                {
                    PaymentMethod.Cash => "Tiền mặt",
                    PaymentMethod.VietQr => "VietQR",
                    PaymentMethod.BankTransfer => "Chuyển khoản",
                    PaymentMethod.Points => "Điểm tích lũy",
                    PaymentMethod.Mixed => "Hỗn hợp",
                    _ => p.Method.ToString()
                }))
                : "Tiền mặt";

            var activeOrderItems = session?.OrderItems != null
                ? session.OrderItems.Where(o => !o.IsCancelled).ToList()
                : new List<OrderItem>();

            if (activeOrderItems.Any())
            {
                foreach (var item in activeOrderItems)
                {
                    result.Add(new DetailedRevenueRowDto
                    {
                        InvoiceCode = inv.InvoiceCode,
                        CreatedAt = inv.CreatedAt,
                        ShiftId = inv.ShiftId,
                        CashierName = inv.CashierUser?.FullName ?? "Thu Ngân",
                        TableNumber = table?.TableNumber ?? "Bàn",
                        TableType = table?.TableType?.TypeName ?? "Bida",
                        AreaName = table?.Area?.AreaName ?? "Khu vực",
                        FloorName = table?.Area?.Floor?.FloorName ?? "Tầng",
                        StartTime = session?.StartTime,
                        EndTime = session?.EndTime ?? inv.CreatedAt,
                        PlayMinutes = inv.TotalPlayMinutes,
                        TimeAmount = inv.TimeAmount,
                        AppliedComboName = inv.AppliedComboName ?? session?.ComboName ?? "",
                        CustomerType = custType,
                        CustomerName = custName,
                        CustomerPhone = custPhone,
                        MembershipTier = tierName,
                        PointsEarned = inv.PointsEarned,
                        PointsUsed = inv.PointsUsed,
                        ItemName = item.Product?.ProductName ?? "Món F&B",
                        ItemCategory = item.Product?.Category?.CategoryName ?? "Dịch vụ",
                        ItemQuantity = item.Quantity,
                        ItemUnitPrice = item.UnitPrice,
                        ItemTotalAmount = item.TotalAmount,
                        IsComboItem = item.IsComboItem,
                        ServiceAmount = inv.ServiceAmount,
                        DiscountPercent = inv.DiscountPercent,
                        DiscountAmount = inv.DiscountAmount,
                        PointsDiscountAmount = inv.PointsDiscountAmount,
                        InvoiceFinalAmount = inv.FinalAmount,
                        PaymentMethod = paymentMethods
                    });
                }
            }
            else
            {
                result.Add(new DetailedRevenueRowDto
                {
                    InvoiceCode = inv.InvoiceCode,
                    CreatedAt = inv.CreatedAt,
                    ShiftId = inv.ShiftId,
                    CashierName = inv.CashierUser?.FullName ?? "Thu Ngân",
                    TableNumber = table?.TableNumber ?? "Bàn",
                    TableType = table?.TableType?.TypeName ?? "Bida",
                    AreaName = table?.Area?.AreaName ?? "Khu vực",
                    FloorName = table?.Area?.Floor?.FloorName ?? "Tầng",
                    StartTime = session?.StartTime,
                    EndTime = session?.EndTime ?? inv.CreatedAt,
                    PlayMinutes = inv.TotalPlayMinutes,
                    TimeAmount = inv.TimeAmount,
                    AppliedComboName = inv.AppliedComboName ?? session?.ComboName ?? "",
                    CustomerType = custType,
                    CustomerName = custName,
                    CustomerPhone = custPhone,
                    MembershipTier = tierName,
                    PointsEarned = inv.PointsEarned,
                    PointsUsed = inv.PointsUsed,
                    ItemName = "[Chỉ Chơi Giờ - Không Dùng F&B]",
                    ItemCategory = "Tiền Giờ Bida",
                    ItemQuantity = 1,
                    ItemUnitPrice = 0,
                    ItemTotalAmount = 0,
                    IsComboItem = false,
                    ServiceAmount = inv.ServiceAmount,
                    DiscountPercent = inv.DiscountPercent,
                    DiscountAmount = inv.DiscountAmount,
                    PointsDiscountAmount = inv.PointsDiscountAmount,
                    InvoiceFinalAmount = inv.FinalAmount,
                    PaymentMethod = paymentMethods
                });
            }
        }

        return result;
    }
}
