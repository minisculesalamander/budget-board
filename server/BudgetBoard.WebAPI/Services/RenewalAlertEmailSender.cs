using BudgetBoard.Utils;
using BudgetBoard.WebAPI.Resources;
using Microsoft.Extensions.Localization;

namespace BudgetBoard.WebAPI.Services;

public interface IRenewalAlertEmailSender
{
    bool IsConfigured { get; }

    Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Sends renewal mail through the same SMTP settings used for account email.
/// </summary>
public sealed class RenewalAlertEmailSender(
    IConfiguration configuration,
    IStringLocalizer<ApiResponseStrings> responseLocalizer
) : IRenewalAlertEmailSender
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configuration["EMAIL_SENDER"])
        && !string.IsNullOrWhiteSpace(configuration["EMAIL_SMTP_HOST"]);

    public Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sender = new EmailSender(configuration, responseLocalizer);
        return sender.SendEmailAsync(to, subject, htmlBody);
    }
}
