namespace BudgetBoard.Database.Models;

/// <summary>
/// Per-user settings for subscription renewal alerts.
/// Webhook URLs and bot tokens are stored encrypted. SMTP credentials stay in the host environment.
/// </summary>
public class RenewalAlertSettings
{
    /// <summary>
    /// User these settings belong to. Also the primary key.
    /// </summary>
    public Guid UserID { get; set; }

    /// <summary>
    /// Reference to the user these settings belong to.
    /// </summary>
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// Whether the background job should send alerts for this user.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Default number of days before a charge to notify. Zero means the charge date.
    /// </summary>
    public int DaysBefore { get; set; } = 3;

    /// <summary>
    /// Whether to send email when SMTP is configured on the host.
    /// </summary>
    public bool EmailEnabled { get; set; }

    /// <summary>
    /// Destination address. When empty, the account email is used.
    /// </summary>
    public string? EmailAddress { get; set; }

    /// <summary>
    /// Whether to post to a Discord incoming webhook.
    /// </summary>
    public bool DiscordEnabled { get; set; }

    /// <summary>
    /// Encrypted Discord webhook URL.
    /// </summary>
    public string? DiscordWebhookUrl { get; set; }

    /// <summary>
    /// Whether to send a Telegram bot message.
    /// </summary>
    public bool TelegramEnabled { get; set; }

    /// <summary>
    /// Encrypted Telegram bot token.
    /// </summary>
    public string? TelegramBotToken { get; set; }

    /// <summary>
    /// Telegram chat ID or @channel name. Not a secret.
    /// </summary>
    public string? TelegramChatId { get; set; }

    /// <summary>
    /// Whether to POST to a generic webhook, including ntfy.
    /// </summary>
    public bool WebhookEnabled { get; set; }

    /// <summary>
    /// Encrypted webhook or ntfy topic URL.
    /// </summary>
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Encrypted bearer token sent with the webhook, when set.
    /// </summary>
    public string? WebhookToken { get; set; }

    /// <summary>
    /// Payload style: <c>json</c> or <c>ntfy</c>.
    /// </summary>
    public string WebhookFormat { get; set; } = "json";
}
