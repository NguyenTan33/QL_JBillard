using QL_JBillard.Domain.Entities;

namespace QL_JBillard.Services.Interfaces;

public interface IAuditService
{
    void Log(int? userId, string action, string entityName, string? entityId, object? oldValues = null, object? newValues = null);
    List<AuditLog> GetRecentLogs(int limit = 100);
    (bool IsValid, int TamperedIndex, string Message) VerifyChainIntegrity();
}
