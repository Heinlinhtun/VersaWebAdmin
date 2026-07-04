namespace VersaWebAdmin.Services;

public interface IDropboxBackupUploadService
{
    Task UploadBackupAsync(string localFilePath, string fileName, DateTime backupTime, CancellationToken cancellationToken);
}
