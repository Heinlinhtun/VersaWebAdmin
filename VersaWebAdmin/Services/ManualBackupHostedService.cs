namespace VersaWebAdmin.Services;

public sealed class ManualBackupHostedService : BackgroundService
{
    private readonly IManualBackupQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ManualBackupHostedService> _logger;

    public ManualBackupHostedService(
        IManualBackupQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ManualBackupHostedService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _queue.WaitForBackupAsync(stoppingToken);

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var backupService = scope.ServiceProvider.GetRequiredService<ITenantBackupService>();
                    var result = await backupService.RunBackupAsync("Admin manual backup", stoppingToken);

                    if (result.IsSuccess)
                    {
                        _logger.LogInformation("Manual background backup completed: {Message}", result.Message);
                    }
                    else
                    {
                        _logger.LogWarning("Manual background backup completed with errors: {Message}", result.Message);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Manual background backup failed unexpectedly.");
                }
                finally
                {
                    _queue.MarkBackupCompleted();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
