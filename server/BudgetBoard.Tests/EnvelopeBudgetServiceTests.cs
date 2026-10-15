using BudgetBoard.Database.Models;
using BudgetBoard.IntegrationTests.Fakers;
using BudgetBoard.Service;
using BudgetBoard.Service.Interfaces;
using BudgetBoard.Service.Models;
using BudgetBoard.Service.Resources;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace BudgetBoard.IntegrationTests;

[Collection("IntegrationTests")]
public class EnvelopeBudgetServiceTests
{
    private static readonly DateOnly October = new(2026, 10, 1);
    private static readonly DateOnly November = new(2026, 11, 1);

    [Fact]
    public async Task ReadMonthAsync_WhenNeverEnabled_ShouldStayOffAndLeaveBudgetsAlone()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);

        var month = await service.ReadMonthAsync(helper.demoUser.Id, October);

        month.Enabled.Should().BeFalse();
        month.ToBeAssigned.Should().Be(0);
        month.Groups.Should().NotBeEmpty();
        helper.UserDataContext.Budgets.Should().BeEmpty();
    }

    [Fact]
    public async Task SetEnabledAsync_ShouldToggleWithoutDeletingAssignments()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);
        var account = await AddAccountAsync(helper, "Checking", 1000m, October);

        await service.SetOnBudgetAccountsAsync(helper.demoUser.Id, [account.ID]);
        await service.SetEnabledAsync(helper.demoUser.Id, true);
        await service.SetAssignedAsync(
            helper.demoUser.Id,
            new EnvelopeAssignRequest
            {
                Month = October,
                Category = "Groceries",
                Assigned = 200m,
            }
        );

        await service.SetEnabledAsync(helper.demoUser.Id, false);
        var disabled = await service.ReadMonthAsync(helper.demoUser.Id, October);
        await service.SetEnabledAsync(helper.demoUser.Id, true);
        var enabled = await service.ReadMonthAsync(helper.demoUser.Id, October);

        disabled.Enabled.Should().BeFalse();
        disabled.ToBeAssigned.Should().Be(800m);
        enabled.Enabled.Should().BeTrue();
        enabled.ToBeAssigned.Should().Be(800m);
        Envelope(enabled, "Groceries").Available.Should().Be(200m);
    }

    [Fact]
    public async Task ReadMonthAsync_ShouldFundToBeAssignedFromOnBudgetBalancesOnly()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);
        var checking = await AddAccountAsync(helper, "Checking", 800m, October);
        var savings = await AddAccountAsync(helper, "Savings", 200m, new DateOnly(2026, 10, 20));
        await AddAccountAsync(helper, "Brokerage", 5000m, October);

        await service.SetOnBudgetAccountsAsync(helper.demoUser.Id, [checking.ID, savings.ID]);

        var month = await service.ReadMonthAsync(helper.demoUser.Id, new DateOnly(2026, 10, 15));

        month.OnBudgetBalance.Should().Be(1000m);
        month.ToBeAssigned.Should().Be(1000m);
        month.Accounts.Should().Contain(account => account.Name == "Brokerage" && !account.OnBudget);
        month.Accounts.Single(account => account.ID == savings.ID).Balance.Should().Be(200m);
    }

    [Fact]
    public async Task SetAssignedAsync_ShouldMoveMoneyOutOfToBeAssignedAndIgnoreOtherUsers()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);
        var account = await AddAccountAsync(helper, "Checking", 1000m, October);
        await service.SetOnBudgetAccountsAsync(helper.demoUser.Id, [account.ID]);
        await service.SetAssignedAsync(
            helper.demoUser.Id,
            new EnvelopeAssignRequest
            {
                Month = October,
                Category = "groceries",
                Assigned = 250m,
            }
        );

        var otherUser = new ApplicationUserFaker().Generate();
        helper.UserDataContext.Users.Add(otherUser);
        await helper.UserDataContext.SaveChangesAsync();
        var otherAccount = await AddAccountAsync(helper, "Other", 9000m, October, otherUser.Id);
        await service.SetOnBudgetAccountsAsync(otherUser.Id, [otherAccount.ID]);

        var month = await service.ReadMonthAsync(helper.demoUser.Id, October);
        var groceries = Envelope(month, "Groceries");

        month.ToBeAssigned.Should().Be(750m);
        groceries.Assigned.Should().Be(250m);
        groceries.Available.Should().Be(250m);
        helper.UserDataContext.Budgets.Should().BeEmpty();
        helper
            .UserDataContext.EnvelopeAssignments.Should()
            .ContainSingle(assignment => assignment.Category == "Groceries");
    }

    [Fact]
    public async Task ReadMonthAsync_WhenSpending_ShouldDrawDownEnvelopeAndKeepToBeAssigned()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);
        var account = await AddAccountAsync(helper, "Checking", 1000m, October);
        await service.SetOnBudgetAccountsAsync(helper.demoUser.Id, [account.ID]);
        await service.SetAssignedAsync(
            helper.demoUser.Id,
            new EnvelopeAssignRequest
            {
                Month = October,
                Category = "Groceries",
                Assigned = 200m,
            }
        );
        await AddTransactionAsync(
            helper,
            account,
            -50m,
            new DateOnly(2026, 10, 12),
            "Food & Dining",
            "Groceries"
        );

        var month = await service.ReadMonthAsync(helper.demoUser.Id, October);
        var groceries = Envelope(month, "Groceries");

        groceries.Activity.Should().Be(50m);
        groceries.Available.Should().Be(150m);
        month.OnBudgetBalance.Should().Be(950m);
        month.ToBeAssigned.Should().Be(800m);
    }

    [Fact]
    public async Task ReadMonthAsync_WhenIncome_ShouldIncreaseToBeAssigned()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);
        var account = await AddAccountAsync(helper, "Checking", 100m, new DateOnly(2026, 9, 30));
        await service.SetOnBudgetAccountsAsync(helper.demoUser.Id, [account.ID]);
        await AddTransactionAsync(
            helper,
            account,
            1000m,
            new DateOnly(2026, 10, 1),
            "Income",
            "Paycheck"
        );

        var month = await service.ReadMonthAsync(helper.demoUser.Id, October);

        month.Income.Should().Be(1000m);
        month.OnBudgetBalance.Should().Be(1100m);
        month.ToBeAssigned.Should().Be(1100m);
        month.Groups.SelectMany(group => group.Envelopes).Should().NotContain(envelope =>
            envelope.Category == "Paycheck"
        );
    }

    [Fact]
    public async Task ReadMonthAsync_ShouldRollLeftoverAndOverspendingIntoTheNextMonth()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);
        var account = await AddAccountAsync(helper, "Checking", 300m, October);
        await service.SetOnBudgetAccountsAsync(helper.demoUser.Id, [account.ID]);
        await service.SetAssignedAsync(
            helper.demoUser.Id,
            new EnvelopeAssignRequest
            {
                Month = October,
                Category = "Groceries",
                Assigned = 40m,
            }
        );
        await service.SetAssignedAsync(
            helper.demoUser.Id,
            new EnvelopeAssignRequest
            {
                Month = October,
                Category = "Restaurants",
                Assigned = 80m,
            }
        );
        await AddTransactionAsync(
            helper,
            account,
            -15m,
            new DateOnly(2026, 10, 4),
            "Food & Dining",
            "Groceries"
        );
        await AddTransactionAsync(
            helper,
            account,
            -100m,
            new DateOnly(2026, 10, 8),
            "Food & Dining",
            "Restaurants"
        );

        var next = await service.ReadMonthAsync(helper.demoUser.Id, November);
        var groceries = Envelope(next, "Groceries");
        var restaurants = Envelope(next, "Restaurants");

        groceries.Leftover.Should().Be(25m);
        groceries.Available.Should().Be(25m);
        restaurants.Leftover.Should().Be(-20m);
        restaurants.Available.Should().Be(-20m);
        next.OnBudgetBalance.Should().Be(185m);
        next.ToBeAssigned.Should().Be(180m);
    }

    [Fact]
    public async Task MoveAsync_ShouldTransferBetweenEnvelopesWithoutChangingToBeAssigned()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);
        var account = await AddAccountAsync(helper, "Checking", 500m, October);
        await service.SetOnBudgetAccountsAsync(helper.demoUser.Id, [account.ID]);
        await service.SetAssignedAsync(
            helper.demoUser.Id,
            new EnvelopeAssignRequest
            {
                Month = October,
                Category = "Groceries",
                Assigned = 100m,
            }
        );
        await service.SetAssignedAsync(
            helper.demoUser.Id,
            new EnvelopeAssignRequest
            {
                Month = October,
                Category = "Restaurants",
                Assigned = 20m,
            }
        );

        await service.MoveAsync(
            helper.demoUser.Id,
            new EnvelopeMoveRequest
            {
                Month = October,
                FromCategory = "Groceries",
                ToCategory = "Restaurants",
                Amount = 30m,
            }
        );

        var month = await service.ReadMonthAsync(helper.demoUser.Id, October);
        Envelope(month, "Groceries").Available.Should().Be(70m);
        Envelope(month, "Restaurants").Available.Should().Be(50m);
        month.ToBeAssigned.Should().Be(380m);
    }

    [Fact]
    public async Task MoveAsync_WhenAmountExceedsAvailableOrIsInvalid_ShouldReject()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);
        var account = await AddAccountAsync(helper, "Checking", 100m, October);
        await service.SetOnBudgetAccountsAsync(helper.demoUser.Id, [account.ID]);
        await service.SetAssignedAsync(
            helper.demoUser.Id,
            new EnvelopeAssignRequest
            {
                Month = October,
                Category = "Groceries",
                Assigned = 10m,
            }
        );

        var moveTooMuch = () =>
            service.MoveAsync(
                helper.demoUser.Id,
                new EnvelopeMoveRequest
                {
                    Month = October,
                    FromCategory = "Groceries",
                    ToCategory = "Restaurants",
                    Amount = 11m,
                }
            );
        var moveSame = () =>
            service.MoveAsync(
                helper.demoUser.Id,
                new EnvelopeMoveRequest
                {
                    Month = October,
                    FromCategory = "Groceries",
                    ToCategory = "Groceries",
                    Amount = 1m,
                }
            );
        var moveZero = () =>
            service.MoveAsync(
                helper.demoUser.Id,
                new EnvelopeMoveRequest
                {
                    Month = October,
                    FromCategory = "Groceries",
                    ToCategory = "Restaurants",
                    Amount = 0m,
                }
            );

        await moveTooMuch.Should().ThrowAsync<BudgetBoardServiceException>();
        await moveSame.Should().ThrowAsync<BudgetBoardServiceException>();
        await moveZero.Should().ThrowAsync<BudgetBoardServiceException>();
    }

    [Fact]
    public async Task MoveAsync_ShouldBeAbleToMoveLeftoverFromAPriorMonth()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);
        var account = await AddAccountAsync(helper, "Checking", 100m, October);
        await service.SetOnBudgetAccountsAsync(helper.demoUser.Id, [account.ID]);
        await service.SetAssignedAsync(
            helper.demoUser.Id,
            new EnvelopeAssignRequest
            {
                Month = October,
                Category = "Groceries",
                Assigned = 40m,
            }
        );

        await service.MoveAsync(
            helper.demoUser.Id,
            new EnvelopeMoveRequest
            {
                Month = November,
                FromCategory = "Groceries",
                ToCategory = "Restaurants",
                Amount = 15m,
            }
        );

        var month = await service.ReadMonthAsync(helper.demoUser.Id, November);
        Envelope(month, "Groceries").Leftover.Should().Be(40m);
        Envelope(month, "Groceries").Assigned.Should().Be(-15m);
        Envelope(month, "Groceries").Available.Should().Be(25m);
        Envelope(month, "Restaurants").Available.Should().Be(15m);
        month.ToBeAssigned.Should().Be(60m);
    }

    [Fact]
    public async Task SetAssignedAsync_WhenCategoryIsAGroupOrIncome_ShouldReject()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);

        var assignGroup = () =>
            service.SetAssignedAsync(
                helper.demoUser.Id,
                new EnvelopeAssignRequest
                {
                    Month = October,
                    Category = "Food & Dining",
                    Assigned = 10m,
                }
            );
        var assignIncome = () =>
            service.SetAssignedAsync(
                helper.demoUser.Id,
                new EnvelopeAssignRequest
                {
                    Month = October,
                    Category = "Paycheck",
                    Assigned = 10m,
                }
            );

        await assignGroup.Should().ThrowAsync<BudgetBoardServiceException>();
        await assignIncome.Should().ThrowAsync<BudgetBoardServiceException>();
    }

    [Fact]
    public async Task ReadMonthAsync_ShouldIgnoreOnBudgetTransfersAndCountOffBudgetTransfers()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);
        var checking = await AddAccountAsync(helper, "Checking", 400m, October);
        var savings = await AddAccountAsync(helper, "Savings", 50m, October);
        await service.SetOnBudgetAccountsAsync(helper.demoUser.Id, [checking.ID, savings.ID]);
        await AddTransactionAsync(
            helper,
            checking,
            -80m,
            new DateOnly(2026, 10, 3),
            "Transfer",
            "Savings"
        );
        await AddTransactionAsync(
            helper,
            savings,
            80m,
            new DateOnly(2026, 10, 3),
            "Transfer",
            "Savings"
        );

        var brokerage = await AddAccountAsync(helper, "Brokerage", 0m, October);
        await AddTransactionAsync(
            helper,
            checking,
            -25m,
            new DateOnly(2026, 10, 4),
            "Transfer",
            "Investment"
        );

        var month = await service.ReadMonthAsync(helper.demoUser.Id, October);

        month.OnBudgetBalance.Should().Be(425m);
        month.ToBeAssigned.Should().Be(425m);
        month.OutsideActivity.Should().Be(25m);
        month.Income.Should().Be(0);
        brokerage.Should().NotBeNull();
    }

    [Fact]
    public async Task ReadMonthAsync_ShouldKeepLaterBalancesAndAssignmentsOutOfAnEarlierMonth()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);
        var account = await AddAccountAsync(helper, "Checking", 0m, new DateOnly(2026, 9, 1));
        helper.UserDataContext.Balances.Add(
            new Balance
            {
                Amount = 700m,
                Date = new DateOnly(2026, 11, 2),
                AccountID = account.ID,
            }
        );
        await helper.UserDataContext.SaveChangesAsync();
        await service.SetOnBudgetAccountsAsync(helper.demoUser.Id, [account.ID]);
        await service.SetAssignedAsync(
            helper.demoUser.Id,
            new EnvelopeAssignRequest
            {
                Month = November,
                Category = "Groceries",
                Assigned = 50m,
            }
        );

        var october = await service.ReadMonthAsync(helper.demoUser.Id, October);
        var november = await service.ReadMonthAsync(helper.demoUser.Id, November);

        october.OnBudgetBalance.Should().Be(0);
        october.ToBeAssigned.Should().Be(0);
        Envelope(october, "Groceries").Assigned.Should().Be(0);
        november.OnBudgetBalance.Should().Be(700m);
        november.ToBeAssigned.Should().Be(650m);
    }

    [Fact]
    public async Task SetOnBudgetAccountsAsync_WhenAccountIsMissing_ShouldReject()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);

        var act = () => service.SetOnBudgetAccountsAsync(helper.demoUser.Id, [Guid.NewGuid()]);

        await act.Should().ThrowAsync<BudgetBoardServiceException>();
    }

    [Fact]
    public async Task ReadMonthAsync_WhenSpendingIsRefunded_ShouldReturnTheMoneyToTheEnvelope()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);
        var account = await AddAccountAsync(helper, "Checking", 100m, October);
        await service.SetOnBudgetAccountsAsync(helper.demoUser.Id, [account.ID]);
        await service.SetAssignedAsync(
            helper.demoUser.Id,
            new EnvelopeAssignRequest
            {
                Month = October,
                Category = "Groceries",
                Assigned = 40m,
            }
        );
        await AddTransactionAsync(
            helper,
            account,
            -30m,
            new DateOnly(2026, 10, 2),
            "Food & Dining",
            "Groceries"
        );
        await AddTransactionAsync(
            helper,
            account,
            10m,
            new DateOnly(2026, 10, 9),
            "Food & Dining",
            "Groceries"
        );

        var month = await service.ReadMonthAsync(helper.demoUser.Id, October);
        var groceries = Envelope(month, "Groceries");

        groceries.Activity.Should().Be(20m);
        groceries.Available.Should().Be(20m);
        month.ToBeAssigned.Should().Be(60m);
        month.Income.Should().Be(0);
    }

    private static EnvelopeBudgetService CreateService(TestHelper helper) =>
        new(
            Mock.Of<ILogger<IEnvelopeBudgetService>>(),
            helper.UserDataContext,
            TestHelper.CreateMockLocalizer<ResponseStrings>(),
            TestHelper.CreateMockLocalizer<LogStrings>()
        );

    private static async Task<Account> AddAccountAsync(
        TestHelper helper,
        string name,
        decimal balance,
        DateOnly balanceDate,
        Guid? userId = null
    )
    {
        var ownerId = userId ?? helper.demoUser.Id;
        var institution = new Institution { Name = $"{name} Bank", UserID = ownerId };
        var account = new Account
        {
            Name = name,
            InstitutionID = institution.ID,
            UserID = ownerId,
            Source = AccountSource.Manual,
            Type = "Checking",
        };
        helper.UserDataContext.Institutions.Add(institution);
        helper.UserDataContext.Accounts.Add(account);
        helper.UserDataContext.Balances.Add(
            new Balance
            {
                Amount = balance,
                Date = balanceDate,
                AccountID = account.ID,
            }
        );
        await helper.UserDataContext.SaveChangesAsync();
        return account;
    }

    /// <summary>
    /// Records a transaction and applies it to the account balance, matching TransactionService.
    /// </summary>
    private static async Task AddTransactionAsync(
        TestHelper helper,
        Account account,
        decimal amount,
        DateOnly date,
        string category,
        string subcategory
    )
    {
        helper.UserDataContext.Transactions.Add(
            new Transaction
            {
                Amount = amount,
                Date = date,
                Category = category,
                Subcategory = subcategory,
                Source = TransactionSource.Manual,
                AccountID = account.ID,
            }
        );

        var balances = helper
            .UserDataContext.Balances.Where(balance =>
                balance.AccountID == account.ID && balance.Date >= date
            )
            .ToList();
        if (balances.Count == 0)
        {
            var preceding = helper
                .UserDataContext.Balances.Where(balance =>
                    balance.AccountID == account.ID && balance.Date < date
                )
                .OrderByDescending(balance => balance.Date)
                .FirstOrDefault();
            helper.UserDataContext.Balances.Add(
                new Balance
                {
                    Amount = (preceding?.Amount ?? 0) + amount,
                    Date = date,
                    AccountID = account.ID,
                }
            );
        }
        else
        {
            foreach (var balance in balances)
            {
                balance.Amount += amount;
            }
        }

        await helper.UserDataContext.SaveChangesAsync();
    }

    private static IEnvelopeCategoryResponse Envelope(IEnvelopeMonthResponse month, string category) =>
        month
            .Groups.SelectMany(group => group.Envelopes)
            .Single(envelope => envelope.Category == category);
}
