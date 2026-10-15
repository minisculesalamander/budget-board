namespace BudgetBoard.Database.Models;

/// <summary>
/// Opt-in envelope budgeting preferences for one user. Monthly category limits stay in <see cref="Budget"/>.
/// </summary>
public class EnvelopeSettings
{
    /// <summary>
    /// Unique identifier for the settings row.
    /// </summary>
    public Guid ID { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Whether the envelope budget page is turned on. Assignments are kept when this is false.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Identifier for the user who owns these settings.
    /// </summary>
    public required Guid UserID { get; set; }

    /// <summary>
    /// Reference to the owning user.
    /// </summary>
    public ApplicationUser? User { get; set; } = null;
}
