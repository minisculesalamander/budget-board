namespace BudgetBoard.Service.Models;

public static class RenewalAlertLimits
{
    public const int DefaultDaysBefore = 3;
    public const int MinDaysBefore = 0;
    public const int MaxDaysBefore = 90;
    public const int MaxUrlLength = 2000;
    public const int MaxSecretLength = 512;
    public const int MaxEmailLength = 320;
    public const int MaxChatIdLength = 64;
}

public static class RenewalAlertChannels
{
    public const string Email = "Email";
    public const string Discord = "Discord";
    public const string Telegram = "Telegram";
    public const string Webhook = "Webhook";
}

public static class RenewalAlertWebhookFormats
{
    public const string Json = "json";
    public const string Ntfy = "ntfy";
}

public static class RenewalAlertErrorCodes
{
    public const string SmtpMissing = "smtp_missing";
    public const string EmailMissing = "email_missing";
    public const string Unreachable = "unreachable";
    public const string Http = "http";
    public const string Unreadable = "unreadable";
    public const string Invalid = "invalid";
    public const string Failed = "failed";
}

public sealed class RenewalAlertChannelError
{
    public RenewalAlertChannelError(string channel, string code, int? statusCode)
    {
        Channel = channel;
        Code = code;
        StatusCode = statusCode;
    }

    public string Channel { get; }
    public string Code { get; }
    public int? StatusCode { get; }
}

public sealed class RenewalAlertChannelResult
{
    public required string Channel { get; init; }
    public bool Succeeded { get; init; }
    public RenewalAlertChannelError? Error { get; init; }
}

public sealed class RenewalAlertSendResult
{
    public IReadOnlyList<RenewalAlertChannelResult> Channels { get; init; } = [];
}

/// <summary>
/// Plaintext channel settings passed to the sender. Never log or return this type.
/// </summary>
public sealed class RenewalAlertChannelPayload
{
    public bool EmailEnabled { get; init; }
    public string? EmailAddress { get; init; }
    public bool DiscordEnabled { get; init; }
    public string? DiscordWebhookUrl { get; init; }
    public bool TelegramEnabled { get; init; }
    public string? TelegramBotToken { get; init; }
    public string? TelegramChatId { get; init; }
    public bool WebhookEnabled { get; init; }
    public string? WebhookUrl { get; init; }
    public string? WebhookToken { get; init; }
    public string WebhookFormat { get; init; } = RenewalAlertWebhookFormats.Json;
}

public sealed class RenewalAlertNotice
{
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public string? MerchantName { get; init; }
    public decimal? Amount { get; init; }
    public string? AccountName { get; init; }
    public DateOnly? NextChargeDate { get; init; }
    public int? DaysUntil { get; init; }
    public required RenewalAlertChannelPayload Channels { get; init; }
}

public class RenewalAlertSettingsResponse
{
    public bool Enabled { get; set; }
    public int DaysBefore { get; set; } = RenewalAlertLimits.DefaultDaysBefore;
    public bool EmailEnabled { get; set; }
    public string? EmailAddress { get; set; }
    public bool DiscordEnabled { get; set; }
    public bool DiscordWebhookConfigured { get; set; }
    public bool TelegramEnabled { get; set; }
    public bool TelegramBotTokenConfigured { get; set; }
    public string? TelegramChatId { get; set; }
    public bool WebhookEnabled { get; set; }
    public bool WebhookUrlConfigured { get; set; }
    public bool WebhookTokenConfigured { get; set; }
    public string WebhookFormat { get; set; } = RenewalAlertWebhookFormats.Json;
    public bool SmtpConfigured { get; set; }
}

public class RenewalAlertSettingsUpdateRequest
{
    public bool Enabled { get; set; }
    public int DaysBefore { get; set; } = RenewalAlertLimits.DefaultDaysBefore;
    public bool EmailEnabled { get; set; }
    public string? EmailAddress { get; set; }
    public bool DiscordEnabled { get; set; }
    public OptionalField<string?> DiscordWebhookUrl { get; set; } = new();
    public bool TelegramEnabled { get; set; }
    public OptionalField<string?> TelegramBotToken { get; set; } = new();
    public string? TelegramChatId { get; set; }
    public bool WebhookEnabled { get; set; }
    public OptionalField<string?> WebhookUrl { get; set; } = new();
    public OptionalField<string?> WebhookToken { get; set; } = new();
    public string WebhookFormat { get; set; } = RenewalAlertWebhookFormats.Json;
}

public class SubscriptionRenewalPreferenceResponse
{
    public Guid RecurringRuleID { get; set; }
    public bool AlertsEnabled { get; set; } = true;
    public int? DaysBefore { get; set; }
}

public class RenewalAlertPreferenceListResponse
{
    public int DefaultDaysBefore { get; set; } = RenewalAlertLimits.DefaultDaysBefore;
    public bool AlertsEnabled { get; set; }
    public IReadOnlyList<SubscriptionRenewalPreferenceResponse> Preferences { get; set; } = [];
}

public class SubscriptionRenewalPreferenceUpdateRequest
{
    public bool AlertsEnabled { get; set; } = true;
    public int? DaysBefore { get; set; }
}
