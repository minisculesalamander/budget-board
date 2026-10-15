using System.Security.Cryptography;
using System.Text.Json;
using BudgetBoard.Database.Models;
using BudgetBoard.IntegrationTests.Fakers;
using BudgetBoard.Service;
using BudgetBoard.Service.Helpers;
using BudgetBoard.Service.Interfaces;
using BudgetBoard.Service.Models;
using BudgetBoard.Service.Resources;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BudgetBoard.IntegrationTests;

[Collection("IntegrationTests")]
public class RenewalAlertServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);
    private const string WebhookUrl = "https://ntfy.example/budget";
    private const string DiscordUrl =
        "https://discord.com/api/webhooks/123456789012345678/abcdefghijklmnopqrstuvwxyz";
    private const string TelegramToken = "123456789:AAHexampletokenvalue1234567890";

    [Fact]
    public async Task SendDueAlerts_NotifiesConfirmedSubscriptionInsideWindowOnce()
    {
        var helper = new TestHelper();
        var notifier = new RecordingNotifier();
        var service = CreateService(helper, notifier);
        await AddRule(helper, new DateOnly(2026, 10, 18));
        await service.UpdateSettingsAsync(helper.demoUser.Id, WebhookSettings());

        var sent = await service.SendDueAlertsAsync(CancellationToken.None);
        var sentAgain = await service.SendDueAlertsAsync(CancellationToken.None);

        sent.Should().Be(1);
        sentAgain.Should().Be(0);
        notifier.Notices.Should().ContainSingle();
        notifier.Notices[0].Subject.Should().Be("RenewalAlertSubjectInDays [3, Netflix]");
        notifier.Notices[0].Body.Should().Be(
            "RenewalAlertBody [Netflix, 2026-10-18, 15.99, Checking]"
        );
        notifier.Notices[0].Channels.WebhookUrl.Should().Be(WebhookUrl);
        notifier.Notices[0].DaysUntil.Should().Be(3);
    }

    [Fact]
    public async Task SendDueAlerts_SkipsSuggestionsInactiveRulesAndDatesOutsideTheWindow()
    {
        var helper = new TestHelper();
        var notifier = new RecordingNotifier();
        var service = CreateService(helper, notifier);
        await AddRule(
            helper,
            new DateOnly(2026, 10, 18),
            status: RecurringRuleSuggestionStatus.Suggested
        );
        await AddRule(helper, new DateOnly(2026, 10, 18), active: false);
        await AddRule(helper, new DateOnly(2026, 11, 18), merchant: "Later");
        await service.UpdateSettingsAsync(helper.demoUser.Id, WebhookSettings());

        var sent = await service.SendDueAlertsAsync(CancellationToken.None);

        sent.Should().Be(0);
        notifier.Notices.Should().BeEmpty();
    }

    [Fact]
    public async Task SendDueAlerts_UsesPerSubscriptionDaysAndMute()
    {
        var helper = new TestHelper();
        var notifier = new RecordingNotifier();
        var service = CreateService(helper, notifier);
        var custom = await AddRule(helper, new DateOnly(2026, 10, 18), merchant: "Custom");
        var muted = await AddRule(helper, new DateOnly(2026, 10, 16), merchant: "Muted");
        await service.UpdateSettingsAsync(helper.demoUser.Id, WebhookSettings(days: 1));
        await service.UpdatePreferenceAsync(
            helper.demoUser.Id,
            custom.ID,
            new SubscriptionRenewalPreferenceUpdateRequest { AlertsEnabled = true, DaysBefore = 3 }
        );
        await service.UpdatePreferenceAsync(
            helper.demoUser.Id,
            muted.ID,
            new SubscriptionRenewalPreferenceUpdateRequest { AlertsEnabled = false, DaysBefore = 1 }
        );

        var sent = await service.SendDueAlertsAsync(CancellationToken.None);

        sent.Should().Be(1);
        notifier.Notices.Should().ContainSingle();
        notifier.Notices[0].MerchantName.Should().Be("Custom");
    }

    [Fact]
    public async Task SendDueAlerts_HonorsZeroDayWindowAndIgnoresOtherUsers()
    {
        var helper = new TestHelper();
        var notifier = new RecordingNotifier();
        var service = CreateService(helper, notifier);
        await AddRule(helper, Today, merchant: "Today");
        await AddRule(helper, Today.AddDays(1), merchant: "Tomorrow");
        var other = new ApplicationUserFaker().Generate();
        helper.UserDataContext.Users.Add(other);
        var otherAccount = new AccountFaker(other.Id).Generate();
        helper.UserDataContext.Accounts.Add(otherAccount);
        helper.UserDataContext.RecurringRules.Add(
            new RecurringRule
            {
                UserID = other.Id,
                AccountID = otherAccount.ID,
                MerchantName = "Other",
                StartDate = Today,
                Amount = 4m,
                Cadence = "{\"version\":1,\"unit\":\"Month\",\"interval\":1}",
                SuggestionStatus = RecurringRuleSuggestionStatus.Accepted,
                IsActive = true,
            }
        );
        await helper.UserDataContext.SaveChangesAsync();
        await service.UpdateSettingsAsync(helper.demoUser.Id, WebhookSettings(days: 0));

        var sent = await service.SendDueAlertsAsync(CancellationToken.None);

        sent.Should().Be(1);
        notifier.Notices.Single().MerchantName.Should().Be("Today");
    }

    [Fact]
    public async Task SendDueAlerts_RetriesOnlyTheChannelThatFailed()
    {
        var helper = new TestHelper();
        helper.demoUser.Email = "person@example.com";
        await helper.UserDataContext.SaveChangesAsync();
        var notifier = new RecordingNotifier();
        notifier.FailChannels.Add(RenewalAlertChannels.Webhook);
        var service = CreateService(helper, notifier);
        await AddRule(helper, new DateOnly(2026, 10, 18));
        await service.UpdateSettingsAsync(
            helper.demoUser.Id,
            new RenewalAlertSettingsUpdateRequest
            {
                Enabled = true,
                DaysBefore = 3,
                EmailEnabled = true,
                WebhookEnabled = true,
                WebhookUrl = WebhookUrl,
                WebhookFormat = RenewalAlertWebhookFormats.Json,
            }
        );

        var first = await service.SendDueAlertsAsync(CancellationToken.None);
        notifier.FailChannels.Clear();
        var second = await service.SendDueAlertsAsync(CancellationToken.None);
        var third = await service.SendDueAlertsAsync(CancellationToken.None);

        first.Should().Be(1);
        second.Should().Be(1);
        third.Should().Be(0);
        notifier.Notices.Should().HaveCount(2);
        notifier.Notices[0].Channels.EmailEnabled.Should().BeTrue();
        notifier.Notices[0].Channels.WebhookEnabled.Should().BeTrue();
        notifier.Notices[0].Channels.EmailAddress.Should().Be("person@example.com");
        notifier.Notices[1].Channels.EmailEnabled.Should().BeFalse();
        notifier.Notices[1].Channels.WebhookEnabled.Should().BeTrue();
        var sentChannels = helper
            .UserDataContext.RenewalAlertDeliveries.Where(row =>
                row.Status == RenewalAlertDeliveryStatus.Sent
            )
            .Select(row => row.Channel)
            .ToList();
        sentChannels
            .Should()
            .BeEquivalentTo([RenewalAlertChannels.Email, RenewalAlertChannels.Webhook]);
    }

    [Fact]
    public async Task SendDueAlerts_DoesNothingWhenAlertsAreDisabled()
    {
        var helper = new TestHelper();
        var notifier = new RecordingNotifier();
        var service = CreateService(helper, notifier);
        await AddRule(helper, new DateOnly(2026, 10, 18));
        await service.UpdateSettingsAsync(helper.demoUser.Id, WebhookSettings(enabled: false));

        var sent = await service.SendDueAlertsAsync(CancellationToken.None);

        sent.Should().Be(0);
        notifier.Notices.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateSettings_StoresSecretsEncryptedAndOmitsThemFromTheResponse()
    {
        var helper = new TestHelper();
        var service = CreateService(helper, new RecordingNotifier());

        var response = await service.UpdateSettingsAsync(
            helper.demoUser.Id,
            new RenewalAlertSettingsUpdateRequest
            {
                Enabled = true,
                DaysBefore = 3,
                DiscordEnabled = true,
                DiscordWebhookUrl = DiscordUrl,
                TelegramEnabled = true,
                TelegramBotToken = TelegramToken,
                TelegramChatId = "424242",
                WebhookFormat = RenewalAlertWebhookFormats.Json,
            }
        );

        var stored = helper.UserDataContext.RenewalAlertSettings.Single();
        stored.DiscordWebhookUrl.Should().Be("enc:" + DiscordUrl);
        stored.TelegramBotToken.Should().Be("enc:" + TelegramToken);
        stored.TelegramChatId.Should().Be("424242");
        response.DiscordWebhookConfigured.Should().BeTrue();
        response.TelegramBotTokenConfigured.Should().BeTrue();
        response.TelegramChatId.Should().Be("424242");
        var json = JsonSerializer.Serialize(response);
        json.Should().NotContain("abcdefghijklmnopqrstuvwxyz");
        json.Should().NotContain(TelegramToken);

        var kept = stored.DiscordWebhookUrl;
        await service.UpdateSettingsAsync(
            helper.demoUser.Id,
            new RenewalAlertSettingsUpdateRequest
            {
                Enabled = true,
                DaysBefore = 4,
                DiscordEnabled = true,
                TelegramEnabled = true,
                TelegramChatId = "424242",
                WebhookFormat = RenewalAlertWebhookFormats.Json,
            }
        );
        stored.DiscordWebhookUrl.Should().Be(kept);
        stored.DaysBefore.Should().Be(4);

        var cleared = await service.UpdateSettingsAsync(
            helper.demoUser.Id,
            new RenewalAlertSettingsUpdateRequest
            {
                Enabled = true,
                DaysBefore = 4,
                DiscordEnabled = false,
                DiscordWebhookUrl = new OptionalField<string?>(null),
                WebhookFormat = RenewalAlertWebhookFormats.Json,
            }
        );
        stored.DiscordWebhookUrl.Should().BeNull();
        cleared.DiscordWebhookConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateSettings_RejectsInvalidDestinationsAndDays()
    {
        var helper = new TestHelper();
        var service = CreateService(helper, new RecordingNotifier());

        var invalidDiscord = () =>
            service.UpdateSettingsAsync(
                helper.demoUser.Id,
                new RenewalAlertSettingsUpdateRequest
                {
                    DaysBefore = 3,
                    DiscordEnabled = true,
                    DiscordWebhookUrl = "https://example.com/hook",
                    WebhookFormat = RenewalAlertWebhookFormats.Json,
                }
            );
        var invalidDays = () =>
            service.UpdateSettingsAsync(
                helper.demoUser.Id,
                new RenewalAlertSettingsUpdateRequest
                {
                    DaysBefore = 91,
                    WebhookFormat = RenewalAlertWebhookFormats.Json,
                }
            );

        await invalidDiscord.Should().ThrowAsync<BudgetBoardServiceException>();
        await invalidDays.Should().ThrowAsync<BudgetBoardServiceException>();
        helper.UserDataContext.RenewalAlertSettings.Should().BeEmpty();
    }

    [Fact]
    public async Task SendTest_UsesSavedSettingsAndDoesNotRecordADelivery()
    {
        var helper = new TestHelper();
        var notifier = new RecordingNotifier();
        var service = CreateService(helper, notifier);
        await service.UpdateSettingsAsync(helper.demoUser.Id, WebhookSettings());

        await service.SendTestAsync(helper.demoUser.Id, CancellationToken.None);

        notifier.Notices.Should().ContainSingle();
        notifier.Notices[0].Subject.Should().Be("RenewalAlertTestSubject");
        helper.UserDataContext.RenewalAlertDeliveries.Should().BeEmpty();
    }

    [Fact]
    public async Task SendTest_ThrowsWhenNothingCanBeDelivered()
    {
        var helper = new TestHelper();
        var service = CreateService(helper, new RecordingNotifier());
        await service.UpdateSettingsAsync(
            helper.demoUser.Id,
            new RenewalAlertSettingsUpdateRequest
            {
                Enabled = true,
                DaysBefore = 3,
                WebhookFormat = RenewalAlertWebhookFormats.Json,
            }
        );

        var act = () => service.SendTestAsync(helper.demoUser.Id, CancellationToken.None);

        await act.Should().ThrowAsync<BudgetBoardServiceException>();
    }

    [Fact]
    public async Task UpdatePreference_StoresOverrideAndClearsItWhenUsingTheDefault()
    {
        var helper = new TestHelper();
        var service = CreateService(helper, new RecordingNotifier());
        var rule = await AddRule(helper, new DateOnly(2026, 10, 18));
        var suggested = await AddRule(
            helper,
            new DateOnly(2026, 10, 18),
            status: RecurringRuleSuggestionStatus.Suggested
        );

        var saved = await service.UpdatePreferenceAsync(
            helper.demoUser.Id,
            rule.ID,
            new SubscriptionRenewalPreferenceUpdateRequest { AlertsEnabled = true, DaysBefore = 1 }
        );
        var reset = await service.UpdatePreferenceAsync(
            helper.demoUser.Id,
            rule.ID,
            new SubscriptionRenewalPreferenceUpdateRequest { AlertsEnabled = true, DaysBefore = null }
        );
        var missing = () =>
            service.UpdatePreferenceAsync(
                helper.demoUser.Id,
                Guid.NewGuid(),
                new SubscriptionRenewalPreferenceUpdateRequest()
            );
        var unconfirmed = () =>
            service.UpdatePreferenceAsync(
                helper.demoUser.Id,
                suggested.ID,
                new SubscriptionRenewalPreferenceUpdateRequest { DaysBefore = 2 }
            );

        saved.DaysBefore.Should().Be(1);
        reset.DaysBefore.Should().BeNull();
        helper.UserDataContext.SubscriptionRenewalPreferences.Should().BeEmpty();
        await missing.Should().ThrowAsync<BudgetBoardServiceException>();
        await unconfirmed.Should().ThrowAsync<BudgetBoardServiceException>();
    }

    private static RenewalAlertService CreateService(TestHelper helper, RecordingNotifier notifier)
    {
        return new RenewalAlertService(
            helper.UserDataContext,
            new FixedNow(Today),
            notifier,
            new PrefixProtector(),
            NullLogger<RenewalAlertService>.Instance,
            TestHelper.CreateMockLocalizer<ResponseStrings>(),
            TestHelper.CreateMockLocalizer<LogStrings>()
        );
    }

    private static RenewalAlertSettingsUpdateRequest WebhookSettings(int days = 3, bool enabled = true)
    {
        return new RenewalAlertSettingsUpdateRequest
        {
            Enabled = enabled,
            DaysBefore = days,
            WebhookEnabled = true,
            WebhookUrl = WebhookUrl,
            WebhookFormat = RenewalAlertWebhookFormats.Json,
        };
    }

    private static async Task<RecurringRule> AddRule(
        TestHelper helper,
        DateOnly start,
        string merchant = "Netflix",
        RecurringRuleSuggestionStatus status = RecurringRuleSuggestionStatus.Accepted,
        bool active = true
    )
    {
        var account = new AccountFaker(helper.demoUser.Id).Generate();
        account.Name = "Checking";
        helper.UserDataContext.Accounts.Add(account);
        var rule = new RecurringRule
        {
            UserID = helper.demoUser.Id,
            AccountID = account.ID,
            MerchantName = merchant,
            StartDate = start,
            IsActive = active,
            SuggestionStatus = status,
            Amount = 15.99m,
            AmountMode = RecurringAmountMode.Fixed,
            Cadence = "{\"version\":1,\"unit\":\"Month\",\"interval\":1}",
        };
        helper.UserDataContext.RecurringRules.Add(rule);
        await helper.UserDataContext.SaveChangesAsync();
        return rule;
    }

    private sealed class FixedNow(DateOnly today) : INowProvider
    {
        public DateTime Now => today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        public DateTime UtcNow => Now;
        public DateOnly Today => today;
    }

    private sealed class PrefixProtector : IRenewalAlertSecretProtector
    {
        public string Protect(string plaintext)
        {
            return "enc:" + plaintext;
        }

        public string Unprotect(string ciphertext)
        {
            if (!ciphertext.StartsWith("enc:", StringComparison.Ordinal))
            {
                throw new CryptographicException("unreadable");
            }

            return ciphertext[4..];
        }
    }

    private sealed class RecordingNotifier : IRenewalAlertNotifier
    {
        public List<RenewalAlertNotice> Notices { get; } = [];
        public HashSet<string> FailChannels { get; } = [];
        public bool IsEmailConfigured { get; set; } = true;

        public Task<RenewalAlertSendResult> SendAsync(
            RenewalAlertNotice notice,
            CancellationToken cancellationToken
        )
        {
            Notices.Add(notice);
            var results = new List<RenewalAlertChannelResult>();
            Add(notice.Channels.EmailEnabled, RenewalAlertChannels.Email, results);
            Add(notice.Channels.DiscordEnabled, RenewalAlertChannels.Discord, results);
            Add(notice.Channels.TelegramEnabled, RenewalAlertChannels.Telegram, results);
            Add(notice.Channels.WebhookEnabled, RenewalAlertChannels.Webhook, results);
            return Task.FromResult(new RenewalAlertSendResult { Channels = results });
        }

        private void Add(bool enabled, string channel, List<RenewalAlertChannelResult> results)
        {
            if (!enabled)
            {
                return;
            }

            var failed = FailChannels.Contains(channel);
            results.Add(
                new RenewalAlertChannelResult
                {
                    Channel = channel,
                    Succeeded = !failed,
                    Error = failed
                        ? new RenewalAlertChannelError(channel, RenewalAlertErrorCodes.Failed, null)
                        : null,
                }
            );
        }
    }
}
