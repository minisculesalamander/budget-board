using BudgetBoard.Database.Models;
using BudgetBoard.Service.Interfaces;
using BudgetBoard.Service.Models;
using BudgetBoard.Service.Resources;
using Microsoft.Extensions.Localization;

namespace BudgetBoard.Service.Helpers;

internal static class AutomaticRuleActionHandler
{
    internal static async Task<int> ApplyActionToTransactions(
        IRuleParameterRequest action,
        IList<Transaction> transactions,
        IEnumerable<ITransactionCategory> allCategories,
        ITransactionService transactionService,
        Guid userGuid,
        IStringLocalizer<ResponseStrings> responseLocalizer,
        AutomaticRuleActionContext? actionContext = null
    )
    {
        if (
            action.Operator.Equals(
                AutomaticRuleConstants.ActionOperators.Delete,
                StringComparison.CurrentCultureIgnoreCase
            )
        )
        {
            await transactionService.DeleteTransactionsAsync(
                userGuid,
                transactions.Select(t => t.ID)
            );
            return transactions.Count;
        }
        if (IsAppendNote(action))
        {
            return await ApplyAppendNote(action, transactions, transactionService, userGuid);
        }
        if (
            action.Field.Equals(
                AutomaticRuleConstants.TransactionFields.Transfer,
                StringComparison.CurrentCultureIgnoreCase
            )
        )
        {
            return await AutomaticRuleLinkActions.MarkAsTransferAsync(
                action,
                transactions,
                allCategories,
                transactionService,
                RequireContext(actionContext),
                responseLocalizer
            );
        }
        if (
            action.Field.Equals(
                AutomaticRuleConstants.TransactionFields.RecurringRule,
                StringComparison.CurrentCultureIgnoreCase
            )
        )
        {
            return await AutomaticRuleLinkActions.LinkRecurringRuleAsync(
                action,
                transactions,
                RequireContext(actionContext),
                responseLocalizer
            );
        }
        if (
            action.Operator.Equals(
                AutomaticRuleConstants.ActionOperators.Set,
                StringComparison.CurrentCultureIgnoreCase
            )
        )
        {
            return await ApplySetAction(
                action,
                transactions,
                allCategories,
                transactionService,
                userGuid,
                responseLocalizer
            );
        }
        if (
            AutomaticRuleActionValidator.IsTagOperator(action.Operator)
            && action.Field.Equals(
                AutomaticRuleConstants.TransactionFields.Tags,
                StringComparison.CurrentCultureIgnoreCase
            )
        )
        {
            return await ApplyTagAction(
                action,
                transactions,
                transactionService,
                userGuid,
                responseLocalizer
            );
        }
        if (AutomaticRuleActionValidator.IsTagOperator(action.Operator))
        {
            throw new BudgetBoardServiceException(
                responseLocalizer[
                    "AutomaticRuleInvalidActionCombinationError",
                    action.Field,
                    action.Operator
                ]
            );
        }

        throw new BudgetBoardServiceException(
            responseLocalizer["AutomaticRuleUnsupportedOperatorError", action.Operator]
        );
    }

    private static async Task<int> ApplySetAction(
        IRuleParameterRequest action,
        IEnumerable<Transaction> transactions,
        IEnumerable<ITransactionCategory> allCategories,
        ITransactionService transactionService,
        Guid userGuid,
        IStringLocalizer<ResponseStrings> responseLocalizer
    )
    {
        if (
            action.Field.Equals(
                AutomaticRuleConstants.TransactionFields.Merchant,
                StringComparison.CurrentCultureIgnoreCase
            )
        )
        {
            return await ApplySetActionForMerchant(
                action,
                transactions,
                transactionService,
                userGuid
            );
        }
        else if (
            action.Field.Equals(
                AutomaticRuleConstants.TransactionFields.Category,
                StringComparison.CurrentCultureIgnoreCase
            )
        )
        {
            return await ApplySetActionForCategory(
                action,
                transactions,
                allCategories,
                transactionService,
                userGuid,
                responseLocalizer
            );
        }
        else if (AutomaticRuleActionValidator.IsNoteField(action.Field))
        {
            return await ApplySetActionForNote(action, transactions, transactionService, userGuid);
        }
        else if (
            action.Field.Equals(
                AutomaticRuleConstants.TransactionFields.Amount,
                StringComparison.CurrentCultureIgnoreCase
            )
        )
        {
            return await ApplySetActionForAmount(
                action,
                transactions,
                transactionService,
                userGuid,
                responseLocalizer
            );
        }
        else if (
            action.Field.Equals(
                AutomaticRuleConstants.TransactionFields.Date,
                StringComparison.CurrentCultureIgnoreCase
            )
        )
        {
            return await ApplySetActionForDate(
                action,
                transactions,
                transactionService,
                userGuid,
                responseLocalizer
            );
        }
        else
        {
            throw new BudgetBoardServiceException(
                responseLocalizer["AutomaticRuleUnsupportedActionFieldError", action.Field]
            );
        }
    }

    private static async Task<int> ApplySetActionForMerchant(
        IRuleParameterRequest action,
        IEnumerable<Transaction> transactions,
        ITransactionService transactionService,
        Guid userGuid
    )
    {
        var updateRequests = new List<ITransactionUpdateRequest>();
        foreach (var transaction in transactions)
        {
            updateRequests.Add(
                new TransactionUpdateRequest(transaction) { MerchantName = action.Value }
            );
        }

        if (updateRequests.Count > 0)
        {
            await transactionService.UpdateTransactionsAsync(userGuid, updateRequests);
        }

        return updateRequests.Count;
    }

    private static async Task<int> ApplySetActionForNote(
        IRuleParameterRequest action,
        IEnumerable<Transaction> transactions,
        ITransactionService transactionService,
        Guid userGuid
    )
    {
        var updateRequests = new List<ITransactionUpdateRequest>();
        foreach (var transaction in transactions)
        {
            updateRequests.Add(new TransactionUpdateRequest(transaction) { Notes = action.Value });
        }

        if (updateRequests.Count > 0)
        {
            await transactionService.UpdateTransactionsAsync(userGuid, updateRequests);
        }

        return updateRequests.Count;
    }

    private static bool IsAppendNote(IRuleParameterRequest action)
    {
        return AutomaticRuleActionValidator.IsNoteField(action.Field)
            && action.Operator.Equals(
                AutomaticRuleConstants.ActionOperators.Add,
                StringComparison.CurrentCultureIgnoreCase
            );
    }

    private static AutomaticRuleActionContext RequireContext(AutomaticRuleActionContext? actionContext)
    {
        return actionContext
            ?? throw new InvalidOperationException(
                "Automatic rule link actions require an action context."
            );
    }

    private static async Task<int> ApplyAppendNote(
        IRuleParameterRequest action,
        IEnumerable<Transaction> transactions,
        ITransactionService transactionService,
        Guid userGuid
    )
    {
        var updateRequests = new List<ITransactionUpdateRequest>();
        foreach (var transaction in transactions)
        {
            var notes = AppendNote(transaction.Notes, action.Value);
            if (notes == transaction.Notes)
            {
                continue;
            }

            updateRequests.Add(new TransactionUpdateRequest(transaction) { Notes = notes });
        }

        if (updateRequests.Count > 0)
        {
            await transactionService.UpdateTransactionsAsync(userGuid, updateRequests);
        }

        return updateRequests.Count;
    }

    private static string AppendNote(string existing, string addition)
    {
        var trimmed = addition.Trim();
        if (trimmed.Length == 0)
        {
            return existing ?? string.Empty;
        }

        var current = existing ?? string.Empty;
        if (
            current
                .Split('\n')
                .Any(line => line.Trim().Equals(trimmed, StringComparison.Ordinal))
        )
        {
            return current;
        }

        return current.Length == 0 ? trimmed : current + "\n" + trimmed;
    }

    private static async Task<int> ApplySetActionForCategory(
        IRuleParameterRequest action,
        IEnumerable<Transaction> transactions,
        IEnumerable<ITransactionCategory> allCategories,
        ITransactionService transactionService,
        Guid userGuid,
        IStringLocalizer<ResponseStrings> responseLocalizer
    )
    {
        string newCategory = string.Empty;

        if (!string.IsNullOrEmpty(action.Value))
        {
            var foundCategory = allCategories
                .FirstOrDefault(c =>
                    c.Value.Equals(action.Value, StringComparison.CurrentCultureIgnoreCase)
                )
                ?.Value;

            if (foundCategory != null)
            {
                newCategory = foundCategory;
            }
            else
            {
                throw new BudgetBoardServiceException(
                    responseLocalizer["AutomaticRuleCategoryNotFoundError", action.Value]
                );
            }
        }

        var updateRequests = new List<ITransactionUpdateRequest>();
        foreach (var transaction in transactions)
        {
            var updateRequest = new TransactionUpdateRequest(transaction);
            (updateRequest.Category, updateRequest.Subcategory) =
                TransactionCategoriesHelpers.GetFullCategory(newCategory, allCategories);
            updateRequests.Add(updateRequest);
        }

        if (updateRequests.Count > 0)
        {
            await transactionService.UpdateTransactionsAsync(userGuid, updateRequests);
        }

        return updateRequests.Count;
    }

    private static async Task<int> ApplySetActionForAmount(
        IRuleParameterRequest action,
        IEnumerable<Transaction> transactions,
        ITransactionService transactionService,
        Guid userGuid,
        IStringLocalizer<ResponseStrings> responseLocalizer
    )
    {
        var expression = AutomaticRuleActionValidator.ParseAmountExpression(
            action,
            responseLocalizer
        );

        var updateRequests = new List<ITransactionUpdateRequest>();
        foreach (var transaction in transactions)
        {
            decimal newAmount;
            try
            {
                newAmount = expression.Evaluate(transaction.Amount);
            }
            catch (DivideByZeroException)
            {
                throw new BudgetBoardServiceException(
                    responseLocalizer["AutomaticRuleDivisionByZeroError"]
                );
            }
            catch (OverflowException)
            {
                throw new BudgetBoardServiceException(
                    responseLocalizer["AutomaticRuleArithmeticOverflowError"]
                );
            }

            updateRequests.Add(new TransactionUpdateRequest(transaction) { Amount = newAmount });
        }

        if (updateRequests.Count > 0)
        {
            await transactionService.UpdateTransactionsAsync(userGuid, updateRequests);
        }

        return updateRequests.Count;
    }

    private static async Task<int> ApplyTagAction(
        IRuleParameterRequest action,
        IEnumerable<Transaction> transactions,
        ITransactionService transactionService,
        Guid userGuid,
        IStringLocalizer<ResponseStrings> responseLocalizer
    )
    {
        var tags = AutomaticRuleActionValidator.ParseTags(action, responseLocalizer);
        var updateRequests = new List<ITransactionUpdateRequest>();
        foreach (var transaction in transactions)
        {
            var updateRequest = new TransactionUpdateRequest(transaction);
            if (
                action.Operator.Equals(
                    AutomaticRuleConstants.ActionOperators.Add,
                    StringComparison.CurrentCultureIgnoreCase
                )
            )
            {
                updateRequest.AddTags = tags;
            }
            else
            {
                updateRequest.RemoveTags = tags;
            }

            updateRequests.Add(updateRequest);
        }

        if (updateRequests.Count > 0)
        {
            await transactionService.UpdateTransactionsAsync(userGuid, updateRequests);
        }

        return updateRequests.Count;
    }

    private static async Task<int> ApplySetActionForDate(
        IRuleParameterRequest action,
        IEnumerable<Transaction> transactions,
        ITransactionService transactionService,
        Guid userGuid,
        IStringLocalizer<ResponseStrings> responseLocalizer
    )
    {
        if (!DateOnly.TryParse(action.Value, out var newDate))
        {
            throw new BudgetBoardServiceException(
                responseLocalizer["AutomaticRuleInvalidDateError", action.Value]
            );
        }

        var updateRequests = new List<ITransactionUpdateRequest>();
        foreach (var transaction in transactions)
        {
            updateRequests.Add(new TransactionUpdateRequest(transaction) { Date = newDate });
        }

        if (updateRequests.Count > 0)
        {
            await transactionService.UpdateTransactionsAsync(userGuid, updateRequests);
        }

        return updateRequests.Count;
    }
}
