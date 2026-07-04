using Dropbox.Api;
using Dropbox.Api.Files;
using Microsoft.Extensions.Options;
using VersaWebAdmin.Models;

namespace VersaWebAdmin.Services;

public class DropboxBackupUploadService : IDropboxBackupUploadService
{
    private readonly DropboxBackupOptions _options;
    private readonly ILogger<DropboxBackupUploadService> _logger;

    public DropboxBackupUploadService(
        IOptions<DropboxBackupOptions> options,
        ILogger<DropboxBackupUploadService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task UploadBackupAsync(string localFilePath, string fileName, DateTime backupTime, CancellationToken cancellationToken)
    {
        var rootPath = NormalizeDropboxPath(_options.RootPath);
        var dateFolder = backupTime.ToString("yyyy-MM-dd");
        var targetFolder = $"{rootPath}/{dateFolder}";
        var targetPath = $"{targetFolder}/{fileName}";

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCancellation.CancelAfter(TimeSpan.FromSeconds(Math.Max(30, _options.DropboxUploadTimeoutSeconds)));

        using var client = CreateDropboxClient();

        await EnsureFolderExistsAsync(client, targetFolder, linkedCancellation.Token);

        await using var fileStream = File.OpenRead(localFilePath);
        await client.Files.UploadAsync(
                path: targetPath,
                mode: WriteMode.Overwrite.Instance,
                body: fileStream)
            .WaitAsync(linkedCancellation.Token);

        _logger.LogInformation("Uploaded backup to Dropbox path {DropboxPath}", targetPath);
    }

    private DropboxClient CreateDropboxClient()
    {
        if (!string.IsNullOrWhiteSpace(_options.RefreshToken) &&
            !string.IsNullOrWhiteSpace(_options.AppKey) &&
            !string.IsNullOrWhiteSpace(_options.AppSecret))
        {
            _logger.LogDebug("Using Dropbox refresh token flow for backup uploads.");
            return new DropboxClient(_options.RefreshToken, _options.AppKey, _options.AppSecret);
        }

        if (!string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            _logger.LogWarning("Using fallback Dropbox access token flow. Consider configuring AppKey, AppSecret, and RefreshToken instead.");
            return new DropboxClient(_options.AccessToken);
        }

        throw new InvalidOperationException(
            "Dropbox configuration is missing. Configure either AppKey/AppSecret/RefreshToken or AccessToken.");
    }

    private async Task EnsureFolderExistsAsync(DropboxClient client, string folderPath, CancellationToken cancellationToken)
    {
        try
        {
            await client.Files.CreateFolderV2Async(folderPath, autorename: false)
                .WaitAsync(cancellationToken);
        }
        catch (ApiException<CreateFolderError>)
        {
            _logger.LogDebug("Dropbox folder {DropboxFolder} already exists.", folderPath);
        }
    }

    private static string NormalizeDropboxPath(string? path)
    {
        var normalized = string.IsNullOrWhiteSpace(path) ? "/VersaBackups" : path.Trim();
        normalized = normalized.Replace("\\", "/").TrimEnd('/');

        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return normalized;
    }
}
