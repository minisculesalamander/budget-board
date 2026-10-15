namespace BudgetBoard.Database.Models;

/// <summary>
/// Whether a recurring rule was confirmed by the user or is still a detection suggestion.
/// </summary>
public enum RecurringRuleSuggestionStatus
{
    /// <summary>
    /// The rule is confirmed and participates in forecasting and automatic matching.
    /// </summary>
    Accepted,

    /// <summary>
    /// The rule was detected from transactions and is waiting for the user to accept or dismiss it.
    /// </summary>
    Suggested,

    /// <summary>
    /// The user dismissed this detection. It is kept so the same charge is not suggested again.
    /// </summary>
    Dismissed,
}

/// <summary>
/// Defines how the amount for a recurring rule is determined.
/// </summary>
public enum RecurringAmountMode
{
    /// <summary>
    /// Uses the configured fixed amount.
    /// </summary>
    Fixed,

    /// <summary>
    /// Determines the amount automatically from matching transactions.
    /// </summary>
    Automatic,
}

/// <summary>
/// Represents a rule for identifying recurring transactions.
/// </summary>
public class RecurringRule
{
    /// <summary>
    /// Unique identifier for the recurring rule.
    /// </summary>
    public Guid ID { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Identifier for the user who owns the rule.
    /// </summary>
    public required Guid UserID { get; set; }

    /// <summary>
    /// Reference to the user who owns the rule.
    /// </summary>
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// Identifier for the account associated with the rule.
    /// </summary>
    public required Guid AccountID { get; set; }

    /// <summary>
    /// Reference to the account associated with the rule.
    /// </summary>
    public Account? Account { get; set; }

    /// <summary>
    /// Merchant name used to match transactions, if specified.
    /// </summary>
    public string? MerchantName { get; set; }

    /// <summary>
    /// Category used to match transactions, if specified.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Subcategory used to match transactions, if specified.
    /// </summary>
    public string? Subcategory { get; set; }

    /// <summary>
    /// JSON cadence definition at which the rule repeats.
    /// </summary>
    public string Cadence { get; set; } = "{\"version\":1,\"unit\":\"Month\",\"interval\":1}";

    /// <summary>
    /// Date on which the recurring rule begins.
    /// </summary>
    public required DateOnly StartDate { get; set; }

    /// <summary>
    /// Date on which the recurring rule ends, if specified.
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Indicates whether the recurring rule is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this rule is confirmed, a detection suggestion, or a dismissed suggestion.
    /// </summary>
    public RecurringRuleSuggestionStatus SuggestionStatus { get; set; } =
        RecurringRuleSuggestionStatus.Accepted;

    /// <summary>
    /// Describes how the recurring amount is determined.
    /// </summary>
    public RecurringAmountMode AmountMode { get; set; } = RecurringAmountMode.Fixed;

    /// <summary>
    /// Fixed amount associated with the recurring rule.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Transactions identified by the recurring rule.
    /// </summary>
    public ICollection<Transaction> Transactions { get; set; } = [];
}
