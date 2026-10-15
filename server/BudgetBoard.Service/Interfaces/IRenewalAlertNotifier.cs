using BudgetBoard.Service.Models;

namespace BudgetBoard.Service.Interfaces;

/// <summary>
/// Delivers a renewal alert on the channels selected in the notice.
/// </summary>
public interface IRenewalAlertNotifier
{
    /// <summary>
    /// Whether the host has SMTP settings for renewal email.
    /// </summary>
    bool IsEmailConfigured { get; }

    /// <summary>
    /// Sends the notice. One result is returned for each enabled channel.
    /// </summary>
    Task<RenewalAlertSendResult> SendAsync(
        RenewalAlertNotice notice,
        CancellationToken cancellationToken
    );
}
