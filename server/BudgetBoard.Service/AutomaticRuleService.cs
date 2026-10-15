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

public class AutomaticRuleService(
    ILogger<IAutomaticRuleService> logger,
    UserDataContext userDataContext,
    ITransactionService transactionService,
    IStringLocalizer<ResponseStrings> responseLocalizer,
    IStringLocalizer<LogStrings> logLocalizer
) : IAutomaticRuleService
{
    /// <inheritdoc />
    public async Task CreateAutomaticRuleAsync(Guid userGuid, IAutomaticRuleCreateRequest request)
    {
        var userData = await GetCurrentUserAsync(userGuid);

        if (request.Conditions.Count == 0)
        {
            logger.LogError("{LogMessage}", logLocalizer["NoConditionsCreateLog"]);
            throw new BudgetBoardServiceException(responseLocalizer["NoConditionsCreateError"]);
        }

        if (request.Actions.Count == 0)
        {
            logger.LogError("{LogMessage}", logLocalizer["NoActionsCreateLog"]);
            throw new BudgetBoardServiceException(responseLocalizer["NoActionsCreateError"]);
        }

        AutomaticRuleActionValidator.Validate(request.Actions, responseLocalizer);
        var matchMode = AutomaticRuleMatcher.NormalizeMatchMode(
            request.MatchMode,
            responseLocalizer
        );
        var priority =
            request.Priority
            ?? (
                userData.AutomaticRules.Count == 0
                    ? 1
                    : userData.AutomaticRules.Max(rule => rule.Priority) + 1
            );

        var newRuleId = Guid.NewGuid();
        var newRule = new AutomaticRule
        {
            ID = newRuleId,
            UserID = userData.Id,
            MatchMode = matchMode,
            Priority = priority,
            StopProcessing = request.StopProcessing,
            Conditions =
            [
                .. request.Conditions.Select(c => new RuleCondition
                {
                    Field = c.Field,
                    Operator = c.Operator,
                    Value = c.Value,
                    RuleID = newRuleId,
                }),
            ],
            Actions =
            [
                .. request.Actions.Select(a => new RuleAction
                {
                    Field = a.Field,
                    Operator = a.Operator,
                    Value = a.Value,
                    RuleID = newRuleId,
                }),
            ],
        };

        userDataContext.AutomaticRules.Add(newRule);
        await userDataContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IAutomaticRuleResponse>> ReadAutomaticRulesAsync(Guid userGuid)
    {
        var userData = await GetCurrentUserAsync(userGuid);
        return userData
            .AutomaticRules.OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.ID)
            .Select(rule => (IAutomaticRuleResponse)new AutomaticRuleResponse(rule))
            .ToList();
    }

    /// <inheritdoc />
    public async Task UpdateAutomaticRuleAsync(Guid userGuid, IAutomaticRuleUpdateRequest request)
    {
        var userData = await GetCurrentUserAsync(userGuid);
        var existingRule = GetAutomaticRuleById(userData, request.ID);

        if (!string.IsNullOrWhiteSpace(request.MatchMode))
        {
            existingRule.MatchMode = AutomaticRuleMatcher.NormalizeMatchMode(
                request.MatchMode,
                responseLocalizer
            );
        }
        if (request.Priority.HasValue)
        {
            existingRule.Priority = request.Priority.Value;
        }
        if (request.StopProcessing.HasValue)
        {
            existingRule.StopProcessing = request.StopProcessing.Value;
        }

        if (request.Actions.Count > 0)
        {
            AutomaticRuleActionValidator.Validate(request.Actions, responseLocalizer);
        }

        if (request.Conditions.Count > 0)
        {
            userDataContext.RuleConditions.RemoveRange(existingRule.Conditions);
            foreach (var condition in request.Conditions)
            {
                userDataContext.RuleConditions.Add(
                    new RuleCondition
                    {
                        Field = condition.Field,
                        Operator = condition.Operator,
                        Value = condition.Value,
                        RuleID = existingRule.ID,
                    }
                );
            }
        }
        if (request.Actions.Count > 0)
        {
            userDataContext.RuleActions.RemoveRange(existingRule.Actions);
            foreach (var action in request.Actions)
            {
                userDataContext.RuleActions.Add(
                    new RuleAction
                    {
                        Field = action.Field,
                        Operator = action.Operator,
                        Value = action.Value,
                        RuleID = existingRule.ID,
                    }
                );
            }
        }

        await userDataContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task DeleteAutomaticRuleAsync(Guid userGuid, Guid ruleGuid)
    {
        var userData = await GetCurrentUserAsync(userGuid);
        var rule = GetAutomaticRuleById(userData, ruleGuid);

        userData.AutomaticRules.Remove(rule);
        await userDataContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<string> RunOneOffAutomaticRuleAsync(
        Guid userGuid,
        IAutomaticRuleCreateRequest request
    )
    {
        var userData = await GetCurrentUserAsync(userGuid);
        var allCategories = TransactionCategoriesHelpers.GetAllTransactionCategories(userData);

        AutomaticRuleActionValidator.Validate(request.Actions, responseLocalizer);

        var (updatedCount, _) = await RunAutomaticRule(userData, request, allCategories, null);
        logger.LogInformation("{LogMessage}", logLocalizer["RuleAppliedActionsLog", updatedCount]);

        return responseLocalizer["RuleRunSummary", updatedCount];
    }

    /// <inheritdoc />
    public async Task<AutomaticRulePreviewResponse> PreviewAutomaticRuleAsync(
        Guid userGuid,
        IAutomaticRuleCreateRequest request
    )
    {
        var userData = await GetCurrentUserAsync(userGuid);
        if (request.Conditions.Count == 0)
        {
            logger.LogError("{LogMessage}", logLocalizer["NoConditionsCreateLog"]);
            throw new BudgetBoardServiceException(responseLocalizer["NoConditionsCreateError"]);
        }

        var matchMode = AutomaticRuleMatcher.NormalizeMatchMode(
            request.MatchMode,
            responseLocalizer
        );
        var allCategories = TransactionCategoriesHelpers.GetAllTransactionCategories(userData);
        var matches = GetMatchingTransactions(
            request.Conditions,
            matchMode,
            userData.Accounts.SelectMany(account => account.Transactions),
            allCategories
        );

        return new AutomaticRulePreviewResponse
        {
            MatchCount = matches.Count,
            Matches =
            [
                .. matches
                    .OrderByDescending(transaction => transaction.Date)
                    .ThenBy(transaction => transaction.MerchantName)
                    .Take(AutomaticRulePreviewResponse.MatchLimit)
                    .Select(transaction => new AutomaticRulePreviewMatch
                    {
                        ID = transaction.ID,
                        Date = transaction.Date,
                        MerchantName = transaction.MerchantName,
                        Description = transaction.Notes,
                        Amount = transaction.Amount,
                        Category = transaction.Category,
                        Subcategory = transaction.Subcategory,
                        AccountID = transaction.AccountID,
                        AccountName = transaction.Account?.Name ?? string.Empty,
                    }),
            ],
        };
    }

    /// <inheritdoc />
    public async Task RunSavedAutomaticRulesAsync(Guid userGuid)
    {
        var userData = await GetCurrentUserAsync(userGuid);
        await RunSavedRulesAsync(userData, null);
    }

    /// <inheritdoc />
    public async Task ApplySavedRulesToTransactionsAsync(
        Guid userGuid,
        IReadOnlyCollection<Guid> transactionIds
    )
    {
        if (transactionIds.Count == 0)
        {
            return;
        }

        var userData = await GetCurrentUserAsync(userGuid);
        await RunSavedRulesAsync(userData, transactionIds.ToHashSet());
    }

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
                    .Include(u => u.AutomaticRules)
                    .ThenInclude(r => r.Conditions)
                    .Include(u => u.AutomaticRules)
                    .ThenInclude(r => r.Actions)
                    .Include(u => u.TransactionCategories)
                    .Include(u => u.Accounts)
                    .ThenInclude(a => a.Transactions)
                    .Include(u => u.UserSettings)
        );
    }

    private AutomaticRule GetAutomaticRuleById(ApplicationUser userData, Guid ruleId)
    {
        var rule = userData.AutomaticRules.FirstOrDefault(r => r.ID == ruleId);
        if (rule == null)
        {
            logger.LogError("{LogMessage}", logLocalizer["AutomaticRuleNotFoundLog"]);
            throw new BudgetBoardServiceException(responseLocalizer["AutomaticRuleNotFoundError"]);
        }
        return rule;
    }

    private async Task RunSavedRulesAsync(
        ApplicationUser userData,
        IReadOnlySet<Guid>? transactionIds
    )
    {
        var allCategories = TransactionCategoriesHelpers.GetAllTransactionCategories(userData);
        var transactions = userData.Accounts.SelectMany(account => account.Transactions);
        if (transactionIds is not null)
        {
            transactions = transactions.Where(transaction => transactionIds.Contains(transaction.ID));
        }

        var transactionList = transactions.ToList();
        var stoppedTransactionIds = new HashSet<Guid>();
        var rules = userData
            .AutomaticRules.OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.ID)
            .ToList();

        foreach (var rule in rules)
        {
            var request = new AutomaticRuleCreateRequest(new AutomaticRuleResponse(rule));
            var candidates = transactionList
                .Where(transaction => !stoppedTransactionIds.Contains(transaction.ID))
                .ToList();
            var (updatedCount, matchedTransactions) = await RunAutomaticRule(
                userData,
                request,
                allCategories,
                candidates
            );
            logger.LogInformation(
                "{LogMessage}",
                logLocalizer["RuleAppliedActionsLog", updatedCount]
            );

            if (!rule.StopProcessing)
            {
                continue;
            }

            foreach (var matchedTransaction in matchedTransactions)
            {
                stoppedTransactionIds.Add(matchedTransaction.ID);
            }
        }
    }

    private async Task<(int UpdatedCount, IReadOnlyList<Transaction> Matches)> RunAutomaticRule(
        ApplicationUser userData,
        IAutomaticRuleCreateRequest rule,
        IEnumerable<ITransactionCategory> allCategories,
        IEnumerable<Transaction>? candidates
    )
    {
        var matchedTransactions = GetMatchingTransactions(
            rule.Conditions,
            rule.MatchMode,
            candidates ?? userData.Accounts.SelectMany(account => account.Transactions),
            allCategories
        );

        logger.LogInformation(
            "{LogMessage}",
            logLocalizer["RuleMatchedTransactionsLog", matchedTransactions.Count]
        );

        var updatedCount = await ApplyActionsToTransactions(
            rule.Actions,
            matchedTransactions,
            allCategories,
            userData
        );
        return (updatedCount, matchedTransactions);
    }

    private List<Transaction> GetMatchingTransactions(
        IEnumerable<IRuleParameterRequest> conditions,
        string matchMode,
        IEnumerable<Transaction> transactions,
        IEnumerable<ITransactionCategory> allCategories
    )
    {
        try
        {
            return AutomaticRuleMatcher.Match(
                conditions,
                AutomaticRuleMatcher.NormalizeMatchMode(matchMode, responseLocalizer),
                transactions,
                allCategories,
                responseLocalizer
            );
        }
        catch (BudgetBoardServiceException exception)
        {
            logger.LogError(exception, "{LogMessage}", logLocalizer["ErrorApplyingConditionLog"]);
            throw;
        }
    }

    private async Task<int> ApplyActionsToTransactions(
        IEnumerable<IRuleParameterRequest> actions,
        IEnumerable<Transaction> transactions,
        IEnumerable<ITransactionCategory> allCategories,
        ApplicationUser userData
    )
    {
        var actionContext = new AutomaticRuleActionContext
        {
            UserDataContext = userDataContext,
            UserId = userData.Id,
            AllTransactions = userData.Accounts.SelectMany(account => account.Transactions),
        };
        int updatedCount = 0;
        foreach (var action in actions)
        {
            try
            {
                updatedCount += await AutomaticRuleActionHandler.ApplyActionToTransactions(
                    action,
                    [.. transactions],
                    allCategories,
                    transactionService,
                    userData.Id,
                    responseLocalizer,
                    actionContext
                );
            }
            catch (BudgetBoardServiceException bbex)
            {
                logger.LogError(
                    bbex,
                    "{LogMessage}",
                    logLocalizer["ErrorApplyingActionLog", bbex.Message]
                );
                continue;
            }
        }
        return updatedCount;
    }
}
