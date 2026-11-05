using System;
using System.Collections.Generic;

namespace QL_JBillard.Services.DTOs;

public class DailyChartPointDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;       // "01/11", "T2", "14h"
    public decimal TimeRevenue { get; set; }
    public decimal ServiceRevenue { get; set; }
    public decimal TotalRevenue { get; set; }
    
    // Thuộc tính hỗ trợ vẽ biểu đồ XAML Canvas / Polyline
    public double NormalizedHeight { get; set; }           // 0..100%

    public string TotalRevenueFormatted => TotalRevenue > 0 ? $"{TotalRevenue:N0} đ" : "0 đ";

    public string TotalRevenueCompact
    {
        get
        {
            if (TotalRevenue <= 0) return "0 đ";
            if (TotalRevenue >= 1_000_000m)
            {
                decimal tr = TotalRevenue / 1_000_000m;
                return tr >= 10m ? $"{tr:F1} tr" : $"{tr:F2} tr";
            }
            if (TotalRevenue >= 1_000m)
            {
                return $"{TotalRevenue / 1_000m:N0} k";
            }
            return $"{TotalRevenue:N0} đ";
        }
    }
}

public class RevenueOverviewDto
{
    public decimal TotalRevenue { get; set; }
    public decimal TimeRevenue { get; set; }
    public decimal ServiceRevenue { get; set; }
    public decimal TotalDiscounts { get; set; }
    public int TotalInvoices { get; set; }
    public int TotalPlayMinutes { get; set; }
    public decimal AverageBillValue => TotalInvoices > 0 ? TotalRevenue / TotalInvoices : 0;
    
    public bool IsHourlyView { get; set; }
    public bool IsSplitMonthView { get; set; }
    public bool IsStandardChartView => !IsHourlyView && !IsSplitMonthView;

    public List<DailyChartPointDto> ChartPoints { get; set; } = new();

    // Phân chia 2 ca cho quán bida 24/24h: Ca Sáng (06h - 17h) và Ca Tối/Đêm (18h - 05h sáng)
    public List<DailyChartPointDto> DayShiftPoints { get; set; } = new();
    public List<DailyChartPointDto> NightShiftPoints { get; set; } = new();
    public decimal DayShiftRevenue => DayShiftPoints.Sum(p => p.TotalRevenue);
    public decimal NightShiftRevenue => NightShiftPoints.Sum(p => p.TotalRevenue);

    // Phân chia 2 nửa tháng (15 ngày đầu & các ngày còn lại) khi xem theo tháng
    public List<DailyChartPointDto> FirstHalfMonthPoints { get; set; } = new();
    public List<DailyChartPointDto> SecondHalfMonthPoints { get; set; } = new();
    public decimal FirstHalfMonthRevenue => FirstHalfMonthPoints.Sum(p => p.TotalRevenue);
    public decimal SecondHalfMonthRevenue => SecondHalfMonthPoints.Sum(p => p.TotalRevenue);
    public string FirstHalfTitle { get; set; } = "📅 NỬA ĐẦU THÁNG (Ngày 01 - 15)";
    public string SecondHalfTitle { get; set; } = "📅 NỬA CUỐI THÁNG (Ngày 16 - Cuối tháng)";
}

public class TablePerformanceDto
{
    public int TableId { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string TableTypeName { get; set; } = string.Empty;
    public string AreaName { get; set; } = string.Empty;
    public int TotalSessions { get; set; }
    public int TotalPlayMinutes { get; set; }
    public string PlayTimeFormatted => $"{TotalPlayMinutes / 60}h {TotalPlayMinutes % 60}m";
    public decimal TimeRevenue { get; set; }
    public decimal ServiceRevenue { get; set; }
    public decimal TotalRevenue => TimeRevenue + ServiceRevenue;
    
    // Thuộc tính tỷ lệ cột biểu đồ
    public double RelativePercentage { get; set; } // 0..100% so với bàn cao nhất

    // Đánh giá trực quan khách quan: Thấp / Trung Bình / Cao / HOT
    public string Rating { get; set; } = "THẤP";
    public string RatingBadgeText { get; set; } = "⚪ Thấp";
    public string RatingBadgeBg { get; set; } = "#F1F5F9";
    public string RatingBadgeBorder { get; set; } = "#CBD5E1";
    public string RatingBadgeFg { get; set; } = "#64748B";
    public string RatingBarColor { get; set; } = "#94A3B8";
}

public class ProductSalesDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal TotalRevenue { get; set; }
    public double PercentageOfTotal { get; set; }
    public double RelativePercentage { get; set; } // 0..100% cho thanh bar biểu đồ

    // Đánh giá trực quan khách quan mức độ bán chạy: Thấp / Trung Bình / Cao / HOT
    public string Rating { get; set; } = "THẤP";
    public string RatingBadgeText { get; set; } = "⚪ Thấp";
    public string RatingBadgeBg { get; set; } = "#F1F5F9";
    public string RatingBadgeBorder { get; set; } = "#CBD5E1";
    public string RatingBadgeFg { get; set; } = "#64748B";
    public string RatingBarColor { get; set; } = "#94A3B8";
}

public enum ComparisonPeriodMode
{
    TodayVsYesterday,
    ThisWeekVsLastWeek,
    ThisWeekVsTwoWeeksAgo,
    ThisMonthVsLastMonth
}

public class ComparisonBarPointDto
{
    public string Label { get; set; } = string.Empty;
    public decimal ValueCurrent { get; set; }
    public decimal ValuePrevious { get; set; }
    
    // Chiều cao chuẩn hóa (0..140 px)
    public double HeightCurrent { get; set; }
    public double HeightPrevious { get; set; }

    public static string FormatCompact(decimal val)
    {
        if (val <= 0) return "0 đ";
        if (val >= 1_000_000m)
        {
            decimal tr = val / 1_000_000m;
            return tr >= 10m ? $"{tr:F1} tr" : $"{tr:F2} tr";
        }
        if (val >= 1_000m)
        {
            return $"{val / 1_000m:N0} k";
        }
        return $"{val:N0} đ";
    }

    public string ValueCurrentFormatted => FormatCompact(ValueCurrent);
    public string ValuePreviousFormatted => FormatCompact(ValuePrevious);
    public string ValueCurrentFullFormatted => ValueCurrent > 0 ? $"{ValueCurrent:N0} đ" : "0 đ";
    public string ValuePreviousFullFormatted => ValuePrevious > 0 ? $"{ValuePrevious:N0} đ" : "0 đ";
}

public class PeriodComparisonDto
{
    public string PeriodModeTitle { get; set; } = string.Empty;
    public string CurrentPeriodLabel { get; set; } = string.Empty;
    public string PreviousPeriodLabel { get; set; } = string.Empty;
    
    public decimal CurrentRevenue { get; set; }
    public decimal PreviousRevenue { get; set; }
    public decimal RevenueDifference => CurrentRevenue - PreviousRevenue;
    public double RevenueGrowthPercent => PreviousRevenue > 0 
        ? (double)((CurrentRevenue - PreviousRevenue) / PreviousRevenue) * 100 
        : (CurrentRevenue > 0 ? 100 : 0);

    public decimal CurrentTimeRevenue { get; set; }
    public decimal PreviousTimeRevenue { get; set; }

    public decimal CurrentServiceRevenue { get; set; }
    public decimal PreviousServiceRevenue { get; set; }

    public int CurrentInvoices { get; set; }
    public int PreviousInvoices { get; set; }
    public int InvoicesDiff => CurrentInvoices - PreviousInvoices;

    public List<ComparisonBarPointDto> ComparisonPoints { get; set; } = new();
}

public class DetailedRevenueRowDto
{
    public string InvoiceCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string DateStr => CreatedAt.ToString("dd/MM/yyyy");
    public string TimeStr => CreatedAt.ToString("HH:mm:ss");
    public string HourSlot => $"{CreatedAt.Hour:D2}:00";
    public int? ShiftId { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public string TableNumber { get; set; } = string.Empty;
    public string TableType { get; set; } = string.Empty;
    public string AreaName { get; set; } = string.Empty;
    public string FloorName { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int PlayMinutes { get; set; }
    public decimal TimeAmount { get; set; }
    public string AppliedComboName { get; set; } = string.Empty;
    public string CustomerType { get; set; } = "Khách Vãng Lai"; // "Hội Viên" hoặc "Khách Vãng Lai"
    public string CustomerName { get; set; } = "Khách Vãng Lai";
    public string CustomerPhone { get; set; } = string.Empty;
    public string MembershipTier { get; set; } = "Không";
    public int PointsEarned { get; set; }
    public int PointsUsed { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ItemCategory { get; set; } = string.Empty;
    public int ItemQuantity { get; set; }
    public decimal ItemUnitPrice { get; set; }
    public decimal ItemTotalAmount { get; set; }
    public bool IsComboItem { get; set; }
    public decimal ServiceAmount { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal PointsDiscountAmount { get; set; }
    public decimal InvoiceFinalAmount { get; set; }
    public string PaymentMethod { get; set; } = "Tiền mặt";
}
