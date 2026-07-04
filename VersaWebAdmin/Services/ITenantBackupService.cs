using VersaWebAdmin.Models;

namespace VersaWebAdmin.Services;

public interface ITenantBackupService
{
    Task<BackupRunResult> RunBackupAsync(string trigger, CancellationToken cancellationToken);
    Task<BackupRunResult> RunBackupForTenantAsync(string dbName, string trigger, CancellationToken cancellationToken);
    Task<string> CreateTenantBackupArchiveAsync(string dbName, CancellationToken cancellationToken);
}
