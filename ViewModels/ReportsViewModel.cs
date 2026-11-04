using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Services.DTOs;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.ViewModels;

public partial class ReportsViewModel : ObservableObject
{
    private readonly IReportService _reportService;
    private readonly IInventoryService? _inventoryService;
    private readonly ITableService? _tableService;

    [ObservableProperty]
    private string _activeTab = "OVERVIEW"; // OVERVIEW, TABLES, PRODUCTS, COMPARISON

    [ObservableProperty]
    private string _selectedPreset = "THIS_WEEK"; // TODAY, THIS_WEEK, THIS_MONTH, CUSTOM

    [ObservableProperty]
    private DateTime _fromDate = DateTime.Now.AddDays(-7);

    [ObservableProperty]
    private DateTime _toDate = DateTime.Now;

    [ObservableProperty]
    private RevenueOverviewDto _overview = new();

    // ── Dữ liệu & bộ lọc Báo cáo theo Bàn ──
    [ObservableProperty]
    private ObservableCollection<TablePerformanceDto> _tableReports = new();

    [ObservableProperty]
    private ObservableCollection<Area> _areas = new();

    [ObservableProperty]
    private Area? _selectedArea;

    [ObservableProperty]
    private string _tableSearchKeyword = string.Empty;

    [ObservableProperty]
    private decimal _totalTablesRevenue;

    [ObservableProperty]
    private int _totalTablesSessions;

    [ObservableProperty]
    private string _topTableNumber = "Chưa có";

    // ── Dữ liệu & bộ lọc Báo cáo theo Món F&B ──
    [ObservableProperty]
    private ObservableCollection<ProductSalesDto> _productReports = new();

    [ObservableProperty]
    private ObservableCollection<ProductCategory> _categories = new();

    [ObservableProperty]
    private ProductCategory? _selectedCategory;

    [ObservableProperty]
    private string _productSearchKeyword = string.Empty;

    [ObservableProperty]
    private int _totalProductsSold;

    [ObservableProperty]
    private decimal _totalProductRevenue;

    [ObservableProperty]
    private int _totalProductItemsCount;

    [ObservableProperty]
    private string _bestSellingProductName = "Chưa có";

    // ── Dữ liệu So sánh kỳ ──
    [ObservableProperty]
    private PeriodComparisonDto _comparisonReport = new();

    [ObservableProperty]
    private ComparisonPeriodMode _selectedComparisonMode = ComparisonPeriodMode.ThisWeekVsLastWeek;

    // Tọa độ vẽ biểu đồ đường (Line Chart Points cho XAML Polyline)
    [ObservableProperty]
    private PointCollection _lineChartPoints = new();

    [ObservableProperty]
    private PointCollection _areaChartPoints = new();

    // Nhãn trục X và Y cho Biểu đồ đường (Line Chart)
    [ObservableProperty]
    private string _chartYMaxLabel = "0 đ";

    [ObservableProperty]
    private string _chartY75Label = "0 đ";

    [ObservableProperty]
    private string _chartYMidLabel = "0 đ";

    [ObservableProperty]
    private string _chartY25Label = "0 đ";

    [ObservableProperty]
    private string _chartYZeroLabel = "0 đ";

    [ObservableProperty]
    private string _chartXAxisTitle = "▶ Thời gian (Mốc giờ 24/24h / Ngày)";

    [ObservableProperty]
    private ObservableCollection<ChartAxisTick> _chartXTicks = new();

    // Nhãn trục Y cho Biểu đồ cột kép so sánh
    [ObservableProperty]
    private string _comparisonYMaxLabel = "0 đ";

    [ObservableProperty]
    private string _comparisonYMidLabel = "0 đ";

    [ObservableProperty]
    private string _comparisonYZeroLabel = "0 đ";

    public ReportsViewModel(
        IReportService reportService,
        IInventoryService? inventoryService = null,
        ITableService? tableService = null)
    {
        _reportService = reportService;
        _inventoryService = inventoryService;
        _tableService = tableService;

        LoadFilterCatalogs();
        ApplyPreset("THIS_WEEK");
    }

    private void LoadFilterCatalogs()
    {
        // Danh mục F&B
        var catList = new ObservableCollection<ProductCategory>
        {
            new ProductCategory { CategoryId = 0, CategoryName = "── Tất Cả Danh Mục ──" }
        };
        if (_inventoryService != null)
        {
            try
            {
                var dbCats = _inventoryService.GetAllCategories();
                foreach (var c in dbCats) catList.Add(c);
            }
            catch { }
        }
        Categories = catList;
        SelectedCategory = catList[0];

        // Khu vực bàn
        var areaList = new ObservableCollection<Area>
        {
            new Area { AreaId = 0, AreaName = "── Tất Cả Khu Vực ──" }
        };
        if (_tableService != null)
        {
            try
            {
                var dbAreas = _tableService.GetAllAreas();
                foreach (var a in dbAreas) areaList.Add(a);
            }
            catch { }
        }
        Areas = areaList;
        SelectedArea = areaList[0];
    }

    [RelayCommand]
    private void SetTab(string tab)
    {
        ActiveTab = tab;
        LoadCurrentTab();
    }

    [RelayCommand]
    private void SetPreset(string preset)
    {
        SelectedPreset = preset;
        ApplyPreset(preset);
    }

    private void ApplyPreset(string preset)
    {
        var now = DateTime.Now;
        switch (preset)
        {
            case "TODAY":
                FromDate = now.Date;
                ToDate = now.Date;
                break;
            case "THIS_WEEK":
                int diff = (int)now.DayOfWeek == 0 ? 6 : (int)now.DayOfWeek - 1;
                FromDate = now.Date.AddDays(-diff);
                ToDate = now.Date;
                break;
            case "THIS_MONTH":
                FromDate = new DateTime(now.Year, now.Month, 1);
                ToDate = now.Date;
                break;
            case "CUSTOM":
                break;
        }
        LoadCurrentTab();
    }

    partial void OnFromDateChanged(DateTime value)
    {
        if (SelectedPreset == "CUSTOM") LoadCurrentTab();
    }

    partial void OnToDateChanged(DateTime value)
    {
        if (SelectedPreset == "CUSTOM") LoadCurrentTab();
    }

    partial void OnSelectedComparisonModeChanged(ComparisonPeriodMode value)
    {
        LoadComparison();
    }

    partial void OnSelectedCategoryChanged(ProductCategory? value)
    {
        if (ActiveTab == "PRODUCTS") LoadProducts();
    }

    partial void OnProductSearchKeywordChanged(string value)
    {
        if (ActiveTab == "PRODUCTS") LoadProducts();
    }

    partial void OnSelectedAreaChanged(Area? value)
    {
        if (ActiveTab == "TABLES") LoadTables();
    }

    partial void OnTableSearchKeywordChanged(string value)
    {
        if (ActiveTab == "TABLES") LoadTables();
    }

    [RelayCommand]
    private void ResetProductFilter()
    {
        ProductSearchKeyword = string.Empty;
        if (Categories.Any()) SelectedCategory = Categories[0];
        LoadProducts();
    }

    [RelayCommand]
    private void ResetTableFilter()
    {
        TableSearchKeyword = string.Empty;
        if (Areas.Any()) SelectedArea = Areas[0];
        LoadTables();
    }

    [RelayCommand]
    public void Refresh()
    {
        LoadCurrentTab();
    }

    [RelayCommand]
    public void ExportDetailedRevenueData()
    {
        try
        {
            var data = _reportService.GetDetailedRevenueData(FromDate, ToDate);
            if (data == null || !data.Any())
            {
                MessageBox.Show("Không có dữ liệu hóa đơn nào trong khoảng thời gian đã chọn để xuất báo cáo.", 
                    "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Title = "Xuất Dữ Liệu Doanh Thu Chi Tiết (Data Analyst)",
                Filter = "File Excel / CSV (*.csv)|*.csv|All Files (*.*)|*.*",
                FileName = $"BaoCao_DoanhThu_ChiTiet_JBillard_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Mã Hóa Đơn,Ngày,Giờ,Khung Giờ,Ca Trực,Thu Ngân,Số Bàn,Loại Bàn,Khu Vực,Tầng,Giờ Bắt Đầu,Giờ Kết Thúc,Thời Gian Chơi (Phút),Tiền Giờ (VNĐ),Gói Combo,Loại Khách Hàng,Tên Khách Hàng,Số Điện Thoại,Hạng Thành Viên,Điểm Tích Lũy,Điểm Đã Tiêu,Tên Món F&B,Nhóm Món,Số Lượng,Đơn Giá (VNĐ),Thành Tiền Món (VNĐ),Món Thuộc Combo,Tiền Dịch Vụ (VNĐ),Giảm Giá (%),Tiền Giảm Trừ (VNĐ),Giảm Trừ Điểm (VNĐ),Tổng Tiền Hóa Đơn (VNĐ),Hình Thức Thanh Toán");

                foreach (var row in data)
                {
                    sb.AppendLine(string.Join(",",
                        EscapeCsv(row.InvoiceCode),
                        EscapeCsv(row.DateStr),
                        EscapeCsv(row.TimeStr),
                        EscapeCsv(row.HourSlot),
                        EscapeCsv(row.ShiftId?.ToString() ?? "N/A"),
                        EscapeCsv(row.CashierName),
                        EscapeCsv(row.TableNumber),
                        EscapeCsv(row.TableType),
                        EscapeCsv(row.AreaName),
                        EscapeCsv(row.FloorName),
                        EscapeCsv(row.StartTime?.ToString("dd/MM/yyyy HH:mm:ss") ?? ""),
                        EscapeCsv(row.EndTime?.ToString("dd/MM/yyyy HH:mm:ss") ?? ""),
                        row.PlayMinutes,
                        row.TimeAmount,
                        EscapeCsv(row.AppliedComboName),
                        EscapeCsv(row.CustomerType),
                        EscapeCsv(row.CustomerName),
                        EscapeCsv(row.CustomerPhone),
                        EscapeCsv(row.MembershipTier),
                        row.PointsEarned,
                        row.PointsUsed,
                        EscapeCsv(row.ItemName),
                        EscapeCsv(row.ItemCategory),
                        row.ItemQuantity,
                        row.ItemUnitPrice,
                        row.ItemTotalAmount,
                        row.IsComboItem ? "Có" : "Không",
                        row.ServiceAmount,
                        row.DiscountPercent,
                        row.DiscountAmount,
                        row.PointsDiscountAmount,
                        row.InvoiceFinalAmount,
                        EscapeCsv(row.PaymentMethod)
                    ));
                }

                File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(true));

                var res = MessageBox.Show(
                    $"Xuất báo cáo Data Analyst thành công!\nTổng cộng: {data.Count} dòng dữ liệu chi tiết.\nFile lưu tại: {sfd.FileName}\n\nBạn có muốn mở file ngay bây giờ không?",
                    "Xuất Dữ Liệu Thành Công",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (res == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi xuất file báo cáo doanh thu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    public void LoadCurrentTab()
    {
        switch (ActiveTab)
        {
            case "OVERVIEW":
                LoadOverview();
                break;
            case "TABLES":
                LoadTables();
                break;
            case "PRODUCTS":
                LoadProducts();
                break;
            case "COMPARISON":
                LoadComparison();
                break;
        }
    }

    private void LoadOverview()
    {
        Overview = _reportService.GetRevenueOverview(FromDate, ToDate);
        GenerateLineChartPoints();
    }

    private void LoadTables()
    {
        int? areaId = (SelectedArea != null && SelectedArea.AreaId > 0) ? SelectedArea.AreaId : null;
        var tables = _reportService.GetTablePerformance(FromDate, ToDate, areaId, TableSearchKeyword);
        TableReports = new ObservableCollection<TablePerformanceDto>(tables);

        TotalTablesRevenue = tables.Sum(t => t.TotalRevenue);
        TotalTablesSessions = tables.Sum(t => t.TotalSessions);
        TopTableNumber = tables.FirstOrDefault() != null
            ? $"{tables[0].TableNumber} ({tables[0].TotalRevenue:N0} đ)"
            : "Chưa có";
    }

    private void LoadProducts()
    {
        int? catId = (SelectedCategory != null && SelectedCategory.CategoryId > 0) ? SelectedCategory.CategoryId : null;
        var prods = _reportService.GetProductSales(FromDate, ToDate, catId, ProductSearchKeyword);
        ProductReports = new ObservableCollection<ProductSalesDto>(prods);

        TotalProductsSold = prods.Sum(p => p.QuantitySold);
        TotalProductRevenue = prods.Sum(p => p.TotalRevenue);
        TotalProductItemsCount = prods.Count;
        BestSellingProductName = prods.FirstOrDefault() != null
            ? $"{prods[0].ProductName} (x{prods[0].QuantitySold})"
            : "Chưa có";
    }

    private void LoadComparison()
    {
        ComparisonReport = _reportService.GetPeriodComparison(SelectedComparisonMode);
        decimal maxVal = 0;
        if (ComparisonReport.ComparisonPoints.Any())
        {
            maxVal = ComparisonReport.ComparisonPoints.Max(p => Math.Max(p.ValueCurrent, p.ValuePrevious));
        }
        decimal displayMax = maxVal > 0 ? maxVal : 500000m;
        ComparisonYMaxLabel = $"{displayMax:N0} đ";
        ComparisonYMidLabel = $"{displayMax * 0.5m:N0} đ";
        ComparisonYZeroLabel = "0 đ";
    }

    private void GenerateLineChartPoints()
    {
        var pts = Overview.ChartPoints;
        if (!pts.Any())
        {
            LineChartPoints = new PointCollection();
            AreaChartPoints = new PointCollection();
            ChartXTicks = new ObservableCollection<ChartAxisTick>();
            ChartYMaxLabel = "0 đ";
            ChartY75Label = "0 đ";
            ChartYMidLabel = "0 đ";
            ChartY25Label = "0 đ";
            ChartYZeroLabel = "0 đ";
            return;
        }

        double width = 760.0;
        double height = 150.0;
        double step = pts.Count > 1 ? width / (pts.Count - 1) : width;

        decimal maxVal = pts.Max(p => p.TotalRevenue);
        decimal displayMax = maxVal > 0 ? maxVal : 1000000m;
        ChartYMaxLabel = $"{displayMax:N0} đ";
        ChartY75Label = $"{displayMax * 0.75m:N0} đ";
        ChartYMidLabel = $"{displayMax * 0.5m:N0} đ";
        ChartY25Label = $"{displayMax * 0.25m:N0} đ";
        ChartYZeroLabel = "0 đ";

        if (pts.Count == 24)
        {
            ChartXAxisTitle = "▶ Thời gian (24 Khung giờ trong ngày: 00h - 23h)";
        }
        else
        {
            ChartXAxisTitle = "▶ Thời gian (Các mốc ngày trong kỳ báo cáo)";
        }

        // Tạo nhãn mốc trục X
        var ticks = new ObservableCollection<ChartAxisTick>();
        if (pts.Count <= 8)
        {
            for (int i = 0; i < pts.Count; i++)
            {
                ticks.Add(new ChartAxisTick { X = i * step, Label = pts[i].Label });
            }
        }
        else
        {
            int stepInterval = Math.Max(1, pts.Count / 8);
            for (int i = 0; i < pts.Count; i += stepInterval)
            {
                ticks.Add(new ChartAxisTick { X = i * step, Label = pts[i].Label });
            }
            if (!ticks.Any(t => t.Label == pts[^1].Label))
            {
                ticks.Add(new ChartAxisTick { X = (pts.Count - 1) * step, Label = pts[^1].Label });
            }
        }
        ChartXTicks = ticks;

        if (maxVal == 0) maxVal = 1;

        var linePoints = new PointCollection();
        var areaPoints = new PointCollection();

        // Điểm đầu của diện tích (đáy trái)
        areaPoints.Add(new Point(0, height));

        for (int i = 0; i < pts.Count; i++)
        {
            double x = i * step;
            double normalizedY = (double)(pts[i].TotalRevenue / maxVal);
            double y = height - (normalizedY * (height - 20)) - 10;

            var pt = new Point(x, y);
            linePoints.Add(pt);
            areaPoints.Add(pt);
        }

        // Điểm cuối của diện tích (đáy phải)
        areaPoints.Add(new Point((pts.Count - 1) * step, height));

        LineChartPoints = linePoints;
        AreaChartPoints = areaPoints;
    }
}

public class ChartAxisTick
{
    public double X { get; set; }
    public string Label { get; set; } = string.Empty;
}
