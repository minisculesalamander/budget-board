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

public class YearPlanService(
    ILogger<IYearPlanService> logger,
    UserDataContext userDataContext,
    IBudgetService budgetService,
    IStringLocalizer<ResponseStrings> responseLocalizer,
    IStringLocalizer<LogStrings> logLocalizer
) : IYearPlanService
{
    private const int MinimumYear = 2000;
    private const int MaximumYear = 2100;
    private const int MinimumYearCount = 1;
    private const int MaximumYearCount = 5;
    private const decimal MaximumAmount = 1_000_000_000m;

    /// <inheritdoc />
    public async Task<IYearPlanResponse> ReadPlanAsync(Guid userGuid, int startYear, int yearCount)
    {
        if (!IsValidRange(startYear, yearCount))
        {
            logger.LogError(
                "{LogMessage}",
                logLocalizer["YearPlanInvalidRangeLog", startYear, yearCount]
            );
            throw new BudgetBoardServiceException(responseLocalizer["YearPlanInvalidRangeError"]);
        }

        var user = await GetUserAsync(userGuid);
        var startMonth = await GetFinancialYearStartMonthAsync(userGuid);
        var months = BuildMonths(startYear, startMonth, yearCount);
        var categories = LoadCategories(user);
        var budgets = await LoadBudgetsAsync(userGuid, months);
        var actuals = await LoadActualsAsync(userGuid, months, categories);

        var sections = BuildSections(categories, months, budgets, actuals);
        var monthTotals = BuildMonthTotals(months, startMonth, sections);
        var yearTotals = BuildYearTotals(monthTotals);

        return new YearPlanResponse
        {
            FinancialYearStartMonth = startMonth,
            StartYear = startYear,
            YearCount = yearCount,
            Months = months,
            Sections = sections,
            MonthTotals = monthTotals,
            YearTotals = yearTotals,
        };
    }

    /// <inheritdoc />
    public async Task UpdateSettingsAsync(Guid userGuid, IPlanningSettingsUpdateRequest request)
    {
        await GetUserAsync(userGuid);

        if (request.FinancialYearStartMonth is < 1 or > 12)
        {
            logger.LogError(
                "{LogMessage}",
                logLocalizer["YearPlanInvalidMonthLog", request.FinancialYearStartMonth]
            );
            throw new BudgetBoardServiceException(responseLocalizer["YearPlanInvalidMonthError"]);
        }

        var settings = await userDataContext.PlanningSettings.FirstOrDefaultAsync(s =>
            s.UserID == userGuid
        );
        if (settings == null)
        {
            userDataContext.PlanningSettings.Add(
                new PlanningSettings
                {
                    UserID = userGuid,
                    FinancialYearStartMonth = request.FinancialYearStartMonth,
                }
            );
        }
        else
        {
            settings.FinancialYearStartMonth = request.FinancialYearStartMonth;
        }

        await userDataContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task UpsertCellAsync(Guid userGuid, IYearPlanCellUpdateRequest request)
    {
        var user = await GetUserAsync(userGuid);
        var categories = LoadCategories(user);
        if (!TryResolveCategory(categories, request.Category, out var category))
        {
            logger.LogError(
                "{LogMessage}",
                logLocalizer["YearPlanCategoryNotAllowedLog", request.Category]
            );
            throw new BudgetBoardServiceException(
                responseLocalizer["YearPlanCategoryNotAllowedError", request.Category]
            );
        }

        var month = StartOfMonth(request.Month);
        if (string.IsNullOrWhiteSpace(request.Amount))
        {
            await ClearCellAsync(userGuid, category, month, categories);
            return;
        }

        if (!TryEvaluatePlanAmount(request.Amount, out var limit))
        {
            logger.LogError(
                "{LogMessage}",
                logLocalizer["YearPlanAmountInvalidLog", request.Amount]
            );
            throw new BudgetBoardServiceException(responseLocalizer["YearPlanAmountInvalidError"]);
        }

        var existing = await userDataContext
            .Budgets.AsNoTracking()
            .FirstOrDefaultAsync(b =>
                b.UserID == userGuid
                && b.Month.Year == month.Year
                && b.Month.Month == month.Month
                && b.Category.ToLower() == category.Value.ToLower()
            );

        if (existing == null)
        {
            await budgetService.CreateBudgetsAsync(
                userGuid,
                [
                    new BudgetCreateRequest
                    {
                        Month = month,
                        Category = category.Value,
                        Limit = limit,
                    },
                ],
                autoManageParents: true
            );
            return;
        }

        await budgetService.UpdateBudgetAsync(
            userGuid,
            new BudgetUpdateRequest
            {
                ID = existing.ID,
                Limit = limit,
                RolloverStartMonth = existing.RolloverStartMonth,
            }
        );
    }

    /// <inheritdoc />
    public async Task CopyYearAsync(Guid userGuid, int fromYear)
    {
        if (fromYear < MinimumYear || fromYear >= MaximumYear)
        {
            logger.LogError(
                "{LogMessage}",
                logLocalizer["YearPlanInvalidRangeLog", fromYear, 1]
            );
            throw new BudgetBoardServiceException(responseLocalizer["YearPlanInvalidRangeError"]);
        }

        var user = await GetUserAsync(userGuid);
        var startMonth = await GetFinancialYearStartMonthAsync(userGuid);
        var sourceMonths = BuildMonths(fromYear, startMonth, 1);
        var categories = LoadCategories(user);
        var allowed = categories.Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sourceBudgets = await LoadBudgetsAsync(userGuid, sourceMonths);
        var targetMonths = sourceMonths.Select(month => month.AddMonths(12)).ToList();
        var targetBudgets = await LoadBudgetsAsync(userGuid, targetMonths);
        var targetKeys = targetBudgets
            .Select(b => Key(b.Category, b.Month))
            .ToHashSet();

        var requests = new List<BudgetCreateRequest>();
        foreach (var budget in sourceBudgets)
        {
            if (!allowed.Contains(budget.Category))
            {
                continue;
            }

            var targetMonth = StartOfMonth(budget.Month).AddMonths(12);
            if (targetKeys.Contains(Key(budget.Category, targetMonth)))
            {
                continue;
            }

            requests.Add(
                new BudgetCreateRequest
                {
                    Month = targetMonth,
                    Category = budget.Category,
                    Limit = budget.Limit,
                }
            );
        }

        if (requests.Count == 0)
        {
            return;
        }

        await budgetService.CreateBudgetsAsync(userGuid, requests, autoManageParents: false);
    }

    private async Task ClearCellAsync(
        Guid userGuid,
        PlanCategory category,
        DateOnly month,
        IReadOnlyList<PlanCategory> categories
    )
    {
        var monthBudgets = await userDataContext
            .Budgets.Where(b =>
                b.UserID == userGuid && b.Month.Year == month.Year && b.Month.Month == month.Month
            )
            .ToListAsync();
        var budget = monthBudgets.FirstOrDefault(b =>
            b.Category.Equals(category.Value, StringComparison.OrdinalIgnoreCase)
        );
        if (budget == null)
        {
            return;
        }

        if (!category.IsParent)
        {
            var parentName = category.Parent;
            var childNames = categories
                .Where(c =>
                    !c.IsParent
                    && c.Parent.Equals(parentName, StringComparison.OrdinalIgnoreCase)
                )
                .Select(c => c.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var childBudgets = monthBudgets
                .Where(b => childNames.Contains(b.Category))
                .ToList();
            var oldSum = childBudgets.Sum(b => b.Limit);
            var parentBudget = monthBudgets.FirstOrDefault(b =>
                b.Category.Equals(parentName, StringComparison.OrdinalIgnoreCase)
            );
            var newSum = oldSum - budget.Limit;

            userDataContext.Budgets.Remove(budget);
            if (parentBudget != null && parentBudget.Limit == oldSum)
            {
                if (newSum == 0)
                {
                    userDataContext.Budgets.Remove(parentBudget);
                }
                else
                {
                    parentBudget.Limit = newSum;
                }
            }
        }
        else
        {
            userDataContext.Budgets.Remove(budget);
        }

        await userDataContext.SaveChangesAsync();
    }

    private async Task<ApplicationUser> GetUserAsync(Guid userGuid)
    {
        var user = await userDataContext
            .ApplicationUsers.AsNoTracking()
            .Include(u => u.TransactionCategories)
            .Include(u => u.UserSettings)
            .FirstOrDefaultAsync(u => u.Id == userGuid);

        if (user == null)
        {
            logger.LogError("{LogMessage}", logLocalizer["InvalidUserErrorLog"]);
            throw new BudgetBoardServiceException(responseLocalizer["InvalidUserError"]);
        }

        return user;
    }

    private async Task<int> GetFinancialYearStartMonthAsync(Guid userGuid)
    {
        var startMonth = await userDataContext
            .PlanningSettings.AsNoTracking()
            .Where(s => s.UserID == userGuid)
            .Select(s => (int?)s.FinancialYearStartMonth)
            .FirstOrDefaultAsync();

        return startMonth is >= 1 and <= 12 ? startMonth.Value : 1;
    }

    private async Task<List<Budget>> LoadBudgetsAsync(Guid userGuid, IReadOnlyList<DateOnly> months)
    {
        var start = months[0];
        var endExclusive = months[^1].AddMonths(1);
        return await userDataContext
            .Budgets.AsNoTracking()
            .Where(b => b.UserID == userGuid && b.Month >= start && b.Month < endExclusive)
            .ToListAsync();
    }

    private async Task<ActualMaps> LoadActualsAsync(
        Guid userGuid,
        IReadOnlyList<DateOnly> months,
        IReadOnlyList<PlanCategory> categories
    )
    {
        var start = months[0];
        var endInclusive = months[^1].AddMonths(1).AddDays(-1);
        var monthSet = months.Select(month => (month.Year, month.Month)).ToHashSet();
        var byName = categories.ToDictionary(c => c.Value, StringComparer.OrdinalIgnoreCase);

        var transactions = await userDataContext
            .Transactions.AsNoTracking()
            .Where(t =>
                t.Account!.UserID == userGuid
                && t.Account.HideTransactions == false
                && t.Deleted == null
                && t.Date >= start
                && t.Date <= endInclusive
            )
            .Select(t => new
            {
                t.Category,
                t.Subcategory,
                t.Date,
                t.Amount,
            })
            .ToListAsync();

        var parents = new Dictionary<(string Category, int Year, int Month), decimal>();
        var children = new Dictionary<(string Category, int Year, int Month), decimal>();

        foreach (var transaction in transactions)
        {
            if (!monthSet.Contains((transaction.Date.Year, transaction.Date.Month)))
            {
                continue;
            }

            if (!TryResolveTransaction(byName, transaction.Subcategory, transaction.Category, out var category))
            {
                continue;
            }

            var contribution =
                category.Kind == TransactionCategoryTypes.Income
                    ? transaction.Amount
                    : -transaction.Amount;
            var parentKey = (
                (category.IsParent ? category.Value : category.Parent).ToLowerInvariant(),
                transaction.Date.Year,
                transaction.Date.Month
            );
            parents[parentKey] = parents.GetValueOrDefault(parentKey) + contribution;

            if (!category.IsParent)
            {
                var childKey = (
                    category.Value.ToLowerInvariant(),
                    transaction.Date.Year,
                    transaction.Date.Month
                );
                children[childKey] = children.GetValueOrDefault(childKey) + contribution;
            }
        }

        return new ActualMaps(parents, children);
    }

    private static List<YearPlanSectionResponse> BuildSections(
        IReadOnlyList<PlanCategory> categories,
        IReadOnlyList<DateOnly> months,
        IReadOnlyList<Budget> budgets,
        ActualMaps actuals
    )
    {
        var budgetsByKey = new Dictionary<(string, int, int), Budget>();
        foreach (var budget in budgets)
        {
            budgetsByKey.TryAdd(Key(budget.Category, budget.Month), budget);
        }

        var sections = new List<YearPlanSectionResponse>();
        foreach (var kind in new[] { TransactionCategoryTypes.Income, TransactionCategoryTypes.Expense })
        {
            var parents = categories
                .Where(c => c.IsParent && c.Kind == kind)
                .OrderBy(c => c.Value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            sections.Add(
                new YearPlanSectionResponse
                {
                    Kind = kind,
                    Parents = parents
                        .Select(parent =>
                        {
                            var children = categories
                                .Where(c =>
                                    !c.IsParent
                                    && c.Kind == kind
                                    && c.Parent.Equals(parent.Value, StringComparison.OrdinalIgnoreCase)
                                )
                                .OrderBy(c => c.Value, StringComparer.OrdinalIgnoreCase)
                                .ToList();

                            return new YearPlanParentResponse
                            {
                                Category = parent.Value,
                                Cells = months
                                    .Select(month =>
                                        ParentCell(parent, children, month, budgetsByKey, actuals)
                                    )
                                    .ToList(),
                                Children = children
                                    .Select(child => new YearPlanChildResponse
                                    {
                                        Category = child.Value,
                                        Cells = months
                                            .Select(month =>
                                                ChildCell(child, month, budgetsByKey, actuals)
                                            )
                                            .ToList(),
                                    })
                                    .ToList(),
                            };
                        })
                        .ToList(),
                }
            );
        }

        return sections;
    }

    private static YearPlanCellResponse ParentCell(
        PlanCategory parent,
        IReadOnlyList<PlanCategory> children,
        DateOnly month,
        IReadOnlyDictionary<(string, int, int), Budget> budgetsByKey,
        ActualMaps actuals
    )
    {
        budgetsByKey.TryGetValue(Key(parent.Value, month), out var parentBudget);
        var childBudgets = children
            .Select(child =>
                budgetsByKey.TryGetValue(Key(child.Value, month), out var childBudget)
                    ? childBudget
                    : null
            )
            .Where(budget => budget != null)
            .Cast<Budget>()
            .ToList();

        decimal? planned = parentBudget?.Limit;
        if (planned == null && childBudgets.Count > 0)
        {
            planned = childBudgets.Sum(budget => budget.Limit);
        }

        return new YearPlanCellResponse
        {
            Month = month,
            BudgetId = parentBudget?.ID,
            Planned = planned,
            Actual = actuals.Parents.GetValueOrDefault(
                (parent.Value.ToLowerInvariant(), month.Year, month.Month)
            ),
        };
    }

    private static YearPlanCellResponse ChildCell(
        PlanCategory child,
        DateOnly month,
        IReadOnlyDictionary<(string, int, int), Budget> budgetsByKey,
        ActualMaps actuals
    )
    {
        budgetsByKey.TryGetValue(Key(child.Value, month), out var budget);
        return new YearPlanCellResponse
        {
            Month = month,
            BudgetId = budget?.ID,
            Planned = budget?.Limit,
            Actual = actuals.Children.GetValueOrDefault(
                (child.Value.ToLowerInvariant(), month.Year, month.Month)
            ),
        };
    }

    private static List<YearPlanMonthTotalResponse> BuildMonthTotals(
        IReadOnlyList<DateOnly> months,
        int startMonth,
        IReadOnlyList<YearPlanSectionResponse> sections
    )
    {
        var income = sections.First(section => section.Kind == TransactionCategoryTypes.Income);
        var expense = sections.First(section => section.Kind == TransactionCategoryTypes.Expense);

        return months
            .Select(month =>
            {
                var plannedIncome = SumPlanned(income, month);
                var actualIncome = SumActual(income, month);
                var plannedExpense = SumPlanned(expense, month);
                var actualExpense = SumActual(expense, month);
                return new YearPlanMonthTotalResponse
                {
                    Month = month,
                    FinancialYear = FinancialYear(month, startMonth),
                    PlannedIncome = plannedIncome,
                    ActualIncome = actualIncome,
                    PlannedExpense = plannedExpense,
                    ActualExpense = actualExpense,
                    PlannedSavings = plannedIncome - plannedExpense,
                    ActualSavings = actualIncome - actualExpense,
                };
            })
            .ToList();
    }

    private static List<YearPlanYearTotalResponse> BuildYearTotals(
        IReadOnlyList<YearPlanMonthTotalResponse> monthTotals
    ) =>
        monthTotals
            .GroupBy(total => total.FinancialYear)
            .OrderBy(group => group.Key)
            .Select(group => new YearPlanYearTotalResponse
            {
                FinancialYear = group.Key,
                PlannedIncome = group.Sum(total => total.PlannedIncome),
                ActualIncome = group.Sum(total => total.ActualIncome),
                PlannedExpense = group.Sum(total => total.PlannedExpense),
                ActualExpense = group.Sum(total => total.ActualExpense),
                PlannedSavings = group.Sum(total => total.PlannedSavings),
                ActualSavings = group.Sum(total => total.ActualSavings),
            })
            .ToList();

    private static decimal SumPlanned(YearPlanSectionResponse section, DateOnly month) =>
        section.Parents.Sum(parent =>
            parent.Cells.First(cell => cell.Month == month).Planned ?? 0
        );

    private static decimal SumActual(YearPlanSectionResponse section, DateOnly month) =>
        section.Parents.Sum(parent => parent.Cells.First(cell => cell.Month == month).Actual);

    private static IReadOnlyList<PlanCategory> LoadCategories(ApplicationUser user) =>
        TransactionCategoriesHelpers
            .GetAllTransactionCategories(user)
            .Where(category => !IsExcluded(category.Value))
            .Where(category =>
                category.CategoryType == TransactionCategoryTypes.Income
                || category.CategoryType == TransactionCategoryTypes.Expense
            )
            .Select(category => new PlanCategory(
                category.Value,
                category.Parent,
                category.CategoryType,
                string.IsNullOrEmpty(category.Parent)
            ))
            .ToList();

    private static bool TryResolveCategory(
        IReadOnlyList<PlanCategory> categories,
        string name,
        out PlanCategory category
    )
    {
        var match = categories.FirstOrDefault(c =>
            c.Value.Equals(name, StringComparison.OrdinalIgnoreCase)
        );
        if (match == null)
        {
            category = null!;
            return false;
        }

        category = match;
        return true;
    }

    private static bool TryResolveTransaction(
        IReadOnlyDictionary<string, PlanCategory> byName,
        string? subcategory,
        string? category,
        out PlanCategory resolved
    )
    {
        if (
            !string.IsNullOrWhiteSpace(subcategory)
            && byName.TryGetValue(subcategory, out var specific)
        )
        {
            resolved = specific;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(category) && byName.TryGetValue(category, out var parent))
        {
            resolved = parent;
            return true;
        }

        resolved = null!;
        return false;
    }

    private static bool TryEvaluatePlanAmount(string text, out decimal amount)
    {
        amount = 0;
        try
        {
            var expression = AmountExpressionParser.Parse(text.Trim());
            var atZero = expression.Evaluate(0);
            var atOne = expression.Evaluate(1);
            if (atZero != atOne)
            {
                return false;
            }

            amount = decimal.Round(atZero, 2, MidpointRounding.AwayFromZero);
            return amount >= 0 && amount <= MaximumAmount;
        }
        catch (AmountExpressionException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool IsExcluded(string value) =>
        value.Equals(
            TransactionCategoriesConstants.HideFromBudgetsCategory,
            StringComparison.OrdinalIgnoreCase
        )
        || value.Equals(
            TransactionCategoriesConstants.TransferCategory,
            StringComparison.OrdinalIgnoreCase
        );

    private static bool IsValidRange(int startYear, int yearCount) =>
        startYear >= MinimumYear
        && startYear <= MaximumYear
        && yearCount >= MinimumYearCount
        && yearCount <= MaximumYearCount
        && startYear + yearCount - 1 <= MaximumYear;

    private static List<DateOnly> BuildMonths(int startYear, int startMonth, int yearCount)
    {
        var start = new DateOnly(startYear, startMonth, 1);
        return Enumerable.Range(0, yearCount * 12).Select(start.AddMonths).ToList();
    }

    private static int FinancialYear(DateOnly month, int startMonth) =>
        month.Month >= startMonth ? month.Year : month.Year - 1;

    private static DateOnly StartOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private static (string Category, int Year, int Month) Key(string category, DateOnly month) =>
        (category.ToLowerInvariant(), month.Year, month.Month);

    private sealed record PlanCategory(string Value, string Parent, string Kind, bool IsParent);

    private sealed record ActualMaps(
        Dictionary<(string Category, int Year, int Month), decimal> Parents,
        Dictionary<(string Category, int Year, int Month), decimal> Children
    );
}
