using BudgetBoard.Database.Data;
using BudgetBoard.Database.Models;
using BudgetBoard.Service.Helpers;
using BudgetBoard.Service.Interfaces;
using BudgetBoard.Service.Models;
using BudgetBoard.Service.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace BudgetBoard.Service;

public class EnvelopeBudgetService(
    ILogger<IEnvelopeBudgetService> logger,
    UserDataContext userDataContext,
    IStringLocalizer<ResponseStrings> responseLocalizer,
    IStringLocalizer<LogStrings> logLocalizer
) : IEnvelopeBudgetService
{
    /// <inheritdoc />
    public async Task<IEnvelopeMonthResponse> ReadMonthAsync(Guid userGuid, DateOnly month)
    {
        await GetCurrentUserAsync(userGuid);
        return await BuildMonthAsync(userGuid, month);
    }

    /// <inheritdoc />
    public async Task SetEnabledAsync(Guid userGuid, bool enabled)
    {
        await GetCurrentUserAsync(userGuid);
        var settings = await userDataContext.EnvelopeSettings.FirstOrDefaultAsync(s =>
            s.UserID == userGuid
        );

        if (settings == null)
        {
            settings = new EnvelopeSettings { UserID = userGuid, Enabled = enabled };
            userDataContext.EnvelopeSettings.Add(settings);
        }
        else
        {
            settings.Enabled = enabled;
        }

        await userDataContext.SaveChangesAsync();
        logger.LogInformation("{LogMessage}", logLocalizer["EnvelopeEnabledLog", enabled]);
    }

    /// <inheritdoc />
    public async Task SetAssignedAsync(Guid userGuid, IEnvelopeAssignRequest request)
    {
        var user = await GetCurrentUserAsync(userGuid);
        var month = StartOfMonth(request.Month);
        var category = RequireEnvelope(user, request.Category);

        var assignment = await userDataContext.EnvelopeAssignments.FirstOrDefaultAsync(a =>
            a.UserID == userGuid && a.Month == month && a.Category == category
        );

        if (request.Assigned == 0)
        {
            if (assignment != null)
            {
                userDataContext.EnvelopeAssignments.Remove(assignment);
            }
        }
        else if (assignment == null)
        {
            userDataContext.EnvelopeAssignments.Add(
                new EnvelopeAssignment
                {
                    UserID = userGuid,
                    Month = month,
                    Category = category,
                    Assigned = request.Assigned,
                }
            );
        }
        else
        {
            assignment.Assigned = request.Assigned;
        }

        await userDataContext.SaveChangesAsync();
        logger.LogInformation(
            "{LogMessage}",
            logLocalizer["EnvelopeAssignedLog", category, month, request.Assigned]
        );
    }

    /// <inheritdoc />
    public async Task MoveAsync(Guid userGuid, IEnvelopeMoveRequest request)
    {
        var user = await GetCurrentUserAsync(userGuid);
        var month = StartOfMonth(request.Month);

        if (request.Amount <= 0)
        {
            throw new BudgetBoardServiceException(
                responseLocalizer["EnvelopeMoveAmountInvalidError"]
            );
        }

        var fromCategory = RequireEnvelope(user, request.FromCategory);
        var toCategory = RequireEnvelope(user, request.ToCategory);
        if (fromCategory.Equals(toCategory, StringComparison.OrdinalIgnoreCase))
        {
            throw new BudgetBoardServiceException(
                responseLocalizer["EnvelopeMoveSameCategoryError"]
            );
        }

        var monthView = await BuildMonthAsync(userGuid, month);
        var available =
            monthView
                .Groups.SelectMany(group => group.Envelopes)
                .FirstOrDefault(envelope =>
                    envelope.Category.Equals(fromCategory, StringComparison.OrdinalIgnoreCase)
                )
                ?.Available ?? 0;
        if (request.Amount > available)
        {
            throw new BudgetBoardServiceException(
                responseLocalizer["EnvelopeMoveExceedsAvailableError"]
            );
        }

        await AddAssignmentAsync(userGuid, month, fromCategory, -request.Amount);
        await AddAssignmentAsync(userGuid, month, toCategory, request.Amount);
        await userDataContext.SaveChangesAsync();
        logger.LogInformation(
            "{LogMessage}",
            logLocalizer["EnvelopeMovedLog", request.Amount, fromCategory, toCategory]
        );
    }

    /// <inheritdoc />
    public async Task SetOnBudgetAccountsAsync(
        Guid userGuid,
        IReadOnlyCollection<Guid> accountIds
    )
    {
        await GetCurrentUserAsync(userGuid);
        var desired = accountIds.Distinct().ToHashSet();
        var owned = await userDataContext
            .Accounts.Where(account =>
                account.UserID == userGuid && account.Deleted == null && desired.Contains(account.ID)
            )
            .Select(account => account.ID)
            .ToListAsync();

        if (owned.Count != desired.Count)
        {
            logger.LogError("{LogMessage}", logLocalizer["EnvelopeAccountNotFoundLog"]);
            throw new BudgetBoardServiceException(
                responseLocalizer["EnvelopeAccountNotFoundError"]
            );
        }

        var existing = await userDataContext
            .EnvelopeAccounts.Where(row => row.UserID == userGuid)
            .ToListAsync();

        foreach (var row in existing.Where(row => !desired.Contains(row.AccountID)))
        {
            userDataContext.EnvelopeAccounts.Remove(row);
        }

        var existingIds = existing.Select(row => row.AccountID).ToHashSet();
        foreach (var accountId in desired.Where(id => !existingIds.Contains(id)))
        {
            userDataContext.EnvelopeAccounts.Add(
                new EnvelopeAccount { UserID = userGuid, AccountID = accountId }
            );
        }

        await userDataContext.SaveChangesAsync();
        logger.LogInformation(
            "{LogMessage}",
            logLocalizer["EnvelopeAccountsUpdatedLog", desired.Count]
        );
    }

    /// <summary>
    /// To Be Assigned is on-budget cash that does not yet have a job.
    /// An envelope's available balance is everything assigned to it through this month, minus
    /// spending through this month, so leftover and overspending both roll into the next month.
    /// Spending reduces the account balance and the envelope by the same amount, so To Be Assigned
    /// stays put. Income and opening balances raise To Be Assigned until they are assigned.
    /// </summary>
    private async Task<EnvelopeMonthResponse> BuildMonthAsync(Guid userGuid, DateOnly month)
    {
        var user = await GetCurrentUserAsync(userGuid);
        var monthStart = StartOfMonth(month);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var categories = TransactionCategoriesHelpers.GetAllTransactionCategories(user);
        var envelopes = GetEnvelopeCategories(categories);
        var envelopeNames = envelopes.ToDictionary(
            envelope => envelope.Value,
            envelope => envelope.Value,
            StringComparer.OrdinalIgnoreCase
        );
        var categoryTypes = categories
            .GroupBy(category => category.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().CategoryType,
                StringComparer.OrdinalIgnoreCase
            );

        var enabled =
            await userDataContext
                .EnvelopeSettings.Where(settings => settings.UserID == userGuid)
                .Select(settings => (bool?)settings.Enabled)
                .FirstOrDefaultAsync() ?? false;

        var accounts = await userDataContext
            .Accounts.Where(account => account.UserID == userGuid && account.Deleted == null)
            .Select(account => new
            {
                account.ID,
                account.Name,
                InstitutionName = account.Institution != null ? account.Institution.Name : "",
            })
            .ToListAsync();

        var onBudgetIds = (
            await userDataContext
                .EnvelopeAccounts.Where(row => row.UserID == userGuid)
                .Select(row => row.AccountID)
                .ToListAsync()
        ).ToHashSet();
        var activeIds = accounts.Select(account => account.ID).ToHashSet();
        onBudgetIds.IntersectWith(activeIds);

        var balances = await userDataContext
            .Balances.Where(balance =>
                activeIds.Contains(balance.AccountID) && balance.Date <= monthEnd
            )
            .Select(balance => new
            {
                balance.AccountID,
                balance.Date,
                balance.Amount,
            })
            .ToListAsync();
        var balanceByAccount = balances
            .GroupBy(balance => balance.AccountID)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(balance => balance.Date).First().Amount
            );

        var transactions = onBudgetIds.Count == 0
            ? []
            : await userDataContext
                .Transactions.Where(transaction =>
                    onBudgetIds.Contains(transaction.AccountID)
                    && transaction.Deleted == null
                    && transaction.Date <= monthEnd
                )
                .Select(transaction => new
                {
                    transaction.Category,
                    transaction.Subcategory,
                    transaction.Amount,
                    transaction.Date,
                })
                .ToListAsync();

        var priorAssigned = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var monthAssigned = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var assignments = await userDataContext
            .EnvelopeAssignments.Where(assignment =>
                assignment.UserID == userGuid && assignment.Month <= monthStart
            )
            .Select(assignment => new
            {
                assignment.Category,
                assignment.Month,
                assignment.Assigned,
            })
            .ToListAsync();

        foreach (var assignment in assignments)
        {
            if (!envelopeNames.TryGetValue(assignment.Category, out var canonical))
            {
                continue;
            }

            var target = assignment.Month < monthStart ? priorAssigned : monthAssigned;
            target[canonical] = target.GetValueOrDefault(canonical) + assignment.Assigned;
        }

        var priorActivity = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var monthActivity = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var income = 0m;
        var outsideActivity = 0m;

        foreach (var transaction in transactions)
        {
            var inMonth = transaction.Date >= monthStart;
            var leaf = LeafCategory(transaction.Category, transaction.Subcategory);
            if (IsHidden(transaction.Category, leaf) || IsTransfer(transaction.Category, leaf, categories))
            {
                if (inMonth)
                {
                    outsideActivity += -transaction.Amount;
                }

                continue;
            }

            if (IsIncome(leaf, categoryTypes))
            {
                if (inMonth && transaction.Amount > 0)
                {
                    income += transaction.Amount;
                }
                else if (inMonth)
                {
                    outsideActivity += -transaction.Amount;
                }

                continue;
            }

            if (envelopeNames.TryGetValue(leaf, out var canonical))
            {
                var target = inMonth ? monthActivity : priorActivity;
                target[canonical] = target.GetValueOrDefault(canonical) + -transaction.Amount;
                continue;
            }

            if (!inMonth)
            {
                continue;
            }

            if (string.IsNullOrEmpty(leaf) && transaction.Amount > 0)
            {
                income += transaction.Amount;
            }
            else
            {
                outsideActivity += -transaction.Amount;
            }
        }

        var categoryResponses = envelopes
            .Select(envelope =>
            {
                var leftover =
                    priorAssigned.GetValueOrDefault(envelope.Value)
                    - priorActivity.GetValueOrDefault(envelope.Value);
                var assigned = monthAssigned.GetValueOrDefault(envelope.Value);
                var activity = monthActivity.GetValueOrDefault(envelope.Value);
                return new EnvelopeCategoryResponse
                {
                    Category = envelope.Value,
                    Parent = envelope.Parent,
                    Leftover = leftover,
                    Assigned = assigned,
                    Activity = activity,
                    Available = leftover + assigned - activity,
                };
            })
            .ToList();

        var onBudgetBalance = accounts
            .Where(account => onBudgetIds.Contains(account.ID))
            .Sum(account => balanceByAccount.GetValueOrDefault(account.ID));

        return new EnvelopeMonthResponse
        {
            Enabled = enabled,
            Month = monthStart,
            ToBeAssigned = onBudgetBalance - categoryResponses.Sum(category => category.Available),
            OnBudgetBalance = onBudgetBalance,
            Income = income,
            OutsideActivity = outsideActivity,
            Accounts = accounts
                .OrderBy(account => account.Name, StringComparer.OrdinalIgnoreCase)
                .Select(account => new EnvelopeAccountResponse
                {
                    ID = account.ID,
                    Name = account.Name,
                    InstitutionName = account.InstitutionName,
                    Balance = balanceByAccount.GetValueOrDefault(account.ID),
                    OnBudget = onBudgetIds.Contains(account.ID),
                })
                .ToList(),
            Groups = categoryResponses
                .GroupBy(
                    category =>
                        string.IsNullOrEmpty(category.Parent) ? category.Category : category.Parent,
                    StringComparer.OrdinalIgnoreCase
                )
                .Select(group => new EnvelopeGroupResponse
                {
                    Name = group.Key,
                    Envelopes = group.ToList(),
                })
                .ToList(),
        };
    }

    private async Task AddAssignmentAsync(
        Guid userGuid,
        DateOnly month,
        string category,
        decimal delta
    )
    {
        var assignment = await userDataContext.EnvelopeAssignments.FirstOrDefaultAsync(row =>
            row.UserID == userGuid && row.Month == month && row.Category == category
        );

        if (assignment == null)
        {
            userDataContext.EnvelopeAssignments.Add(
                new EnvelopeAssignment
                {
                    UserID = userGuid,
                    Month = month,
                    Category = category,
                    Assigned = delta,
                }
            );
            return;
        }

        assignment.Assigned += delta;
        if (assignment.Assigned == 0)
        {
            userDataContext.EnvelopeAssignments.Remove(assignment);
        }
    }

    private string RequireEnvelope(ApplicationUser user, string category)
    {
        var envelopes = GetEnvelopeCategories(
            TransactionCategoriesHelpers.GetAllTransactionCategories(user)
        );
        var match = envelopes.FirstOrDefault(envelope =>
            envelope.Value.Equals(category, StringComparison.OrdinalIgnoreCase)
        );
        if (match == null)
        {
            logger.LogError("{LogMessage}", logLocalizer["EnvelopeCategoryNotAssignableLog"]);
            throw new BudgetBoardServiceException(
                responseLocalizer["EnvelopeCategoryNotAssignableError"]
            );
        }

        return match.Value;
    }

    private static List<TransactionCategoryBase> GetEnvelopeCategories(
        IReadOnlyList<ITransactionCategoryResponse> categories
    )
    {
        var groupNames = new HashSet<string>(
            categories
                .Where(category => !string.IsNullOrEmpty(category.Parent))
                .Select(category => category.Parent),
            StringComparer.OrdinalIgnoreCase
        );

        return categories
            .Where(category =>
                category.CategoryType.Equals(
                    TransactionCategoryTypes.Expense,
                    StringComparison.OrdinalIgnoreCase
                )
                && !groupNames.Contains(category.Value)
                && !category.Parent.Equals(
                    TransactionCategoriesConstants.TransferCategory,
                    StringComparison.OrdinalIgnoreCase
                )
                && !category.Value.Equals(
                    TransactionCategoriesConstants.TransferCategory,
                    StringComparison.OrdinalIgnoreCase
                )
                && !category.Value.Equals(
                    TransactionCategoriesConstants.HideFromBudgetsCategory,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .Select(category => new TransactionCategoryBase
            {
                Value = category.Value,
                Parent = category.Parent,
                CategoryType = category.CategoryType,
            })
            .ToList();
    }

    private static bool IsIncome(
        string leaf,
        IReadOnlyDictionary<string, string> categoryTypes
    ) =>
        !string.IsNullOrEmpty(leaf)
        && categoryTypes.TryGetValue(leaf, out var categoryType)
        && categoryType.Equals(
            TransactionCategoryTypes.Income,
            StringComparison.OrdinalIgnoreCase
        );

    private static bool IsHidden(string? category, string leaf) =>
        leaf.Equals(
            TransactionCategoriesConstants.HideFromBudgetsCategory,
            StringComparison.OrdinalIgnoreCase
        )
        || (category ?? string.Empty).Equals(
            TransactionCategoriesConstants.HideFromBudgetsCategory,
            StringComparison.OrdinalIgnoreCase
        );

    private static bool IsTransfer(
        string? category,
        string leaf,
        IReadOnlyList<ITransactionCategoryResponse> categories
    )
    {
        if (
            (category ?? string.Empty).Equals(
                TransactionCategoriesConstants.TransferCategory,
                StringComparison.OrdinalIgnoreCase
            )
            || leaf.Equals(
                TransactionCategoriesConstants.TransferCategory,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return true;
        }

        var match = categories.FirstOrDefault(item =>
            item.Value.Equals(leaf, StringComparison.OrdinalIgnoreCase)
        );
        return match != null
            && match.Parent.Equals(
                TransactionCategoriesConstants.TransferCategory,
                StringComparison.OrdinalIgnoreCase
            );
    }

    private static string LeafCategory(string? category, string? subcategory) =>
        string.IsNullOrWhiteSpace(subcategory) ? category ?? string.Empty : subcategory;

    private static DateOnly StartOfMonth(DateOnly month) => new(month.Year, month.Month, 1);

    private async Task<ApplicationUser> GetCurrentUserAsync(Guid id)
    {
        return await UserDataServiceHelper.GetCurrentUserAsync(
            userDataContext,
            logger,
            logLocalizer,
            responseLocalizer,
            id,
            users =>
                users
                    .Include(user => user.TransactionCategories)
                    .Include(user => user.TransactionCategoryIcons)
                    .Include(user => user.UserSettings)
        );
    }
}
