namespace BudgetBoard.Database.Models;

/// <summary>
/// Per-user options for the multi-year plan.
/// Monthly income and expense amounts stay on <see cref="Budget"/> rows.
/// </summary>
public class PlanningSettings
{
    /// <summary>
    /// Unique identifier for the settings row.
    /// </summary>
    public Guid ID { get; set; } = Guid.NewGuid();

    /// <summary>
    /// First month of the financial year, from 1 (January) through 12 (December).
    /// </summary>
    public int FinancialYearStartMonth { get; set; } = 1;

    /// <summary>
    /// Identifier for the user who owns these settings.
    /// </summary>
    public required Guid UserID { get; set; }

    /// <summary>
    /// Reference to the owning user.
    /// </summary>
    public ApplicationUser? User { get; set; } = null;
}
