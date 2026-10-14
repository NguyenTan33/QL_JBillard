using QL_JBillard.Domain.Entities;

namespace QL_JBillard.Services.Interfaces;

public interface IBackupAndSecurityService
{
    DatabaseBackupLog CreateBackup(string triggerReason = "MANUAL", bool encrypt = true);
    bool RestoreBackup(string backupFilePath);
    List<DatabaseBackupLog> GetBackupHistory();
    bool VerifyDatabaseIntegrity();
    (bool IsTampered, string Details) CheckTamperProofStatus();
    void StartPeriodicAutoBackup(int intervalMinutes = 15);
    void StopPeriodicAutoBackup();
}
