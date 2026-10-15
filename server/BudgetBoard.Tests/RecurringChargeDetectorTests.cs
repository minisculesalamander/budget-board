using BudgetBoard.Database.Models;
using BudgetBoard.Service.Helpers;
using BudgetBoard.Service.Models;
using FluentAssertions;

namespace BudgetBoard.IntegrationTests;

public class RecurringChargeDetectorTests
{
    private static readonly Guid AccountId = Guid.NewGuid();

    [Fact]
    public void Detect_ShouldGroupSimilarMerchantNamesOntoAMonthlyCadence()
    {
        var transactions = new[]
        {
            Charge(new DateOnly(2026, 1, 15), -15.99m, "NETFLIX.COM"),
            Charge(new DateOnly(2026, 2, 15), -15.99m, "Netflix"),
            Charge(new DateOnly(2026, 3, 15), -16.49m, "NETFLIX *1234"),
        };

        var candidates = RecurringChargeDetector.Detect(transactions, []);

        var candidate = candidates.Should().ContainSingle().Subject;
        candidate.Cadence.Unit.Should().Be(RecurringCadenceUnitValues.Month);
        candidate.Cadence.Interval.Should().Be(1);
        candidate.Amount.Should().Be(-15.99m);
        candidate.StartDate.Should().Be(new DateOnly(2026, 1, 15));
        candidate.MerchantName.Should().Be("NETFLIX *1234");
        candidate.Transactions.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(7, "Week", 1)]
    [InlineData(14, "Week", 2)]
    [InlineData(91, "Month", 3)]
    [InlineData(365, "Year", 1)]
    public void Detect_ShouldRecognizeRegularCadences(int gapDays, string unit, int interval)
    {
        var occurrenceCount = unit == RecurringCadenceUnitValues.Year ? 2 : 3;
        var transactions = Enumerable
            .Range(0, occurrenceCount)
            .Select(index =>
                Charge(new DateOnly(2024, 1, 15).AddDays(gapDays * index), -9.99m, "Membership")
            )
            .ToList();

        var candidate = RecurringChargeDetector.Detect(transactions, []).Should().ContainSingle().Subject;

        candidate.Cadence.Unit.Should().Be(unit);
        candidate.Cadence.Interval.Should().Be(interval);
    }

    [Fact]
    public void Detect_ShouldIgnoreIrregularChargesIncomeTransfersAndDeletedTransactions()
    {
        var transactions = new List<Transaction>
        {
            Charge(new DateOnly(2026, 1, 1), -8m, "Coffee Shop"),
            Charge(new DateOnly(2026, 1, 4), -12m, "Coffee Shop"),
            Charge(new DateOnly(2026, 2, 20), -6m, "Coffee Shop"),
            Charge(new DateOnly(2026, 5, 2), -15m, "Coffee Shop"),
            Charge(new DateOnly(2026, 1, 1), 2000m, "Payroll"),
            Charge(new DateOnly(2026, 2, 1), 2000m, "Payroll"),
            Charge(new DateOnly(2026, 3, 1), 2000m, "Payroll"),
            Charge(
                new DateOnly(2026, 1, 3),
                -25m,
                "Savings",
                TransactionCategoriesConstants.TransferCategory
            ),
            Charge(
                new DateOnly(2026, 2, 3),
                -25m,
                "Savings",
                TransactionCategoriesConstants.TransferCategory
            ),
            Charge(
                new DateOnly(2026, 3, 3),
                -25m,
                "Savings",
                TransactionCategoriesConstants.TransferCategory
            ),
        };
        transactions.Add(Charge(new DateOnly(2026, 1, 8), -4m, "Gym"));
        transactions.Add(Charge(new DateOnly(2026, 2, 8), -4m, "Gym"));
        transactions.Add(Charge(new DateOnly(2026, 3, 8), -4m, "Gym"));
        foreach (var date in new[] { new DateOnly(2026, 1, 12), new DateOnly(2026, 2, 12), new DateOnly(2026, 3, 12) })
        {
            var deleted = Charge(date, -4m, "Cancelled Gym");
            deleted.Deleted = DateTime.UtcNow;
            transactions.Add(deleted);
        }

        foreach (var date in new[] { new DateOnly(2026, 1, 9), new DateOnly(2026, 2, 9), new DateOnly(2026, 3, 9) })
        {
            var linked = Charge(date, -30m, "Rent");
            linked.SourceTransactionLink = new TransactionLink
            {
                SourceTransactionID = linked.ID,
                TargetTransactionID = Guid.NewGuid(),
            };
            transactions.Add(linked);
        }

        RecurringChargeDetector.Detect(transactions, []).Should().ContainSingle().Which.MerchantName.Should().Be("Gym");
    }

    [Fact]
    public void Detect_ShouldSkipChargesAlreadyCoveredByARule()
    {
        var transactions = new[]
        {
            Charge(new DateOnly(2026, 1, 1), -20m, "Internet"),
            Charge(new DateOnly(2026, 2, 1), -20m, "Internet"),
            Charge(new DateOnly(2026, 3, 1), -20m, "Internet"),
        };

        var candidates = RecurringChargeDetector.Detect(
            transactions,
            [new RecurringChargeDetector.BlockingCharge(AccountId, "INTERNET.COM", -20m)]
        );

        candidates.Should().BeEmpty();
    }

    private static Transaction Charge(
        DateOnly date,
        decimal amount,
        string merchantName,
        string? category = "Bills"
    ) =>
        new()
        {
            Amount = amount,
            Date = date,
            MerchantName = merchantName,
            Category = category,
            Source = TransactionSource.Manual,
            AccountID = AccountId,
        };
}
