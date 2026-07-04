using Microsoft.Extensions.Options;
using VersaWebAdmin.Models;

namespace VersaWebAdmin.Services;

public class DailyTenantBackupHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyTenantBackupHostedService> _logger;
    private readonly IOptions<DropboxBackupOptions> _options;
    private DateOnly? _lastScheduledRunDate;

    public DailyTenantBackupHostedService(
        IServiceProvider serviceProvider,
        ILogger<DailyTenantBackupHostedService> logger,
        IOptions<DropboxBackupOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Daily tenant backup hosted service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_options.Value.Enabled)
                {
                    _logger.LogInformation("Daily tenant backup is disabled. Waiting 1 hour before checking again.");
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                    continue;
                }

                var now = DateTime.Now;
                var nextRun = now.Date.AddDays(1);
                var delay = nextRun - now;

                _logger.LogInformation("Next scheduled tenant backup will run at {NextRun}.", nextRun);
                await Task.Delay(delay, stoppingToken);

                var scheduledDate = DateOnly.FromDateTime(DateTime.Now);
                if (_lastScheduledRunDate == scheduledDate)
                {
                    _logger.LogWarning("Skipping duplicate scheduled backup run for {ScheduledDate}.", scheduledDate);
                    continue;
                }

                _lastScheduledRunDate = scheduledDate;

                using var scope = _serviceProvider.CreateScope();
                var backupService = scope.ServiceProvider.GetRequiredService<ITenantBackupService>();
                var result = await backupService.RunBackupAsync("Daily scheduled backup", stoppingToken);

                if (result.IsSkipped)
                {
                    _logger.LogWarning("Scheduled backup run skipped: {Message}", result.Message);
                }
                else if (!result.IsSuccess)
                {
                    _logger.LogWarning("Scheduled backup run completed with failures: {Message}", result.Message);
                }
                else
                {
                    _logger.LogInformation("Scheduled backup run completed successfully: {Message}", result.Message);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in daily tenant backup loop. The service will continue running.");

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Daily tenant backup hosted service stopped.");
    }
}
