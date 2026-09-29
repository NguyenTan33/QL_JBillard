using QL_JBillard.Domain.Entities;

namespace QL_JBillard.Services.Interfaces;

public interface ICustomerService
{
    Customer? FindByPhone(string phone);
    Customer CreateOrUpdateCustomer(string phone, string fullName, string? notes = null);
    List<Customer> GetAllCustomers(string? search = null);
    List<MembershipTier> GetAllTiers();
    void RecordVisitAndPoints(int customerId, decimal totalSpent, int pointsEarned, int pointsUsed);
}
