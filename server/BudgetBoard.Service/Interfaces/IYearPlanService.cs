using BudgetBoard.Service.Models;

namespace BudgetBoard.Service.Interfaces;

/// <summary>
/// Multi-year income and expense plan. Planned amounts are monthly budget limits.
/// Actual amounts come from transactions.
/// </summary>
public interface IYearPlanService
{
    /// <summary>
    /// Reads the plan for one or more financial years, including planned and actual totals.
    /// </summary>
    /// <param name="userGuid">The unique identifier of the user.</param>
    /// <param name="startYear">Calendar year of the first month in the financial year.</param>
    /// <param name="yearCount">Number of financial years to include, from 1 through 5.</param>
    Task<IYearPlanResponse> ReadPlanAsync(Guid userGuid, int startYear, int yearCount);

    /// <summary>
    /// Sets the month the user's financial year begins.
    /// </summary>
    /// <param name="userGuid">The unique identifier of the user.</param>
    /// <param name="request">The settings update.</param>
    Task UpdateSettingsAsync(Guid userGuid, IPlanningSettingsUpdateRequest request);

    /// <summary>
    /// Creates, updates, or clears the budget limit for one category and month.
    /// </summary>
    /// <param name="userGuid">The unique identifier of the user.</param>
    /// <param name="request">The cell update. A blank amount clears the plan.</param>
    Task UpsertCellAsync(Guid userGuid, IYearPlanCellUpdateRequest request);

    /// <summary>
    /// Copies budget limits from a financial year into the next year where a limit is missing.
    /// </summary>
    /// <param name="userGuid">The unique identifier of the user.</param>
    /// <param name="fromYear">Calendar year of the first month in the source financial year.</param>
    Task CopyYearAsync(Guid userGuid, int fromYear);
}
