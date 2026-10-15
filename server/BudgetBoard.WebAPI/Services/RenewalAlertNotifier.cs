using System.Net;
using System.Net.Http.Json;
using System.Text;
using BudgetBoard.Service.Helpers;
using BudgetBoard.Service.Interfaces;
using BudgetBoard.Service.Models;

namespace BudgetBoard.WebAPI.Services;

public sealed class RenewalAlertNotifier(
    IHttpClientFactory httpClientFactory,
    IRenewalAlertEmailSender emailSender,
    ILogger<RenewalAlertNotifier> logger
) : IRenewalAlertNotifier
{
    public const string HttpClientName = "RenewalAlerts";

    /// <inheritdoc />
    public bool IsEmailConfigured => emailSender.IsConfigured;

    /// <inheritdoc />
    public async Task<RenewalAlertSendResult> SendAsync(
        RenewalAlertNotice notice,
        CancellationToken cancellationToken
    )
    {
        var channels = new List<RenewalAlertChannelResult>();
        var payload = notice.Channels;

        if (payload.EmailEnabled)
        {
            channels.Add(await SendEmailAsync(notice, cancellationToken));
        }

        if (payload.DiscordEnabled)
        {
            channels.Add(await SendDiscordAsync(notice, cancellationToken));
        }

        if (payload.TelegramEnabled)
        {
            channels.Add(await SendTelegramAsync(notice, cancellationToken));
        }

        if (payload.WebhookEnabled)
        {
            channels.Add(await SendWebhookAsync(notice, cancellationToken));
        }

        return new RenewalAlertSendResult { Channels = channels };
    }

    private async Task<RenewalAlertChannelResult> SendEmailAsync(
        RenewalAlertNotice notice,
        CancellationToken cancellationToken
    )
    {
        if (!emailSender.IsConfigured)
        {
            return Failure(
                RenewalAlertChannels.Email,
                RenewalAlertErrorCodes.SmtpMissing,
                null
            );
        }

        if (!RenewalAlertAddresses.IsEmail(notice.Channels.EmailAddress ?? string.Empty))
        {
            return Failure(
                RenewalAlertChannels.Email,
                RenewalAlertErrorCodes.EmailMissing,
                null
            );
        }

        try
        {
            var html =
                "<p>"
                + WebUtility.HtmlEncode(notice.Body).Replace("\n", "<br>", StringComparison.Ordinal)
                + "</p>";
            await emailSender.SendAsync(
                notice.Channels.EmailAddress!,
                HeaderSafe(notice.Subject),
                html,
                cancellationToken
            );
            return Success(RenewalAlertChannels.Email);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Renewal alert email failed");
            return Failure(RenewalAlertChannels.Email, RenewalAlertErrorCodes.Failed, null);
        }
    }

    private async Task<RenewalAlertChannelResult> SendDiscordAsync(
        RenewalAlertNotice notice,
        CancellationToken cancellationToken
    )
    {
        var url = notice.Channels.DiscordWebhookUrl ?? string.Empty;
        if (!RenewalAlertAddresses.IsDiscordWebhook(url))
        {
            return Failure(RenewalAlertChannels.Discord, RenewalAlertErrorCodes.Invalid, null);
        }

        var content = JsonContent.Create(
            new Dictionary<string, string> { ["content"] = Truncate(Text(notice), 1900) }
        );
        return await PostAsync(
            RenewalAlertChannels.Discord,
            url,
            content,
            null,
            cancellationToken
        );
    }

    private async Task<RenewalAlertChannelResult> SendTelegramAsync(
        RenewalAlertNotice notice,
        CancellationToken cancellationToken
    )
    {
        var token = notice.Channels.TelegramBotToken ?? string.Empty;
        var chatId = notice.Channels.TelegramChatId ?? string.Empty;
        if (
            !RenewalAlertAddresses.IsTelegramToken(token)
            || !RenewalAlertAddresses.IsTelegramChatId(chatId)
        )
        {
            return Failure(RenewalAlertChannels.Telegram, RenewalAlertErrorCodes.Invalid, null);
        }

        var content = JsonContent.Create(
            new Dictionary<string, string>
            {
                ["chat_id"] = chatId,
                ["text"] = Truncate(Text(notice), 4000),
            }
        );
        return await PostAsync(
            RenewalAlertChannels.Telegram,
            $"https://api.telegram.org/bot{token}/sendMessage",
            content,
            null,
            cancellationToken
        );
    }

    private async Task<RenewalAlertChannelResult> SendWebhookAsync(
        RenewalAlertNotice notice,
        CancellationToken cancellationToken
    )
    {
        var url = notice.Channels.WebhookUrl ?? string.Empty;
        if (!RenewalAlertAddresses.IsWebhookUrl(url))
        {
            return Failure(RenewalAlertChannels.Webhook, RenewalAlertErrorCodes.Invalid, null);
        }

        var token = string.IsNullOrWhiteSpace(notice.Channels.WebhookToken)
            ? null
            : notice.Channels.WebhookToken.Trim();
        if (
            notice.Channels.WebhookFormat.Equals(
                RenewalAlertWebhookFormats.Ntfy,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(Truncate(Text(notice), 4000), Encoding.UTF8, "text/plain"),
            };
            if (notice.Subject.All(character => character < 128))
            {
                request.Headers.TryAddWithoutValidation("Title", HeaderSafe(notice.Subject));
            }

            if (token is not null)
            {
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + HeaderSafe(token));
            }

            return await SendRequestAsync(RenewalAlertChannels.Webhook, request, cancellationToken);
        }

        var json = JsonContent.Create(
            new Dictionary<string, object?>
            {
                ["title"] = notice.Subject,
                ["message"] = notice.Body,
                ["merchant"] = notice.MerchantName,
                ["amount"] = notice.Amount,
                ["account"] = notice.AccountName,
                ["nextChargeDate"] = notice.NextChargeDate?.ToString("yyyy-MM-dd"),
                ["daysUntil"] = notice.DaysUntil,
            }
        );
        return await PostAsync(RenewalAlertChannels.Webhook, url, json, token, cancellationToken);
    }

    private Task<RenewalAlertChannelResult> PostAsync(
        string channel,
        string url,
        HttpContent content,
        string? bearerToken,
        CancellationToken cancellationToken
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.TryAddWithoutValidation(
                "Authorization",
                "Bearer " + HeaderSafe(bearerToken)
            );
        }

        return SendRequestAsync(channel, request, cancellationToken);
    }

    private async Task<RenewalAlertChannelResult> SendRequestAsync(
        string channel,
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );
            if (response.IsSuccessStatusCode)
            {
                return Success(channel);
            }

            logger.LogWarning(
                "Renewal alert {Channel} returned HTTP {StatusCode}",
                channel,
                (int)response.StatusCode
            );
            return Failure(channel, RenewalAlertErrorCodes.Http, (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Renewal alert {Channel} could not be reached", channel);
            return Failure(channel, RenewalAlertErrorCodes.Unreachable, null);
        }
    }

    private static RenewalAlertChannelResult Success(string channel)
    {
        return new RenewalAlertChannelResult { Channel = channel, Succeeded = true };
    }

    private static RenewalAlertChannelResult Failure(string channel, string code, int? statusCode)
    {
        return new RenewalAlertChannelResult
        {
            Channel = channel,
            Succeeded = false,
            Error = new RenewalAlertChannelError(channel, code, statusCode),
        };
    }

    private static string Text(RenewalAlertNotice notice)
    {
        if (string.IsNullOrWhiteSpace(notice.Subject))
        {
            return notice.Body;
        }

        return notice.Subject + "\n" + notice.Body;
    }

    private static string Truncate(string value, int max)
    {
        if (value.Length <= max)
        {
            return value;
        }

        return value[..(max - 1)] + "…";
    }

    private static string HeaderSafe(string value)
    {
        return value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }
}
