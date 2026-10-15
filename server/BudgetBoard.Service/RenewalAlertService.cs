using System.Globalization;
using System.Security.Cryptography;
using BudgetBoard.Database.Data;
using BudgetBoard.Database.Models;
using BudgetBoard.Service.Helpers;
using BudgetBoard.Service.Interfaces;
using BudgetBoard.Service.Models;
using BudgetBoard.Service.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace BudgetBoard.Service;

public class RenewalAlertService(
    UserDataContext userDataContext,
    INowProvider nowProvider,
    IRenewalAlertNotifier notifier,
    IRenewalAlertSecretProtector secretProtector,
    ILogger<RenewalAlertService> logger,
    IStringLocalizer<ResponseStrings> responseLocalizer,
    IStringLocalizer<LogStrings> logLocalizer
) : IRenewalAlertService
{
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(15);

    /// <inheritdoc />
    public async Task<RenewalAlertSettingsResponse> ReadSettingsAsync(Guid userId)
    {
        await EnsureUserAsync(userId);
        var settings = await userDataContext
            .RenewalAlertSettings.AsNoTracking()
            .FirstOrDefaultAsync(row => row.UserID == userId);
        return ToResponse(settings);
    }

    /// <inheritdoc />
    public async Task<RenewalAlertSettingsResponse> UpdateSettingsAsync(
        Guid userId,
        RenewalAlertSettingsUpdateRequest request
    )
    {
        await EnsureUserAsync(userId);
        var daysBefore = NormalizeDaysBefore(request.DaysBefore);
        var emailAddress = NormalizeOptionalText(request.EmailAddress);
        if (emailAddress is not null && !RenewalAlertAddresses.IsEmail(emailAddress))
        {
            throw new BudgetBoardServiceException(responseLocalizer["RenewalAlertEmailInvalidError"]);
        }

        var chatId = NormalizeOptionalText(request.TelegramChatId);
        if (chatId is not null && !RenewalAlertAddresses.IsTelegramChatId(chatId))
        {
            throw new BudgetBoardServiceException(
                responseLocalizer["RenewalAlertTelegramChatInvalidError"]
            );
        }

        var format = request.WebhookFormat?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!RenewalAlertAddresses.IsWebhookFormat(format))
        {
            throw new BudgetBoardServiceException(
                responseLocalizer["RenewalAlertWebhookFormatInvalidError"]
            );
        }

        var settings = await userDataContext.RenewalAlertSettings.FirstOrDefaultAsync(row =>
            row.UserID == userId
        );
        if (settings is null)
        {
            settings = new RenewalAlertSettings { UserID = userId };
            userDataContext.RenewalAlertSettings.Add(settings);
        }

        settings.Enabled = request.Enabled;
        settings.DaysBefore = daysBefore;
        settings.EmailEnabled = request.EmailEnabled;
        settings.EmailAddress = emailAddress;
        settings.DiscordEnabled = request.DiscordEnabled;
        settings.DiscordWebhookUrl = MergeSecret(
            request.DiscordWebhookUrl,
            settings.DiscordWebhookUrl,
            value =>
            {
                if (!RenewalAlertAddresses.IsDiscordWebhook(value))
                {
                    throw new BudgetBoardServiceException(
                        responseLocalizer["RenewalAlertDiscordWebhookInvalidError"]
                    );
                }

                return secretProtector.Protect(value);
            }
        );
        settings.TelegramEnabled = request.TelegramEnabled;
        settings.TelegramBotToken = MergeSecret(
            request.TelegramBotToken,
            settings.TelegramBotToken,
            value =>
            {
                if (!RenewalAlertAddresses.IsTelegramToken(value))
                {
                    throw new BudgetBoardServiceException(
                        responseLocalizer["RenewalAlertTelegramTokenInvalidError"]
                    );
                }

                return secretProtector.Protect(value);
            }
        );
        settings.TelegramChatId = chatId;
        settings.WebhookEnabled = request.WebhookEnabled;
        settings.WebhookUrl = MergeSecret(
            request.WebhookUrl,
            settings.WebhookUrl,
            value =>
            {
                if (!RenewalAlertAddresses.IsWebhookUrl(value))
                {
                    throw new BudgetBoardServiceException(
                        responseLocalizer["RenewalAlertWebhookUrlInvalidError"]
                    );
                }

                return secretProtector.Protect(value);
            }
        );
        settings.WebhookToken = MergeSecret(
            request.WebhookToken,
            settings.WebhookToken,
            value =>
            {
                if (value.Length > RenewalAlertLimits.MaxSecretLength)
                {
                    throw new BudgetBoardServiceException(
                        responseLocalizer["RenewalAlertSecretTooLongError"]
                    );
                }

                return secretProtector.Protect(value);
            }
        );
        settings.WebhookFormat = format;

        await userDataContext.SaveChangesAsync();
        return ToResponse(settings);
    }

    /// <inheritdoc />
    public async Task<RenewalAlertPreferenceListResponse> ReadPreferencesAsync(Guid userId)
    {
        await EnsureUserAsync(userId);
        var settings = await userDataContext
            .RenewalAlertSettings.AsNoTracking()
            .FirstOrDefaultAsync(row => row.UserID == userId);
        var preferences = await userDataContext
            .SubscriptionRenewalPreferences.AsNoTracking()
            .Where(row => row.UserID == userId)
            .Select(row => new SubscriptionRenewalPreferenceResponse
            {
                RecurringRuleID = row.RecurringRuleID,
                AlertsEnabled = row.AlertsEnabled,
                DaysBefore = row.DaysBefore,
            })
            .ToListAsync();

        return new RenewalAlertPreferenceListResponse
        {
            DefaultDaysBefore = settings?.DaysBefore ?? RenewalAlertLimits.DefaultDaysBefore,
            AlertsEnabled = settings?.Enabled ?? false,
            Preferences = preferences,
        };
    }

    /// <inheritdoc />
    public async Task<SubscriptionRenewalPreferenceResponse> UpdatePreferenceAsync(
        Guid userId,
        Guid recurringRuleId,
        SubscriptionRenewalPreferenceUpdateRequest request
    )
    {
        await EnsureUserAsync(userId);
        var rule = await userDataContext.RecurringRules.FirstOrDefaultAsync(row =>
            row.ID == recurringRuleId && row.UserID == userId
        );
        if (rule is null)
        {
            throw new BudgetBoardServiceException(responseLocalizer["RecurringRuleNotFoundError"]);
        }

        if (rule.SuggestionStatus != RecurringRuleSuggestionStatus.Accepted)
        {
            throw new BudgetBoardServiceException(
                responseLocalizer["RenewalAlertRuleNotConfirmedError"]
            );
        }

        if (
            request.DaysBefore is int days
            && (days < RenewalAlertLimits.MinDaysBefore || days > RenewalAlertLimits.MaxDaysBefore)
        )
        {
            throw new BudgetBoardServiceException(responseLocalizer["RenewalAlertDaysBeforeError"]);
        }

        var existing = await userDataContext.SubscriptionRenewalPreferences.FirstOrDefaultAsync(
            row => row.RecurringRuleID == recurringRuleId
        );
        if (request.AlertsEnabled && request.DaysBefore is null)
        {
            if (existing is not null)
            {
                userDataContext.SubscriptionRenewalPreferences.Remove(existing);
                await userDataContext.SaveChangesAsync();
            }

            return new SubscriptionRenewalPreferenceResponse
            {
                RecurringRuleID = recurringRuleId,
                AlertsEnabled = true,
                DaysBefore = null,
            };
        }

        if (existing is null)
        {
            existing = new SubscriptionRenewalPreference
            {
                RecurringRuleID = recurringRuleId,
                UserID = userId,
            };
            userDataContext.SubscriptionRenewalPreferences.Add(existing);
        }

        existing.AlertsEnabled = request.AlertsEnabled;
        existing.DaysBefore = request.DaysBefore;
        await userDataContext.SaveChangesAsync();
        return new SubscriptionRenewalPreferenceResponse
        {
            RecurringRuleID = recurringRuleId,
            AlertsEnabled = existing.AlertsEnabled,
            DaysBefore = existing.DaysBefore,
        };
    }

    /// <inheritdoc />
    public async Task SendTestAsync(Guid userId, CancellationToken cancellationToken)
    {
        await EnsureUserAsync(userId);
        var settings = await userDataContext.RenewalAlertSettings.FirstOrDefaultAsync(
            row => row.UserID == userId,
            cancellationToken
        );
        if (settings is null)
        {
            throw new BudgetBoardServiceException(responseLocalizer["RenewalAlertNoChannelError"]);
        }

        var userEmail = await userDataContext
            .ApplicationUsers.Where(user => user.Id == userId)
            .Select(user => user.Email)
            .FirstOrDefaultAsync(cancellationToken);
        var decryptErrors = new List<RenewalAlertChannelError>();
        var payload = BuildPayload(settings, userEmail, decryptErrors);
        var notice = new RenewalAlertNotice
        {
            Subject = responseLocalizer["RenewalAlertTestSubject"],
            Body = responseLocalizer["RenewalAlertTestBody"],
            Channels = payload,
        };
        var result = await notifier.SendAsync(notice, cancellationToken);
        var failures = result
            .Channels.Where(channel => !channel.Succeeded)
            .Select(channel => channel.Error)
            .OfType<RenewalAlertChannelError>()
            .Concat(decryptErrors)
            .ToList();
        if (result.Channels.Count == 0 || failures.Count > 0)
        {
            var message =
                failures.Count == 0
                    ? responseLocalizer["RenewalAlertNoChannelError"]
                    : string.Join(" ", failures.Select(Describe));
            throw new BudgetBoardServiceException(message);
        }
    }

    /// <inheritdoc />
    public async Task<int> SendDueAlertsAsync(CancellationToken cancellationToken)
    {
        var userIds = await userDataContext
            .RenewalAlertSettings.Where(row => row.Enabled)
            .Select(row => row.UserID)
            .ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var userId in userIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                sent += await SendDueAlertsForUserAsync(userId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "{LogMessage}",
                    logLocalizer["RenewalAlertUserErrorLog", userId]
                );
            }
        }

        return sent;
    }

    private async Task<int> SendDueAlertsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        var settings = await userDataContext.RenewalAlertSettings.FirstOrDefaultAsync(
            row => row.UserID == userId && row.Enabled,
            cancellationToken
        );
        if (settings is null)
        {
            return 0;
        }

        var userEmail = await userDataContext
            .ApplicationUsers.Where(user => user.Id == userId)
            .Select(user => user.Email)
            .FirstOrDefaultAsync(cancellationToken);
        var decryptErrors = new List<RenewalAlertChannelError>();
        var payload = BuildPayload(settings, userEmail, decryptErrors);
        foreach (var error in decryptErrors)
        {
            logger.LogWarning(
                "{LogMessage}",
                logLocalizer["RenewalAlertChannelFailedLog", error.Channel, error.Code, 0]
            );
        }

        var preferences = await userDataContext
            .SubscriptionRenewalPreferences.Where(row => row.UserID == userId)
            .ToDictionaryAsync(row => row.RecurringRuleID, cancellationToken);
        var rules = await userDataContext
            .RecurringRules.AsSplitQuery()
            .Include(rule => rule.Account)
            .Include(rule => rule.Transactions)
            .Where(rule =>
                rule.UserID == userId
                && rule.IsActive
                && rule.SuggestionStatus == RecurringRuleSuggestionStatus.Accepted
            )
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var rule in rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sent += await SendRuleAlertAsync(
                settings,
                payload,
                decryptErrors,
                preferences.GetValueOrDefault(rule.ID),
                rule,
                cancellationToken
            );
        }

        return sent;
    }

    private async Task<int> SendRuleAlertAsync(
        RenewalAlertSettings settings,
        RenewalAlertChannelPayload payload,
        IReadOnlyCollection<RenewalAlertChannelError> decryptErrors,
        SubscriptionRenewalPreference? preference,
        RecurringRule rule,
        CancellationToken cancellationToken
    )
    {
        if (preference is { AlertsEnabled: false })
        {
            return 0;
        }

        var daysBefore = preference?.DaysBefore ?? settings.DaysBefore;
        if (daysBefore < RenewalAlertLimits.MinDaysBefore)
        {
            daysBefore = RenewalAlertLimits.MinDaysBefore;
        }
        else if (daysBefore > RenewalAlertLimits.MaxDaysBefore)
        {
            daysBefore = RenewalAlertLimits.MaxDaysBefore;
        }

        var today = nowProvider.Today;
        IReadOnlyList<DateOnly> occurrences;
        try
        {
            occurrences = RecurringRuleOccurrenceCalculator.GetOccurrences(
                rule,
                today,
                today.AddDays(daysBefore),
                responseLocalizer
            );
        }
        catch (RecurringCadenceValidationException)
        {
            return 0;
        }

        if (occurrences.Count == 0)
        {
            return 0;
        }

        var next = occurrences[0];
        var daysUntil = next.DayNumber - today.DayNumber;
        if (daysUntil < 0 || daysUntil > daysBefore)
        {
            return 0;
        }

        var deliverable = DeliverableChannels(payload, decryptErrors);
        if (deliverable.Count == 0)
        {
            return 0;
        }

        var existing = await userDataContext
            .RenewalAlertDeliveries.Where(row =>
                row.RecurringRuleID == rule.ID && row.OccurrenceDate == next
            )
            .ToListAsync(cancellationToken);
        var utcNow = nowProvider.UtcNow;
        var todo = new List<string>();
        foreach (var channel in deliverable)
        {
            var row = existing.FirstOrDefault(delivery => delivery.Channel == channel);
            if (row?.Status == RenewalAlertDeliveryStatus.Sent)
            {
                continue;
            }

            if (
                row?.Status == RenewalAlertDeliveryStatus.Pending
                && row.ClaimedAt > utcNow.Subtract(ClaimLease)
            )
            {
                continue;
            }

            todo.Add(channel);
        }

        if (todo.Count == 0)
        {
            return 0;
        }

        var claimed = new List<RenewalAlertDelivery>();
        foreach (var channel in todo)
        {
            var row = existing.FirstOrDefault(delivery => delivery.Channel == channel);
            if (row is null)
            {
                row = new RenewalAlertDelivery
                {
                    UserID = rule.UserID,
                    RecurringRuleID = rule.ID,
                    OccurrenceDate = next,
                    Channel = channel,
                };
                userDataContext.RenewalAlertDeliveries.Add(row);
            }

            row.Status = RenewalAlertDeliveryStatus.Pending;
            row.ClaimedAt = utcNow;
            row.SentAt = null;
            row.ErrorSummary = null;
            claimed.Add(row);
        }

        try
        {
            await userDataContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            foreach (var row in claimed)
            {
                if (userDataContext.Entry(row).State == EntityState.Added)
                {
                    userDataContext.Entry(row).State = EntityState.Detached;
                }
            }

            return 0;
        }

        var notice = BuildChargeNotice(rule, next, daysUntil, ForChannels(payload, todo));
        RenewalAlertSendResult result;
        try
        {
            result = await notifier.SendAsync(notice, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{LogMessage}", logLocalizer["RenewalAlertUserErrorLog", rule.UserID]);
            userDataContext.RenewalAlertDeliveries.RemoveRange(claimed);
            await userDataContext.SaveChangesAsync(cancellationToken);
            return 0;
        }

        var sent = 0;
        foreach (var row in claimed)
        {
            var channelResult = result.Channels.FirstOrDefault(channel => channel.Channel == row.Channel);
            if (channelResult?.Succeeded == true)
            {
                row.Status = RenewalAlertDeliveryStatus.Sent;
                row.SentAt = nowProvider.UtcNow;
                row.ErrorSummary = null;
                sent++;
                continue;
            }

            if (channelResult?.Error is RenewalAlertChannelError error)
            {
                logger.LogWarning(
                    "{LogMessage}",
                    logLocalizer[
                        "RenewalAlertChannelFailedLog",
                        error.Channel,
                        error.Code,
                        error.StatusCode ?? 0
                    ]
                );
            }

            userDataContext.RenewalAlertDeliveries.Remove(row);
        }

        await userDataContext.SaveChangesAsync(cancellationToken);
        return sent;
    }

    private RenewalAlertNotice BuildChargeNotice(
        RecurringRule rule,
        DateOnly next,
        int daysUntil,
        RenewalAlertChannelPayload channels
    )
    {
        var merchant = string.IsNullOrWhiteSpace(rule.MerchantName)
            ? responseLocalizer["RenewalAlertUnknownMerchant"].Value
            : rule.MerchantName;
        var account = string.IsNullOrWhiteSpace(rule.Account?.Name)
            ? responseLocalizer["RenewalAlertUnknownAccount"].Value
            : rule.Account!.Name;
        var amount = RecurringRuleAmounts
            .GetForecastAmount(rule)
            .ToString("0.##", CultureInfo.InvariantCulture);
        var date = next.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var subject = daysUntil switch
        {
            0 => responseLocalizer["RenewalAlertSubjectToday", merchant].Value,
            1 => responseLocalizer["RenewalAlertSubjectTomorrow", merchant].Value,
            _ => responseLocalizer["RenewalAlertSubjectInDays", daysUntil, merchant].Value,
        };

        return new RenewalAlertNotice
        {
            Subject = subject,
            Body = responseLocalizer["RenewalAlertBody", merchant, date, amount, account].Value,
            MerchantName = merchant,
            Amount = RecurringRuleAmounts.GetForecastAmount(rule),
            AccountName = account,
            NextChargeDate = next,
            DaysUntil = daysUntil,
            Channels = channels,
        };
    }

    private RenewalAlertChannelPayload BuildPayload(
        RenewalAlertSettings settings,
        string? userEmail,
        ICollection<RenewalAlertChannelError> decryptErrors
    )
    {
        var recipient = string.IsNullOrWhiteSpace(settings.EmailAddress)
            ? userEmail
            : settings.EmailAddress;
        return new RenewalAlertChannelPayload
        {
            EmailEnabled = settings.EmailEnabled,
            EmailAddress = recipient,
            DiscordEnabled = settings.DiscordEnabled,
            DiscordWebhookUrl = TryUnprotect(
                settings.DiscordWebhookUrl,
                RenewalAlertChannels.Discord,
                decryptErrors
            ),
            TelegramEnabled = settings.TelegramEnabled,
            TelegramBotToken = TryUnprotect(
                settings.TelegramBotToken,
                RenewalAlertChannels.Telegram,
                decryptErrors
            ),
            TelegramChatId = settings.TelegramChatId,
            WebhookEnabled = settings.WebhookEnabled,
            WebhookUrl = TryUnprotect(
                settings.WebhookUrl,
                RenewalAlertChannels.Webhook,
                decryptErrors
            ),
            WebhookToken = TryUnprotect(
                settings.WebhookToken,
                RenewalAlertChannels.Webhook,
                decryptErrors
            ),
            WebhookFormat = settings.WebhookFormat,
        };
    }

    private string? TryUnprotect(
        string? ciphertext,
        string channel,
        ICollection<RenewalAlertChannelError> errors
    )
    {
        if (string.IsNullOrEmpty(ciphertext))
        {
            return null;
        }

        try
        {
            return secretProtector.Unprotect(ciphertext);
        }
        catch (CryptographicException ex)
        {
            logger.LogError(
                ex,
                "{LogMessage}",
                logLocalizer["RenewalAlertSecretUnreadableLog", channel]
            );
            errors.Add(
                new RenewalAlertChannelError(channel, RenewalAlertErrorCodes.Unreadable, null)
            );
            return null;
        }
    }

    private List<string> DeliverableChannels(
        RenewalAlertChannelPayload payload,
        IReadOnlyCollection<RenewalAlertChannelError> decryptErrors
    )
    {
        var unreadable = decryptErrors
            .Where(error => error.Code == RenewalAlertErrorCodes.Unreadable)
            .Select(error => error.Channel)
            .ToHashSet(StringComparer.Ordinal);
        var channels = new List<string>();
        if (
            payload.EmailEnabled
            && notifier.IsEmailConfigured
            && RenewalAlertAddresses.IsEmail(payload.EmailAddress ?? string.Empty)
            && !unreadable.Contains(RenewalAlertChannels.Email)
        )
        {
            channels.Add(RenewalAlertChannels.Email);
        }

        if (
            payload.DiscordEnabled
            && RenewalAlertAddresses.IsDiscordWebhook(payload.DiscordWebhookUrl ?? string.Empty)
            && !unreadable.Contains(RenewalAlertChannels.Discord)
        )
        {
            channels.Add(RenewalAlertChannels.Discord);
        }

        if (
            payload.TelegramEnabled
            && RenewalAlertAddresses.IsTelegramToken(payload.TelegramBotToken ?? string.Empty)
            && RenewalAlertAddresses.IsTelegramChatId(payload.TelegramChatId ?? string.Empty)
            && !unreadable.Contains(RenewalAlertChannels.Telegram)
        )
        {
            channels.Add(RenewalAlertChannels.Telegram);
        }

        if (
            payload.WebhookEnabled
            && RenewalAlertAddresses.IsWebhookUrl(payload.WebhookUrl ?? string.Empty)
            && !unreadable.Contains(RenewalAlertChannels.Webhook)
        )
        {
            channels.Add(RenewalAlertChannels.Webhook);
        }

        return channels;
    }

    private static RenewalAlertChannelPayload ForChannels(
        RenewalAlertChannelPayload source,
        IReadOnlyCollection<string> channels
    )
    {
        var selected = channels.ToHashSet(StringComparer.Ordinal);
        return new RenewalAlertChannelPayload
        {
            EmailEnabled = selected.Contains(RenewalAlertChannels.Email),
            EmailAddress = selected.Contains(RenewalAlertChannels.Email) ? source.EmailAddress : null,
            DiscordEnabled = selected.Contains(RenewalAlertChannels.Discord),
            DiscordWebhookUrl = selected.Contains(RenewalAlertChannels.Discord)
                ? source.DiscordWebhookUrl
                : null,
            TelegramEnabled = selected.Contains(RenewalAlertChannels.Telegram),
            TelegramBotToken = selected.Contains(RenewalAlertChannels.Telegram)
                ? source.TelegramBotToken
                : null,
            TelegramChatId = selected.Contains(RenewalAlertChannels.Telegram)
                ? source.TelegramChatId
                : null,
            WebhookEnabled = selected.Contains(RenewalAlertChannels.Webhook),
            WebhookUrl = selected.Contains(RenewalAlertChannels.Webhook) ? source.WebhookUrl : null,
            WebhookToken = selected.Contains(RenewalAlertChannels.Webhook) ? source.WebhookToken : null,
            WebhookFormat = source.WebhookFormat,
        };
    }

    private string? MergeSecret(
        OptionalField<string?> incoming,
        string? existingCiphertext,
        Func<string, string> validateAndProtect
    )
    {
        if (!incoming.IsSpecified)
        {
            return existingCiphertext;
        }

        var value = incoming.Value?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return validateAndProtect(value);
    }

    private RenewalAlertSettingsResponse ToResponse(RenewalAlertSettings? settings)
    {
        if (settings is null)
        {
            return new RenewalAlertSettingsResponse { SmtpConfigured = notifier.IsEmailConfigured };
        }

        return new RenewalAlertSettingsResponse
        {
            Enabled = settings.Enabled,
            DaysBefore = settings.DaysBefore,
            EmailEnabled = settings.EmailEnabled,
            EmailAddress = settings.EmailAddress,
            DiscordEnabled = settings.DiscordEnabled,
            DiscordWebhookConfigured = !string.IsNullOrEmpty(settings.DiscordWebhookUrl),
            TelegramEnabled = settings.TelegramEnabled,
            TelegramBotTokenConfigured = !string.IsNullOrEmpty(settings.TelegramBotToken),
            TelegramChatId = settings.TelegramChatId,
            WebhookEnabled = settings.WebhookEnabled,
            WebhookUrlConfigured = !string.IsNullOrEmpty(settings.WebhookUrl),
            WebhookTokenConfigured = !string.IsNullOrEmpty(settings.WebhookToken),
            WebhookFormat = settings.WebhookFormat,
            SmtpConfigured = notifier.IsEmailConfigured,
        };
    }

    private int NormalizeDaysBefore(int daysBefore)
    {
        if (daysBefore < RenewalAlertLimits.MinDaysBefore || daysBefore > RenewalAlertLimits.MaxDaysBefore)
        {
            throw new BudgetBoardServiceException(responseLocalizer["RenewalAlertDaysBeforeError"]);
        }

        return daysBefore;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private string Describe(RenewalAlertChannelError error)
    {
        return error.Code switch
        {
            RenewalAlertErrorCodes.SmtpMissing => responseLocalizer["RenewalAlertSmtpMissingError"],
            RenewalAlertErrorCodes.EmailMissing => responseLocalizer["RenewalAlertEmailMissingError"],
            RenewalAlertErrorCodes.Unreachable => responseLocalizer[
                "RenewalAlertUnreachableError",
                error.Channel
            ],
            RenewalAlertErrorCodes.Http => responseLocalizer[
                "RenewalAlertHttpError",
                error.Channel,
                error.StatusCode ?? 0
            ],
            RenewalAlertErrorCodes.Unreadable => responseLocalizer[
                "RenewalAlertSecretUnreadableError",
                error.Channel
            ],
            RenewalAlertErrorCodes.Invalid => responseLocalizer[
                "RenewalAlertChannelFailedError",
                error.Channel
            ],
            _ => responseLocalizer["RenewalAlertChannelFailedError", error.Channel],
        };
    }

    private async Task EnsureUserAsync(Guid userId)
    {
        var exists = await userDataContext.ApplicationUsers.AnyAsync(user => user.Id == userId);
        if (!exists)
        {
            logger.LogError("{LogMessage}", logLocalizer["InvalidUserErrorLog"]);
            throw new BudgetBoardServiceException(responseLocalizer["InvalidUserError"]);
        }
    }
}
