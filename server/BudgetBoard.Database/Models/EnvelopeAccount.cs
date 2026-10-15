namespace BudgetBoard.Database.Models;

/// <summary>
/// Marks an account as on-budget for envelope budgeting. Its balance is money that can be given a job.
/// </summary>
public class EnvelopeAccount
{
    /// <summary>
    /// Unique identifier for the membership row.
    /// </summary>
    public Guid ID { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Identifier for the on-budget account.
    /// </summary>
    public required Guid AccountID { get; set; }

    /// <summary>
    /// Reference to the on-budget account.
    /// </summary>
    public Account? Account { get; set; } = null;

    /// <summary>
    /// Identifier for the user who owns the account.
    /// </summary>
    public required Guid UserID { get; set; }

    /// <summary>
    /// Reference to the owning user.
    /// </summary>
    public ApplicationUser? User { get; set; } = null;
}
