using QL_JBillard.Helpers;

namespace QL_JBillard.Services.Strategies.Pricing;

public class StandardPricingStrategy : IPricingStrategy
{
    public string StrategyName => "Tiêu Chuẩn";

    public decimal CalculateCost(DateTime startTime, DateTime endTime, decimal hourlyRate, int minMinutes = 0)
    {
        if (endTime <= startTime) return 0;

        var duration = endTime - startTime;
        double totalHours = duration.TotalHours;

        // Tính tiền giờ tuần tự chính xác theo thời gian thực:
        decimal rawCost = (decimal)totalHours * hourlyRate;

        // Làm tròn theo quy tắc: Dưới 600đ làm tròn xuống, từ 600đ trở lên làm tròn lên
        return BilliardPricingHelper.RoundBilliardAmount(rawCost);
    }
}
