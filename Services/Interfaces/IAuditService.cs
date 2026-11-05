using QL_JBillard.Domain.Entities;

namespace QL_JBillard.Services.Interfaces;

public interface IAuditService
{
    void Log(int? userId, string action, string entityName, string? entityId, object? oldValues = null, object? newValues = null);
    List<AuditLog> GetRecentLogs(int limit = 100);
    List<AuditLog> GetLogs(DateTime? fromDate = null, DateTime? toDate = null, int? limit = null);
    (bool IsValid, int TamperedIndex, string Message) VerifyChainIntegrity();
}
