using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Versa.Core.Services;
using VersaWebAdmin.Models;

namespace VersaWebAdmin.Services;

public class TenantBackupService : ITenantBackupService
{
    private readonly DatabaseService _databaseService;
    private readonly IDropboxBackupUploadService _dropboxBackupUploadService;
    private readonly ITelegramAlertService _telegramAlertService;
    private readonly IOptions<DropboxBackupOptions> _options;
    private readonly ILogger<TenantBackupService> _logger;
    private readonly SemaphoreSlim _backupLock = new(1, 1);

    public TenantBackupService(
        DatabaseService databaseService,
        IDropboxBackupUploadService dropboxBackupUploadService,
        ITelegramAlertService telegramAlertService,
        IOptions<DropboxBackupOptions> options,
        ILogger<TenantBackupService> logger)
    {
        _databaseService = databaseService;
        _dropboxBackupUploadService = dropboxBackupUploadService;
        _telegramAlertService = telegramAlertService;
        _options = options;
        _logger = logger;
    }

    public async Task<BackupRunResult> RunBackupAsync(string trigger, CancellationToken cancellationToken)
    {
        return await RunBackupForDatabasesAsync(null, trigger, cancellationToken);
    }

    public async Task<BackupRunResult> RunBackupForTenantAsync(string dbName, string trigger, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dbName))
        {
            return new BackupRunResult
            {
                Trigger = trigger,
                StartedAt = DateTime.Now,
                EndedAt = DateTime.Now,
                Message = "Tenant database name is missing."
            };
        }

        return await RunBackupForDatabasesAsync([dbName.Trim()], trigger, cancellationToken);
    }

    public async Task<string> CreateTenantBackupArchiveAsync(string dbName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dbName))
        {
            throw new InvalidOperationException("Tenant database name is missing.");
        }

        var backupTime = DateTime.Now;
        var tempFilePath = Path.Combine(
            Path.GetTempPath(),
            $"{dbName}_{backupTime:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.sql.gz");

        try
        {
            await DumpDatabaseToCompressedFileAsync(dbName, tempFilePath, cancellationToken);
            return tempFilePath;
        }
        catch
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }

            throw;
        }
    }

    private async Task<BackupRunResult> RunBackupForDatabasesAsync(IReadOnlyCollection<string>? tenantOverride, string trigger, CancellationToken cancellationToken)
    {
        var runStartedAt = DateTime.Now;

        try
        {
            if (!await _backupLock.WaitAsync(0, cancellationToken))
            {
                _logger.LogWarning("Backup run skipped because another backup run is already in progress. Trigger: {Trigger}", trigger);

                var skippedResult = new BackupRunResult
                {
                    IsSkipped = true,
                    Trigger = trigger,
                    StartedAt = runStartedAt,
                    EndedAt = DateTime.Now,
                    Message = "Backup is already running. Please wait for the current run to finish."
                };

                await SendTelegramAlertSafelyAsync(skippedResult, cancellationToken);
                return skippedResult;
            }
        }
        catch (OperationCanceledException)
        {
            return new BackupRunResult
            {
                IsSkipped = true,
                Trigger = trigger,
                StartedAt = runStartedAt,
                EndedAt = DateTime.Now,
                Message = "Backup request was canceled before it started."
            };
        }

        try
        {
            var options = _options.Value;
            if (!options.Enabled)
            {
                _logger.LogWarning("Backup run skipped because Dropbox backup is disabled. Trigger: {Trigger}", trigger);
                var disabledResult = new BackupRunResult
                {
                    IsSkipped = true,
                    Trigger = trigger,
                    StartedAt = runStartedAt,
                    EndedAt = DateTime.Now,
                    Message = "Dropbox backup is disabled in configuration."
                };

                await SendTelegramAlertSafelyAsync(disabledResult, cancellationToken);
                return disabledResult;
            }

            var hasRefreshTokenConfiguration =
                !string.IsNullOrWhiteSpace(options.RefreshToken) &&
                !string.IsNullOrWhiteSpace(options.AppKey) &&
                !string.IsNullOrWhiteSpace(options.AppSecret);

            var hasAccessTokenConfiguration = !string.IsNullOrWhiteSpace(options.AccessToken);

            if (!hasRefreshTokenConfiguration && !hasAccessTokenConfiguration)
            {
                _logger.LogError("Backup run skipped because Dropbox credentials are missing. Trigger: {Trigger}", trigger);
                var missingTokenResult = new BackupRunResult
                {
                    Trigger = trigger,
                    StartedAt = runStartedAt,
                    EndedAt = DateTime.Now,
                    Message = "Dropbox is not configured. Set AppKey, AppSecret, and RefreshToken, or provide an AccessToken."
                };

                await SendTelegramAlertSafelyAsync(missingTokenResult, cancellationToken);
                return missingTokenResult;
            }

            var tenants = tenantOverride ?? await GetTenantDatabaseNamesAsync(cancellationToken);
            var result = new BackupRunResult
            {
                Trigger = trigger,
                StartedAt = runStartedAt,
                TotalTenants = tenants.Count
            };

            if (tenants.Count == 0)
            {
                result.IsSuccess = true;
                result.EndedAt = DateTime.Now;
                result.Message = "No tenant databases were found for backup.";
                _logger.LogInformation("Backup run completed with no tenant databases to process. Trigger: {Trigger}", trigger);
                await SendTelegramAlertSafelyAsync(result, cancellationToken);
                return result;
            }

            _logger.LogInformation("Starting tenant backup run for {TenantCount} databases. Trigger: {Trigger}", tenants.Count, trigger);

            foreach (var dbName in tenants)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Backup run canceled while processing tenant databases.");
                    break;
                }

                try
                {
                    await BackupSingleTenantAsync(dbName, cancellationToken);
                    result.SuccessfulTenants++;
                    result.DropboxUploadSucceededCount++;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Backup canceled while processing database {DatabaseName}", dbName);
                    result.FailedTenants++;
                    result.FailedDatabases.Add(new BackupTenantFailure
                    {
                        DatabaseName = dbName,
                        Stage = "Canceled",
                        ErrorMessage = "Backup was canceled while processing this database."
                    });
                    break;
                }
                catch (TenantBackupException ex)
                {
                    result.FailedTenants++;
                    if (string.Equals(ex.Stage, "Dropbox Upload", StringComparison.OrdinalIgnoreCase))
                    {
                        result.DropboxUploadFailedCount++;
                    }

                    result.FailedDatabases.Add(new BackupTenantFailure
                    {
                        DatabaseName = dbName,
                        Stage = ex.Stage,
                        ErrorMessage = ShortenErrorMessage(ex.Message)
                    });

                    _logger.LogError(ex, "Backup failed for tenant database {DatabaseName}. Continuing with next tenant.", dbName);
                }
                catch (Exception ex)
                {
                    result.FailedTenants++;
                    result.FailedDatabases.Add(new BackupTenantFailure
                    {
                        DatabaseName = dbName,
                        Stage = "Unexpected",
                        ErrorMessage = ShortenErrorMessage(ex.Message)
                    });
                    _logger.LogError(ex, "Backup failed for tenant database {DatabaseName}. Continuing with next tenant.", dbName);
                }
            }

            result.IsSuccess = result.FailedTenants == 0 && !cancellationToken.IsCancellationRequested;
            result.EndedAt = DateTime.Now;
            result.Message = $"Backup run finished. Success: {result.SuccessfulTenants}, Failed: {result.FailedTenants}, Total: {result.TotalTenants}.";

            _logger.LogInformation(
                "Tenant backup run finished. Trigger: {Trigger}, Success: {SuccessfulTenants}, Failed: {FailedTenants}, Total: {TotalTenants}",
                trigger,
                result.SuccessfulTenants,
                result.FailedTenants,
                result.TotalTenants);

            await SendTelegramAlertSafelyAsync(result, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup run failed unexpectedly. Trigger: {Trigger}", trigger);

            var failedResult = new BackupRunResult
            {
                Trigger = trigger,
                StartedAt = runStartedAt,
                EndedAt = DateTime.Now,
                FailedTenants = 1,
                FailedDatabases =
                [
                    new BackupTenantFailure
                    {
                        DatabaseName = "BackupRun",
                        Stage = "Unexpected",
                        ErrorMessage = ShortenErrorMessage(ex.Message)
                    }
                ],
                Message = "Backup run failed unexpectedly. Check logs for details."
            };

            await SendTelegramAlertSafelyAsync(failedResult, cancellationToken);
            return failedResult;
        }
        finally
        {
            _backupLock.Release();
        }
    }

    private async Task<List<string>> GetTenantDatabaseNamesAsync(CancellationToken cancellationToken)
    {
        var databaseNames = new List<string>();
        var masterConnectionString = _databaseService.GetMasterConnectionString();

        await using var connection = new MySqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT DbName FROM Tenants WHERE DbName IS NOT NULL AND DbName <> '' ORDER BY TenantId";
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var dbName = reader.IsDBNull(0) ? string.Empty : reader.GetString(0).Trim();
            if (!string.IsNullOrWhiteSpace(dbName))
            {
                databaseNames.Add(dbName);
            }
        }

        return databaseNames;
    }

    private async Task BackupSingleTenantAsync(string dbName, CancellationToken cancellationToken)
    {
        string? tempFilePath = null;
        var backupTime = DateTime.Now;

        try
        {
            tempFilePath = Path.Combine(
                Path.GetTempPath(),
                $"{dbName}_{backupTime:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.sql.gz");

            await DumpDatabaseToCompressedFileAsync(dbName, tempFilePath, cancellationToken);

            var dropboxFileName = $"{dbName}_{backupTime:yyyyMMdd_HHmmss}.sql.gz";

            try
            {
                await _dropboxBackupUploadService.UploadBackupAsync(tempFilePath, dropboxFileName, backupTime, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dropbox upload failed for tenant database {DatabaseName}", dbName);
                throw new TenantBackupException(dbName, "Dropbox Upload", ex);
            }
        }
        catch (TenantBackupException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new TenantBackupException(dbName, "Backup", ex);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempFilePath) && File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary backup file {TempFilePath}", tempFilePath);
                }
            }
        }
    }

    private async Task SendTelegramAlertSafelyAsync(BackupRunResult result, CancellationToken cancellationToken)
    {
        try
        {
            if (result.EndedAt == default)
            {
                result.EndedAt = DateTime.Now;
            }

            await _telegramAlertService.SendBackupAlertAsync(result, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram notification failed after backup trigger {Trigger}", result.Trigger);
        }
    }

    private static string ShortenErrorMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Unknown error.";
        }

        var normalized = message.Replace(Environment.NewLine, " ").Trim();
        return normalized.Length <= 220 ? normalized : normalized[..220] + "...";
    }

    private async Task DumpDatabaseToCompressedFileAsync(string dbName, string outputFilePath, CancellationToken cancellationToken)
    {
        var connectionBuilder = new MySqlConnectionStringBuilder(_databaseService.GetTenantConnectionString(dbName));
        var options = _options.Value;

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = string.IsNullOrWhiteSpace(options.MySqlDumpPath) ? "mysqldump" : options.MySqlDumpPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        foreach (var argument in BuildMySqlDumpArguments(connectionBuilder, dbName))
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!string.IsNullOrWhiteSpace(connectionBuilder.Password))
        {
            process.StartInfo.Environment["MYSQL_PWD"] = connectionBuilder.Password;
        }

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start mysqldump for database {dbName}.");
        }

        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(TimeSpan.FromSeconds(Math.Max(60, options.ProcessTimeoutSeconds)));

        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            await using var fileStream = File.Create(outputFilePath);
            await using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);

            await process.StandardOutput.BaseStream.CopyToAsync(gzipStream, timeoutCancellation.Token);
            await gzipStream.FlushAsync(timeoutCancellation.Token);
            await process.WaitForExitAsync(timeoutCancellation.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKillProcess(process);
            throw new TimeoutException($"mysqldump timed out for database {dbName}.");
        }
        catch
        {
            TryKillProcess(process);
            throw;
        }

        var stderr = await stderrTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"mysqldump exited with code {process.ExitCode} for database {dbName}. Error: {stderr}");
        }

        if (new FileInfo(outputFilePath).Length == 0)
        {
            throw new InvalidOperationException($"mysqldump produced an empty backup file for database {dbName}.");
        }
    }

    private static IEnumerable<string> BuildMySqlDumpArguments(MySqlConnectionStringBuilder connectionBuilder, string dbName)
    {
        var host = string.IsNullOrWhiteSpace(connectionBuilder.Server) ? "127.0.0.1" : connectionBuilder.Server;
        var port = connectionBuilder.Port == 0 ? 3306u : connectionBuilder.Port;
        var user = string.IsNullOrWhiteSpace(connectionBuilder.UserID) ? "root" : connectionBuilder.UserID;

        return
        [
            $"--host={host}",
            $"--port={port}",
            $"--user={user}",
            "--single-transaction",
            "--quick",
            "--routines",
            "--events",
            "--triggers",
            dbName
        ];
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }
}

internal sealed class TenantBackupException : Exception
{
    public TenantBackupException(string databaseName, string stage, Exception innerException)
        : base(innerException.Message, innerException)
    {
        DatabaseName = databaseName;
        Stage = stage;
    }

    public string DatabaseName { get; }
    public string Stage { get; }
}
