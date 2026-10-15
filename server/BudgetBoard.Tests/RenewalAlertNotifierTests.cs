using System.Net;
using BudgetBoard.Service.Models;
using BudgetBoard.WebAPI.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BudgetBoard.IntegrationTests;

[Collection("IntegrationTests")]
public class RenewalAlertNotifierTests
{
    private const string DiscordUrl =
        "https://discord.com/api/webhooks/123456789012345678/abcdefghijklmnopqrstuvwxyz";
    private const string TelegramToken = "123456789:AAHexampletokenvalue1234567890";

    [Fact]
    public async Task SendAsync_PostsDiscordTelegramAndJsonWebhook()
    {
        var handler = new StubHandler();
        var notifier = CreateNotifier(handler, new FakeEmail());
        var notice = Sample(
            new RenewalAlertChannelPayload
            {
                DiscordEnabled = true,
                DiscordWebhookUrl = DiscordUrl,
                TelegramEnabled = true,
                TelegramBotToken = TelegramToken,
                TelegramChatId = "424242",
                WebhookEnabled = true,
                WebhookUrl = "https://hooks.example/budget",
                WebhookToken = "secret-token",
                WebhookFormat = RenewalAlertWebhookFormats.Json,
            }
        );

        var result = await notifier.SendAsync(notice, CancellationToken.None);

        result.Channels.Should().OnlyContain(channel => channel.Succeeded);
        handler.Calls.Should().HaveCount(3);
        handler.Calls[0].Uri.Should().Be(DiscordUrl);
        handler.Calls[0].Body.Should().Contain("Netflix");
        handler.Calls[1].Uri.Should().Be(
            $"https://api.telegram.org/bot{TelegramToken}/sendMessage"
        );
        handler.Calls[1].Body.Should().Contain("\"chat_id\":\"424242\"");
        handler.Calls[2].Body.Should().Contain("\"merchant\":\"Netflix\"");
        handler.Calls[2].Body.Should().Contain("\"nextChargeDate\":\"2026-10-18\"");
        handler.Calls[2].Authorization.Should().Be("Bearer secret-token");
    }

    [Fact]
    public async Task SendAsync_PostsNtfyAsPlainText()
    {
        var handler = new StubHandler();
        var notifier = CreateNotifier(handler, new FakeEmail());
        var notice = Sample(
            new RenewalAlertChannelPayload
            {
                WebhookEnabled = true,
                WebhookUrl = "https://ntfy.example/budget",
                WebhookToken = "tk_example",
                WebhookFormat = RenewalAlertWebhookFormats.Ntfy,
            }
        );

        var result = await notifier.SendAsync(notice, CancellationToken.None);

        result.Channels.Single().Succeeded.Should().BeTrue();
        handler.Calls.Single().Body.Should().Contain("Netflix is scheduled");
        handler.Calls.Single().Title.Should().Be("Renews in 3 days: Netflix");
        handler.Calls.Single().Authorization.Should().Be("Bearer tk_example");
        handler.Calls.Single().ContentType.Should().Contain("text/plain");
    }

    [Fact]
    public async Task SendAsync_ReportsHttpFailuresWithoutThrowing()
    {
        var handler = new StubHandler { Status = HttpStatusCode.BadGateway };
        var notifier = CreateNotifier(handler, new FakeEmail());
        var notice = Sample(
            new RenewalAlertChannelPayload
            {
                WebhookEnabled = true,
                WebhookUrl = "https://hooks.example/budget",
                WebhookFormat = RenewalAlertWebhookFormats.Json,
            }
        );

        var result = await notifier.SendAsync(notice, CancellationToken.None);

        result.Channels.Single().Succeeded.Should().BeFalse();
        result.Channels.Single().Error!.Code.Should().Be(RenewalAlertErrorCodes.Http);
        result.Channels.Single().Error!.StatusCode.Should().Be(502);
    }

    [Fact]
    public async Task SendAsync_DoesNotCallDiscordForANonDiscordUrl()
    {
        var handler = new StubHandler();
        var notifier = CreateNotifier(handler, new FakeEmail());
        var notice = Sample(
            new RenewalAlertChannelPayload
            {
                DiscordEnabled = true,
                DiscordWebhookUrl = "https://example.com/not-discord",
            }
        );

        var result = await notifier.SendAsync(notice, CancellationToken.None);

        result.Channels.Single().Error!.Code.Should().Be(RenewalAlertErrorCodes.Invalid);
        handler.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_ReportsMissingSmtpAndSendsWhenConfigured()
    {
        var handler = new StubHandler();
        var unconfigured = new FakeEmail();
        var missing = await CreateNotifier(handler, unconfigured)
            .SendAsync(
                Sample(
                    new RenewalAlertChannelPayload
                    {
                        EmailEnabled = true,
                        EmailAddress = "person@example.com",
                    }
                ),
                CancellationToken.None
            );
        var configured = new FakeEmail { IsConfigured = true };
        var sent = await CreateNotifier(handler, configured)
            .SendAsync(
                Sample(
                    new RenewalAlertChannelPayload
                    {
                        EmailEnabled = true,
                        EmailAddress = "person@example.com",
                    }
                ),
                CancellationToken.None
            );

        missing.Channels.Single().Error!.Code.Should().Be(RenewalAlertErrorCodes.SmtpMissing);
        sent.Channels.Single().Succeeded.Should().BeTrue();
        configured.Sent.Should().ContainSingle();
        configured.Sent[0].To.Should().Be("person@example.com");
        configured.Sent[0].HtmlBody.Should().Contain("Netflix");
        handler.Calls.Should().BeEmpty();
    }

    [Fact]
    public void Protector_RoundTripsWithoutStoringPlaintext()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        using var provider = services.BuildServiceProvider();
        var protector = new DataProtectionRenewalAlertSecretProtector(
            provider.GetRequiredService<IDataProtectionProvider>()
        );

        var cipher = protector.Protect(DiscordUrl);

        cipher.Should().NotContain("webhooks");
        protector.Unprotect(cipher).Should().Be(DiscordUrl);
    }

    private static RenewalAlertNotifier CreateNotifier(StubHandler handler, FakeEmail email)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(clientFactory => clientFactory.CreateClient(RenewalAlertNotifier.HttpClientName))
            .Returns(() => new HttpClient(handler, disposeHandler: false));
        return new RenewalAlertNotifier(
            factory.Object,
            email,
            NullLogger<RenewalAlertNotifier>.Instance
        );
    }

    private static RenewalAlertNotice Sample(RenewalAlertChannelPayload channels)
    {
        return new RenewalAlertNotice
        {
            Subject = "Renews in 3 days: Netflix",
            Body = "Netflix is scheduled to charge on 2026-10-18.",
            MerchantName = "Netflix",
            Amount = 15.99m,
            AccountName = "Checking",
            NextChargeDate = new DateOnly(2026, 10, 18),
            DaysUntil = 3,
            Channels = channels,
        };
    }

    private sealed class FakeEmail : IRenewalAlertEmailSender
    {
        public bool IsConfigured { get; set; }
        public List<(string To, string Subject, string HtmlBody)> Sent { get; } = [];

        public Task SendAsync(
            string to,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken
        )
        {
            Sent.Add((to, subject, htmlBody));
            return Task.CompletedTask;
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.NoContent;
        public List<CapturedCall> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            request.Headers.TryGetValues("Authorization", out var authorization);
            request.Headers.TryGetValues("Title", out var title);
            Calls.Add(
                new CapturedCall(
                    request.RequestUri?.ToString(),
                    body,
                    authorization is null ? null : string.Join(" ", authorization),
                    title is null ? null : string.Join(" ", title),
                    request.Content?.Headers.ContentType?.ToString()
                )
            );
            return new HttpResponseMessage(Status);
        }
    }

    private sealed record CapturedCall(
        string? Uri,
        string? Body,
        string? Authorization,
        string? Title,
        string? ContentType
    );
}
