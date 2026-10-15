using BudgetBoard.Service.Models;

namespace BudgetBoard.Service.Interfaces;

/// <summary>
/// Opt-in envelope budget. Gives on-budget cash a job for the month without changing category limits.
/// </summary>
public interface IEnvelopeBudgetService
{
    /// <summary>
    /// Builds the envelope budget for a month, including To Be Assigned and each category balance.
    /// </summary>
    /// <param name="userGuid">The unique identifier of the user.</param>
    /// <param name="month">Any date in the month to read.</param>
    Task<IEnvelopeMonthResponse> ReadMonthAsync(Guid userGuid, DateOnly month);

    /// <summary>
    /// Turns envelope budgeting on or off. Saved assignments and on-budget accounts are kept.
    /// </summary>
    /// <param name="userGuid">The unique identifier of the user.</param>
    /// <param name="enabled">True to opt in.</param>
    Task SetEnabledAsync(Guid userGuid, bool enabled);

    /// <summary>
    /// Sets the amount assigned to one envelope for a month. Zero clears the assignment.
    /// </summary>
    /// <param name="userGuid">The unique identifier of the user.</param>
    /// <param name="request">The category, month, and assigned amount.</param>
    Task SetAssignedAsync(Guid userGuid, IEnvelopeAssignRequest request);

    /// <summary>
    /// Moves money from one envelope to another in a month. To Be Assigned does not change.
    /// </summary>
    /// <param name="userGuid">The unique identifier of the user.</param>
    /// <param name="request">The source, destination, month, and amount.</param>
    Task MoveAsync(Guid userGuid, IEnvelopeMoveRequest request);

    /// <summary>
    /// Replaces the set of accounts whose balances fund To Be Assigned.
    /// </summary>
    /// <param name="userGuid">The unique identifier of the user.</param>
    /// <param name="accountIds">Account identifiers to mark on-budget.</param>
    Task SetOnBudgetAccountsAsync(Guid userGuid, IReadOnlyCollection<Guid> accountIds);
}
