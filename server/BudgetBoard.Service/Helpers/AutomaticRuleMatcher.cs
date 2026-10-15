using BudgetBoard.Database.Models;
using BudgetBoard.Service.Models;
using BudgetBoard.Service.Resources;
using Microsoft.Extensions.Localization;

namespace BudgetBoard.Service.Helpers;

internal static class AutomaticRuleMatcher
{
    internal static string NormalizeMatchMode(
        string? matchMode,
        IStringLocalizer<ResponseStrings> responseLocalizer
    )
    {
        if (
            string.IsNullOrWhiteSpace(matchMode)
            || matchMode.Equals(
                AutomaticRuleConstants.MatchModes.All,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return AutomaticRuleConstants.MatchModes.All;
        }

        if (
            matchMode.Equals(
                AutomaticRuleConstants.MatchModes.Any,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return AutomaticRuleConstants.MatchModes.Any;
        }

        throw new BudgetBoardServiceException(
            responseLocalizer["AutomaticRuleInvalidMatchModeError", matchMode]
        );
    }

    internal static List<Transaction> Match(
        IEnumerable<IRuleParameterRequest> conditions,
        string matchMode,
        IEnumerable<Transaction> transactions,
        IEnumerable<ITransactionCategory> allCategories,
        IStringLocalizer<ResponseStrings> responseLocalizer
    )
    {
        var eligible = transactions.Where(transaction =>
            transaction.Deleted == null && !transaction.Account!.HideTransactions
        );

        if (
            matchMode.Equals(
                AutomaticRuleConstants.MatchModes.Any,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            var matches = new Dictionary<Guid, Transaction>();
            foreach (var condition in conditions)
            {
                foreach (
                    var transaction in AutomaticRuleConditionHandler.FilterOnCondition(
                        condition,
                        eligible,
                        allCategories,
                        responseLocalizer
                    )
                )
                {
                    matches[transaction.ID] = transaction;
                }
            }

            return [.. matches.Values];
        }

        var matchedTransactions = eligible;
        foreach (var condition in conditions)
        {
            matchedTransactions = AutomaticRuleConditionHandler.FilterOnCondition(
                condition,
                matchedTransactions,
                allCategories,
                responseLocalizer
            );
        }

        return [.. matchedTransactions];
    }
}
