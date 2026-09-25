using QL_JBillard.Helpers;

namespace QL_JBillard.Services.Strategies.Pricing;

public class HappyHourPricingStrategy : IPricingStrategy
{
    public string StrategyName => "Khung Giờ Vàng (-20%)";
    public TimeSpan HappyHourStart { get; set; } = new TimeSpan(8, 0, 0);  // 8h sáng
    public TimeSpan HappyHourEnd { get; set; } = new TimeSpan(14, 0, 0);   // 2h chiều
    public decimal DiscountPercent { get; set; } = 20m;

    public decimal CalculateCost(DateTime startTime, DateTime endTime, decimal hourlyRate, int minMinutes = 0)
    {
        var standardStrategy = new StandardPricingStrategy();
        decimal baseCost = standardStrategy.CalculateCost(startTime, endTime, hourlyRate, minMinutes);

        // Nếu giờ bắt đầu nằm trong khung giờ vàng thì chiết khấu
        if (startTime.TimeOfDay >= HappyHourStart && startTime.TimeOfDay <= HappyHourEnd)
        {
            decimal discount = baseCost * (DiscountPercent / 100m);
            decimal discountedCost = baseCost - discount;
            return BilliardPricingHelper.RoundBilliardAmount(discountedCost);
        }

        return baseCost;
    }
}
