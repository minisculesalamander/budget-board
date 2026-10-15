using BudgetBoard.Database.Models;

namespace BudgetBoard.Service.Helpers;

public static class RecurringRuleAmounts
{
    public static decimal GetForecastAmount(RecurringRule rule)
    {
        if (rule.AmountMode == RecurringAmountMode.Fixed || rule.Transactions.Count < 2)
        {
            return rule.Amount;
        }

        var amounts = rule
            .Transactions.Where(transaction => transaction.Deleted is null)
            .Select(transaction => transaction.Amount)
            .OrderBy(amount => amount)
            .ToList();
        if (amounts.Count < 2)
        {
            return rule.Amount;
        }

        var middle = amounts.Count / 2;
        return amounts.Count % 2 == 0 ? (amounts[middle - 1] + amounts[middle]) / 2 : amounts[middle];
    }
}
