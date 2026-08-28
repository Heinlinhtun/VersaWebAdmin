namespace VersaWebAdmin.Services;

public interface IManualBackupQueue
{
    bool TryQueueBackup();
    ValueTask WaitForBackupAsync(CancellationToken cancellationToken);
    void MarkBackupCompleted();
}
