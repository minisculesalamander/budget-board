using BudgetBoard.Database.Data;
using BudgetBoard.Database.Models;
using BudgetBoard.Service.Interfaces;
using BudgetBoard.Service.Models;
using BudgetBoard.Service.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BudgetBoard.Service.Helpers;

internal sealed class AutomaticRuleActionContext
{
    public required UserDataContext UserDataContext { get; init; }
    public required Guid UserId { get; init; }
    public required IEnumerable<Transaction> AllTransactions { get; init; }
}

internal static class AutomaticRuleLinkActions
{
    private const int TransferDateWindowDays = 3;

    internal static async Task<int> MarkAsTransferAsync(
        IRuleParameterRequest action,
        IList<Transaction> transactions,
        IEnumerable<ITransactionCategory> allCategories,
        ITransactionService transactionService,
        AutomaticRuleActionContext context,
        IStringLocalizer<ResponseStrings> responseLocalizer
    )
    {
        Guid? targetAccountId = null;
        if (!string.IsNullOrWhiteSpace(action.Value))
        {
            if (!Guid.TryParse(action.Value, out var parsedAccountId))
            {
                throw new BudgetBoardServiceException(
                    responseLocalizer["AutomaticRuleInvalidAccountIdError", action.Value]
                );
            }

            targetAccountId = parsedAccountId;
        }

        var updateRequests = new List<ITransactionUpdateRequest>();
        foreach (var transaction in transactions)
        {
            var updateRequest = new TransactionUpdateRequest(transaction);
            (updateRequest.Category, updateRequest.Subcategory) =
                TransactionCategoriesHelpers.GetFullCategory(
                    TransactionCategoriesConstants.TransferCategory,
                    allCategories
                );
            updateRequests.Add(updateRequest);
        }

        if (updateRequests.Count > 0)
        {
            await transactionService.UpdateTransactionsAsync(context.UserId, updateRequests);
        }

        if (targetAccountId is null || transactions.Count == 0)
        {
            return transactions.Count;
        }

        var allTransactions = context.AllTransactions.ToList();
        var transactionIds = allTransactions.Select(transaction => transaction.ID).ToList();
        var links = await context
            .UserDataContext.TransactionLinks.Where(link =>
                transactionIds.Contains(link.SourceTransactionID)
                || transactionIds.Contains(link.TargetTransactionID)
            )
            .ToListAsync();
        var linkedIds = links
            .SelectMany(link => new[] { link.SourceTransactionID, link.TargetTransactionID })
            .ToHashSet();

        foreach (var transaction in allTransactions)
        {
            if (
                transaction.SourceTransactionLink is not null
                || transaction.TargetTransactionLink is not null
            )
            {
                linkedIds.Add(transaction.ID);
            }
        }

        foreach (var transaction in transactions)
        {
            if (linkedIds.Contains(transaction.ID) || transaction.AccountID == targetAccountId)
            {
                continue;
            }

            var candidates = allTransactions
                .Where(candidate =>
                    candidate.ID != transaction.ID
                    && candidate.Deleted == null
                    && candidate.Account?.Deleted == null
                    && candidate.AccountID == targetAccountId
                    && !linkedIds.Contains(candidate.ID)
                    && AreOppositeAmounts(transaction.Amount, candidate.Amount)
                    && Math.Abs(candidate.Date.DayNumber - transaction.Date.DayNumber)
                        <= TransferDateWindowDays
                )
                .ToList();
            if (candidates.Count != 1)
            {
                continue;
            }

            try
            {
                await transactionService.LinkTransactionsAsync(
                    context.UserId,
                    new TransactionLinkRequest
                    {
                        TransactionID = transaction.ID,
                        LinkedTransactionID = candidates[0].ID,
                    }
                );
                linkedIds.Add(transaction.ID);
                linkedIds.Add(candidates[0].ID);
            }
            catch (BudgetBoardServiceException)
            {
                linkedIds.Add(transaction.ID);
            }
        }

        return transactions.Count;
    }

    internal static async Task<int> LinkRecurringRuleAsync(
        IRuleParameterRequest action,
        IList<Transaction> transactions,
        AutomaticRuleActionContext context,
        IStringLocalizer<ResponseStrings> responseLocalizer
    )
    {
        if (!Guid.TryParse(action.Value, out var ruleId))
        {
            throw new BudgetBoardServiceException(
                responseLocalizer["AutomaticRuleInvalidRecurringRuleError", action.Value]
            );
        }

        var rule = await context.UserDataContext.RecurringRules.FirstOrDefaultAsync(candidate =>
            candidate.ID == ruleId && candidate.UserID == context.UserId
        );
        if (rule is null || rule.SuggestionStatus != RecurringRuleSuggestionStatus.Accepted)
        {
            throw new BudgetBoardServiceException(
                responseLocalizer["AutomaticRuleRecurringRuleNotFoundError"]
            );
        }

        var updated = 0;
        foreach (var transaction in transactions)
        {
            if (transaction.AccountID != rule.AccountID)
            {
                continue;
            }

            if (
                transaction.RecurringRuleID is Guid assignedRuleId
                && assignedRuleId != rule.ID
            )
            {
                continue;
            }

            if (transaction.RecurringRuleID != rule.ID)
            {
                transaction.RecurringRuleID = rule.ID;
                transaction.RecurringRule = rule;
            }

            updated++;
        }

        if (updated > 0)
        {
            await context.UserDataContext.SaveChangesAsync();
        }

        return updated;
    }

    private static bool AreOppositeAmounts(decimal firstAmount, decimal secondAmount)
    {
        return firstAmount != 0 && firstAmount == -secondAmount;
    }
}
