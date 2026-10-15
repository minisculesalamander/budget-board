namespace BudgetBoard.Database.Models;

/// <summary>
/// Whether a renewal alert for one charge date is in progress or already sent.
/// </summary>
public enum RenewalAlertDeliveryStatus
{
    /// <summary>
    /// A send is in progress for this channel and charge date.
    /// </summary>
    Pending,

    /// <summary>
    /// This channel already delivered the alert for this charge date.
    /// </summary>
    Sent,
}

/// <summary>
/// One delivery attempt for a subscription charge on a single channel.
/// A sent row blocks a second message for that same charge date and channel.
/// </summary>
public class RenewalAlertDelivery
{
    /// <summary>
    /// Unique identifier for the delivery row.
    /// </summary>
    public Guid ID { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Owner of the subscription.
    /// </summary>
    public Guid UserID { get; set; }

    /// <summary>
    /// Subscription the alert is about.
    /// </summary>
    public Guid RecurringRuleID { get; set; }

    /// <summary>
    /// Charge date this alert refers to.
    /// </summary>
    public DateOnly OccurrenceDate { get; set; }

    /// <summary>
    /// Channel name: Email, Discord, Telegram, or Webhook.
    /// </summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Whether the send is in progress or finished.
    /// </summary>
    public RenewalAlertDeliveryStatus Status { get; set; } = RenewalAlertDeliveryStatus.Pending;

    /// <summary>
    /// When this row was claimed so overlapping runs do not double-send.
    /// </summary>
    public DateTime ClaimedAt { get; set; }

    /// <summary>
    /// When the channel accepted the message.
    /// </summary>
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// Short failure note from an earlier attempt. Secrets are never stored here.
    /// </summary>
    public string? ErrorSummary { get; set; }
}
