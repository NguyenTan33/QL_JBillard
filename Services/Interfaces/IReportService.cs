using System;
using System.Collections.Generic;
using QL_JBillard.Services.DTOs;

namespace QL_JBillard.Services.Interfaces;

public interface IReportService
{
    RevenueOverviewDto GetRevenueOverview(DateTime fromDate, DateTime toDate);
    List<TablePerformanceDto> GetTablePerformance(DateTime fromDate, DateTime toDate, int? areaId = null, string? search = null);
    List<ProductSalesDto> GetProductSales(DateTime fromDate, DateTime toDate, int? categoryId = null, string? search = null);
    PeriodComparisonDto GetPeriodComparison(ComparisonPeriodMode mode);
    List<DetailedRevenueRowDto> GetDetailedRevenueData(DateTime fromDate, DateTime toDate);
}
