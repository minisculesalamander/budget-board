namespace BudgetBoard.Database.Models;

/// <summary>
/// Optional per-subscription override for renewal alerts.
/// A missing row means the subscription uses the user's default and alerts are allowed.
/// </summary>
public class SubscriptionRenewalPreference
{
    /// <summary>
    /// Confirmed recurring rule this override belongs to. Also the primary key.
    /// </summary>
    public Guid RecurringRuleID { get; set; }

    /// <summary>
    /// Reference to the recurring rule.
    /// </summary>
    public RecurringRule? RecurringRule { get; set; }

    /// <summary>
    /// Owner of the rule. Stored for lookups. The rule relationship owns the cascade delete.
    /// </summary>
    public Guid UserID { get; set; }

    /// <summary>
    /// When false, this subscription does not send renewal alerts.
    /// </summary>
    public bool AlertsEnabled { get; set; } = true;

    /// <summary>
    /// Days before the charge to notify. Null uses the user's default.
    /// </summary>
    public int? DaysBefore { get; set; }
}
