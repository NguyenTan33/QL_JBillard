namespace QL_JBillard.Services.Strategies.Pricing;

public interface IPricingStrategy
{
    string StrategyName { get; }
    
    /// <summary>
    /// Tính toán số tiền giờ dựa trên thời gian bắt đầu, kết thúc và giá niêm yết
    /// </summary>
    decimal CalculateCost(DateTime startTime, DateTime endTime, decimal hourlyRate, int minMinutes = 15);
}
