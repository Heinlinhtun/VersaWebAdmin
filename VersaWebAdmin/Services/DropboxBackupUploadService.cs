using Dropbox.Api;
using Dropbox.Api.Files;
using Microsoft.Extensions.Options;
using VersaWebAdmin.Models;

namespace VersaWebAdmin.Services;

public class DropboxBackupUploadService : IDropboxBackupUploadService
{
    private readonly IOptionsMonitor<DropboxBackupOptions> _options;
    private readonly ILogger<DropboxBackupUploadService> _logger;

    public DropboxBackupUploadService(
        IOptionsMonitor<DropboxBackupOptions> options,
        ILogger<DropboxBackupUploadService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task UploadBackupAsync(string localFilePath, string fileName, DateTime backupTime, CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var rootPath = NormalizeDropboxPath(options.RootPath);
        var dateFolder = backupTime.ToString("yyyy-MM-dd");
        var targetFolder = $"{rootPath}/{dateFolder}";
        var targetPath = $"{targetFolder}/{fileName}";

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCancellation.CancelAfter(TimeSpan.FromSeconds(Math.Max(30, options.DropboxUploadTimeoutSeconds)));

        using var client = CreateDropboxClient(options);

        await EnsureFolderExistsAsync(client, targetFolder, linkedCancellation.Token);

        await using var fileStream = File.OpenRead(localFilePath);
        await client.Files.UploadAsync(
                path: targetPath,
                mode: WriteMode.Overwrite.Instance,
                body: fileStream)
            .WaitAsync(linkedCancellation.Token);

        _logger.LogInformation("Uploaded backup to Dropbox path {DropboxPath}", targetPath);
    }

    private DropboxClient CreateDropboxClient(DropboxBackupOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.RefreshToken) &&
            !string.IsNullOrWhiteSpace(options.AppKey) &&
            !string.IsNullOrWhiteSpace(options.AppSecret))
        {
            _logger.LogDebug("Using Dropbox refresh token flow for backup uploads.");
            return new DropboxClient(options.RefreshToken, options.AppKey, options.AppSecret);
        }

        if (!string.IsNullOrWhiteSpace(options.AccessToken))
        {
            _logger.LogWarning("Using fallback Dropbox access token flow. Consider configuring AppKey, AppSecret, and RefreshToken instead.");
            return new DropboxClient(options.AccessToken);
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
