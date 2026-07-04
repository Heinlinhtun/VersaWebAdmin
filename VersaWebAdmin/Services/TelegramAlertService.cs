using System.Net.Http;
using Microsoft.Extensions.Options;
using VersaWebAdmin.Models;

namespace VersaWebAdmin.Services;

public class TelegramAlertService : ITelegramAlertService
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    private const int MaxFailureLines = 10;
    private const int MaxFailureErrorLength = 120;

    private readonly HttpClient _httpClient;
    private readonly IOptions<TelegramAlertOptions> _options;
    private readonly ILogger<TelegramAlertService> _logger;

    public TelegramAlertService(
        HttpClient httpClient,
        IOptions<TelegramAlertOptions> options,
        ILogger<TelegramAlertService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task SendBackupAlertAsync(BackupRunResult result, CancellationToken cancellationToken)
    {
        try
        {
            var options = _options.Value;
            if (!ShouldSend(options, result))
            {
                return;
            }

            var message = BuildMessage(result);
            var requestUri = $"https://api.telegram.org/bot{options.BotToken}/sendMessage";

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCancellation.CancelAfter(RequestTimeout);

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["chat_id"] = options.ChatId,
                    ["text"] = message
                })
            };

            using var response = await _httpClient.SendAsync(request, linkedCancellation.Token);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(linkedCancellation.Token);
                _logger.LogWarning(
                    "Telegram alert request failed with status code {StatusCode}. Response: {ResponseBody}",
                    response.StatusCode,
                    responseBody);
            }
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Telegram alert timed out for backup trigger {Trigger}", result.Trigger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram alert failed for backup trigger {Trigger}", result.Trigger);
        }
    }

    private static bool ShouldSend(TelegramAlertOptions options, BackupRunResult result)
    {
        if (!options.Enabled)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.BotToken) || string.IsNullOrWhiteSpace(options.ChatId))
        {
            return false;
        }

        if (result.IsSuccess)
        {
            return options.SendSuccessMessages;
        }

        return options.SendFailureMessages;
    }

    private static string BuildMessage(BackupRunResult result)
    {
        var lines = new List<string>
        {
            GetStatusLine(result),
            $"🕒 Trigger: {result.Trigger}",
            $"📅 Backup Date: {result.StartedAt:yyyy-MM-dd}",
            $"⏱️ Start: {result.StartedAt:HH:mm:ss}",
            $"⏱️ End: {result.EndedAt:HH:mm:ss}",
            $"🗄️ Total Databases: {result.TotalTenants}",
            $"✅ Success: {result.SuccessfulTenants}",
            $"❌ Failed: {result.FailedTenants}"
        };

        if (result.DropboxUploadSucceededCount > 0 || result.DropboxUploadFailedCount > 0)
        {
            lines.Add($"☁️ Dropbox: {result.DropboxUploadSucceededCount} uploaded, {result.DropboxUploadFailedCount} failed");
        }

        if (result.IsSkipped)
        {
            lines.Add("⏭️ Run skipped.");
        }

        if (result.FailedDatabases.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("⚠️ Failed Databases");

            foreach (var failure in result.FailedDatabases.Take(MaxFailureLines))
            {
                lines.Add($"• {failure.DatabaseName}: {FormatFailureMessage(failure)}");
            }

            var remainingCount = result.FailedDatabases.Count - MaxFailureLines;
            if (remainingCount > 0)
            {
                lines.Add($"• ...and {remainingCount} more failure(s)");
            }
        }
        else if (result.IsSuccess)
        {
            lines.Add("🎉 All tenant backups completed successfully.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string GetStatusLine(BackupRunResult result)
    {
        if (result.IsSkipped)
        {
            return "⏸️ Backup SKIPPED";
        }

        if (result.TotalTenants > 0 && result.FailedTenants >= result.TotalTenants)
        {
            return "🚨 Backup FAILED";
        }

        if (result.FailedTenants > 0)
        {
            return "⚠️ Backup COMPLETED WITH ERRORS";
        }

        return "✅ Backup SUCCESS";
    }

    private static string FormatFailureMessage(BackupTenantFailure failure)
    {
        var message = string.IsNullOrWhiteSpace(failure.ErrorMessage)
            ? "Unknown error."
            : failure.ErrorMessage.Trim();

        if (message.Length > MaxFailureErrorLength)
        {
            message = message[..MaxFailureErrorLength] + "...";
        }

        if (!string.IsNullOrWhiteSpace(failure.Stage))
        {
            return $"{failure.Stage}: {message}";
        }

        return message;
    }
}
