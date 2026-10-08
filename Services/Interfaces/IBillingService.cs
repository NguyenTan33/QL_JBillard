using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Services.DTOs;
using QL_JBillard.Services.Strategies.Pricing;

namespace QL_JBillard.Services.Interfaces;

public interface IBillingService
{
    LiveBillDto CalculateLiveBill(int sessionId, IPricingStrategy? strategy = null);
    Invoice CheckoutTable(
        int sessionId,
        int cashierUserId,
        PaymentMethod paymentMethod,
        decimal discountPercent,
        int pointsUsed,
        string? notes,
        string? bankRef = null,
        IPricingStrategy? strategy = null);
}
