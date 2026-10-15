using BudgetBoard.Service.Models;

namespace BudgetBoard.Service.Interfaces;

/// <summary>
/// Stores renewal-alert settings and sends reminders before confirmed subscription charges.
/// </summary>
public interface IRenewalAlertService
{
    /// <summary>
    /// Reads the user's alert settings. Secrets are not included.
    /// </summary>
    Task<RenewalAlertSettingsResponse> ReadSettingsAsync(Guid userId);

    /// <summary>
    /// Saves alert settings. Omitted secrets are left unchanged.
    /// </summary>
    Task<RenewalAlertSettingsResponse> UpdateSettingsAsync(
        Guid userId,
        RenewalAlertSettingsUpdateRequest request
    );

    /// <summary>
    /// Reads per-subscription alert overrides.
    /// </summary>
    Task<RenewalAlertPreferenceListResponse> ReadPreferencesAsync(Guid userId);

    /// <summary>
    /// Saves a per-subscription override. The default is stored by deleting the override row.
    /// </summary>
    Task<SubscriptionRenewalPreferenceResponse> UpdatePreferenceAsync(
        Guid userId,
        Guid recurringRuleId,
        SubscriptionRenewalPreferenceUpdateRequest request
    );

    /// <summary>
    /// Sends a test message with the saved settings and does not record a delivery.
    /// </summary>
    Task SendTestAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Sends due alerts for every user who has alerts enabled.
    /// </summary>
    /// <returns>The number of channel deliveries that succeeded.</returns>
    Task<int> SendDueAlertsAsync(CancellationToken cancellationToken);
}
