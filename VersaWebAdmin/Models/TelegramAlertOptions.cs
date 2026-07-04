namespace VersaWebAdmin.Models;

public class TelegramAlertOptions
{
    public const string SectionName = "TelegramAlerts";

    public bool Enabled { get; set; }
    public string BotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public bool SendSuccessMessages { get; set; } = true;
    public bool SendFailureMessages { get; set; } = true;
}
