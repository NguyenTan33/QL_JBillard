using Microsoft.EntityFrameworkCore;
using QL_JBillard.Data;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.Services.Implementations;

public class CustomerService : ICustomerService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public CustomerService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public Customer? FindByPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        using var context = _contextFactory.CreateDbContext();
        return context.Customers
                      .Include(c => c.Tier)
                      .FirstOrDefault(c => c.PhoneNumber == phone.Trim());
    }

    public Customer CreateOrUpdateCustomer(string phone, string fullName, string? notes = null)
    {
        using var context = _contextFactory.CreateDbContext();
        string cleanPhone = phone.Trim();
        var customer = context.Customers.FirstOrDefault(c => c.PhoneNumber == cleanPhone);

        if (customer == null)
        {
            var defaultTier = context.MembershipTiers.AsEnumerable().OrderBy(t => t.MinSpentThreshold).FirstOrDefault();
            if (defaultTier == null)
            {
                defaultTier = new MembershipTier
                {
                    TierName = "Thành Viên Đồng",
                    DiscountPercentHour = 0,
                    DiscountPercentFnb = 0,
                    MinSpentThreshold = 0
                };
                context.MembershipTiers.Add(defaultTier);
                context.SaveChanges();
            }

            customer = new Customer
            {
                PhoneNumber = cleanPhone,
                FullName = fullName.Trim(),
                TierId = defaultTier.TierId,
                CurrentPoints = 0,
                TotalPointsAccumulated = 0,
                TotalSpent = 0,
                CreatedAt = DateTime.Now,
                Notes = notes
            };
            context.Customers.Add(customer);
        }
        else
        {
            customer.FullName = fullName.Trim();
            if (notes != null) customer.Notes = notes;
        }

        context.SaveChanges();
        return context.Customers.Include(c => c.Tier).First(c => c.CustomerId == customer.CustomerId);
    }

    public List<Customer> GetAllCustomers(string? search = null)
    {
        using var context = _contextFactory.CreateDbContext();
        var query = context.Customers.Include(c => c.Tier).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string keyword = search.Trim().ToLower();
            query = query.Where(c => c.PhoneNumber.Contains(keyword) || c.FullName.ToLower().Contains(keyword));
        }

        return query.AsEnumerable().OrderByDescending(c => c.TotalSpent).ToList();
    }

    public List<MembershipTier> GetAllTiers()
    {
        using var context = _contextFactory.CreateDbContext();
        return context.MembershipTiers.AsEnumerable().OrderBy(t => t.MinSpentThreshold).ToList();
    }

    public void RecordVisitAndPoints(int customerId, decimal totalSpent, int pointsEarned, int pointsUsed)
    {
        using var context = _contextFactory.CreateDbContext();
        var customer = context.Customers.Find(customerId);
        if (customer == null) return;

        customer.TotalSpent += totalSpent;
        customer.CurrentPoints = Math.Max(0, customer.CurrentPoints - pointsUsed + pointsEarned);
        customer.TotalPointsAccumulated += pointsEarned;
        customer.LastVisitAt = DateTime.Now;

        // Tự động thăng hạng
        var tiers = context.MembershipTiers.AsEnumerable().OrderByDescending(t => t.MinSpentThreshold).ToList();
        foreach (var tier in tiers)
        {
            if (customer.TotalSpent >= tier.MinSpentThreshold)
            {
                customer.TierId = tier.TierId;
                break;
            }
        }

        context.SaveChanges();
    }
}
