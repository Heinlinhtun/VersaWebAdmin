using VersaWebAdmin.Models;

namespace VersaWebAdmin.Services;

public interface ITelegramAlertService
{
    Task SendBackupAlertAsync(BackupRunResult result, CancellationToken cancellationToken);
}
