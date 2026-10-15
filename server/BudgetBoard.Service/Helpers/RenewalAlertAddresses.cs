using System.Net.Mail;
using System.Text.RegularExpressions;
using BudgetBoard.Service.Models;

namespace BudgetBoard.Service.Helpers;

/// <summary>
/// Checks notification destinations before they are stored or used.
/// </summary>
public static partial class RenewalAlertAddresses
{
    public static bool IsEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > RenewalAlertLimits.MaxEmailLength)
        {
            return false;
        }

        if (email.Contains('\r') || email.Contains('\n'))
        {
            return false;
        }

        return MailAddress.TryCreate(email, out var parsed) && parsed.Address == email.Trim();
    }

    public static bool IsDiscordWebhook(string url)
    {
        if (!TryHttpUri(url, RenewalAlertLimits.MaxUrlLength, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var host = uri.IdnHost;
        var allowedHost =
            host.Equals("discord.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("discordapp.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("canary.discord.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("ptb.discord.com", StringComparison.OrdinalIgnoreCase);
        return allowedHost
            && uri.AbsolutePath.Contains("/api/webhooks/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsTelegramToken(string token)
    {
        return !string.IsNullOrWhiteSpace(token)
            && token.Length <= RenewalAlertLimits.MaxSecretLength
            && TelegramTokenPattern().IsMatch(token);
    }

    public static bool IsTelegramChatId(string chatId)
    {
        return !string.IsNullOrWhiteSpace(chatId)
            && chatId.Length <= RenewalAlertLimits.MaxChatIdLength
            && TelegramChatPattern().IsMatch(chatId);
    }

    public static bool IsWebhookUrl(string url)
    {
        return TryHttpUri(url, RenewalAlertLimits.MaxUrlLength, out _);
    }

    public static bool IsWebhookFormat(string format)
    {
        return format.Equals(RenewalAlertWebhookFormats.Json, StringComparison.OrdinalIgnoreCase)
            || format.Equals(RenewalAlertWebhookFormats.Ntfy, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryHttpUri(string url, int maxLength, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(url) || url.Length > maxLength)
        {
            return false;
        }

        if (
            !Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        )
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    [GeneratedRegex(@"^[0-9]{5,}:[A-Za-z0-9_-]{20,}$")]
    private static partial Regex TelegramTokenPattern();

    [GeneratedRegex(@"^(-?[0-9]{1,20}|@[A-Za-z][A-Za-z0-9_]{3,})$")]
    private static partial Regex TelegramChatPattern();
}
