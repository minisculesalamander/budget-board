namespace BudgetBoard.Database.Models;

/// <summary>
/// Money assigned to one category envelope for one month.
/// Leftover and overspending are not stored; they are the running balance of assignments minus spending.
/// </summary>
public class EnvelopeAssignment
{
    /// <summary>
    /// Unique identifier for the assignment.
    /// </summary>
    public Guid ID { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The month the assignment applies to, stored as the first day of that month.
    /// </summary>
    public required DateOnly Month { get; set; }

    /// <summary>
    /// The category envelope that received the money.
    /// </summary>
    public required string Category { get; set; }

    /// <summary>
    /// Amount assigned during this month. Negative when money was moved back to To Be Assigned
    /// or to another envelope.
    /// </summary>
    public required decimal Assigned { get; set; }

    /// <summary>
    /// Identifier for the user who owns the assignment.
    /// </summary>
    public required Guid UserID { get; set; }

    /// <summary>
    /// Reference to the owning user.
    /// </summary>
    public ApplicationUser? User { get; set; } = null;
}
