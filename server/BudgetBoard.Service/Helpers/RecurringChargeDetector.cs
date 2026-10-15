using BudgetBoard.Database.Models;
using BudgetBoard.Service.Models;

namespace BudgetBoard.Service.Helpers;

/// <summary>
/// Finds recurring charges in existing transactions so they can be offered as suggested recurring rules.
/// </summary>
public static class RecurringChargeDetector
{
    public const decimal AmountTolerance = 0.2M;

    private static readonly HashSet<string> MerchantNoiseTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACH",
        "CHECKCARD",
        "COM",
        "DEBIT",
        "INC",
        "LLC",
        "POS",
        "WWW",
    };

    private readonly record struct CadenceTemplate(
        string Unit,
        int Interval,
        int ExpectedDays,
        int ToleranceDays,
        int MinimumOccurrences
    );

    private static readonly CadenceTemplate[] CadenceTemplates =
    [
        new(RecurringCadenceUnitValues.Week, 1, 7, 3, 3),
        new(RecurringCadenceUnitValues.Week, 2, 14, 3, 3),
        new(RecurringCadenceUnitValues.Month, 1, 30, 4, 3),
        new(RecurringCadenceUnitValues.Month, 3, 91, 7, 3),
        new(RecurringCadenceUnitValues.Year, 1, 365, 16, 2),
    ];

    public sealed record Candidate(
        Guid AccountID,
        string MerchantName,
        string? Category,
        string? Subcategory,
        decimal Amount,
        RecurringCadence Cadence,
        DateOnly StartDate,
        IReadOnlyList<Transaction> Transactions
    );

    public readonly record struct BlockingCharge(Guid AccountID, string? MerchantName, decimal Amount);

    public static string NormalizeMerchant(string? merchantName)
    {
        if (string.IsNullOrWhiteSpace(merchantName))
        {
            return string.Empty;
        }

        var tokens = new string(
            merchantName.ToUpperInvariant().Select(character => char.IsLetter(character) ? character : ' ').ToArray()
        )
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 1 && !MerchantNoiseTokens.Contains(token))
            .ToArray();

        return string.Join(' ', tokens);
    }

    public static bool AmountsMatch(decimal expected, decimal actual)
    {
        if (expected == 0)
        {
            return actual == 0;
        }

        return Math.Abs(actual - expected) <= Math.Abs(expected) * AmountTolerance;
    }

    public static IReadOnlyList<Candidate> Detect(
        IEnumerable<Transaction> transactions,
        IEnumerable<BlockingCharge> blockingCharges
    )
    {
        var blocked = blockingCharges.ToList();
        var candidates = new List<Candidate>();

        foreach (
            var group in transactions
                .Where(IsDetectableCharge)
                .GroupBy(transaction => (transaction.AccountID, Merchant: NormalizeMerchant(transaction.MerchantName)))
                .Where(group => group.Key.Merchant.Length > 1)
        )
        {
            var stable = SelectStableAmounts(group.ToList());
            if (stable.Count == 0 || !TryMatchCadence(stable, out var unit, out var interval))
            {
                continue;
            }

            var latest = stable.OrderByDescending(transaction => transaction.Date).First();
            var amount = Median(stable.Select(transaction => transaction.Amount).ToList());
            if (
                blocked.Any(charge =>
                    charge.AccountID == group.Key.AccountID
                    && NormalizeMerchant(charge.MerchantName) == group.Key.Merchant
                    && AmountsMatch(charge.Amount, amount)
                )
            )
            {
                continue;
            }

            candidates.Add(
                new Candidate(
                    group.Key.AccountID,
                    latest.MerchantName ?? group.Key.Merchant,
                    latest.Category,
                    latest.Subcategory,
                    amount,
                    new RecurringCadence
                    {
                        Version = 1,
                        Unit = unit,
                        Interval = interval,
                    },
                    stable.Min(transaction => transaction.Date),
                    stable
                )
            );
        }

        return candidates;
    }

    private static bool IsDetectableCharge(Transaction transaction)
    {
        if (
            transaction.Deleted.HasValue
            || transaction.Amount >= 0
            || string.IsNullOrWhiteSpace(transaction.MerchantName)
            || transaction.SourceTransactionLink is not null
            || transaction.TargetTransactionLink is not null
        )
        {
            return false;
        }

        return !string.Equals(
                transaction.Category,
                TransactionCategoriesConstants.TransferCategory,
                StringComparison.OrdinalIgnoreCase
            )
            && !string.Equals(
                transaction.Category,
                TransactionCategoriesConstants.HideFromBudgetsCategory,
                StringComparison.OrdinalIgnoreCase
            );
    }

    private static List<Transaction> SelectStableAmounts(List<Transaction> transactions)
    {
        var median = Median(transactions.Select(transaction => transaction.Amount).ToList());
        var stable = transactions.Where(transaction => AmountsMatch(median, transaction.Amount)).ToList();
        if (stable.Count * 5 < transactions.Count * 4)
        {
            return [];
        }

        return stable;
    }

    private static bool TryMatchCadence(
        IReadOnlyList<Transaction> transactions,
        out string unit,
        out int interval
    )
    {
        unit = RecurringCadenceUnitValues.Month;
        interval = 1;
        var dates = transactions.Select(transaction => transaction.Date).Distinct().OrderBy(date => date).ToList();
        if (dates.Count < 2)
        {
            return false;
        }

        var gaps = new int[dates.Count - 1];
        for (var index = 1; index < dates.Count; index++)
        {
            gaps[index - 1] = dates[index].DayNumber - dates[index - 1].DayNumber;
        }

        var match = CadenceTemplates
            .Where(template => dates.Count >= template.MinimumOccurrences)
            .Select(template => new
            {
                Template = template,
                Fits = gaps.All(gap => Math.Abs(gap - template.ExpectedDays) <= template.ToleranceDays),
                Deviation = gaps.Sum(gap => Math.Abs(gap - template.ExpectedDays)),
            })
            .Where(candidate => candidate.Fits)
            .OrderBy(candidate => candidate.Deviation)
            .FirstOrDefault();
        if (match is null)
        {
            return false;
        }

        unit = match.Template.Unit;
        interval = match.Template.Interval;
        return true;
    }

    private static decimal Median(IReadOnlyList<decimal> values)
    {
        var ordered = values.OrderBy(value => value).ToList();
        var middle = ordered.Count / 2;
        return ordered.Count % 2 == 0 ? (ordered[middle - 1] + ordered[middle]) / 2 : ordered[middle];
    }
}
