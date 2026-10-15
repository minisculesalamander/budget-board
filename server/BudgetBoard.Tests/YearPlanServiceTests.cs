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
public class YearPlanServiceTests
{
    [Fact]
    public async Task ReadPlanAsync_UsesBudgetLimitsWithoutDoubleCountingParents()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);
        await service.UpdateSettingsAsync(
            helper.demoUser.Id,
            new PlanningSettingsUpdateRequest { FinancialYearStartMonth = 4 }
        );

        AddBudget(helper, "Auto & Transport", 300, new DateOnly(2026, 4, 1));
        AddBudget(helper, "Gas & Fuel", 80, new DateOnly(2026, 4, 1));
        AddBudget(helper, "Parking", 20, new DateOnly(2026, 5, 1));
        AddBudget(helper, "Income", 1000, new DateOnly(2026, 4, 1));
        await helper.UserDataContext.SaveChangesAsync();

        var plan = await service.ReadPlanAsync(helper.demoUser.Id, 2026, 1);

        plan.FinancialYearStartMonth.Should().Be(4);
        plan.Months.Should().HaveCount(12);
        plan.Months[0].Should().Be(new DateOnly(2026, 4, 1));
        plan.Months[^1].Should().Be(new DateOnly(2027, 3, 1));

        var auto = Parent(plan, "expense", "Auto & Transport");
        Cell(auto, new DateOnly(2026, 4, 1)).Planned.Should().Be(300);
        Cell(auto, new DateOnly(2026, 5, 1)).Planned.Should().Be(20);
        Cell(auto, new DateOnly(2026, 6, 1)).Planned.Should().BeNull();

        var year = plan.YearTotals.Should().ContainSingle().Subject;
        year.FinancialYear.Should().Be(2026);
        year.PlannedIncome.Should().Be(1000);
        year.PlannedExpense.Should().Be(320);
        year.PlannedSavings.Should().Be(680);
    }

    [Fact]
    public async Task ReadPlanAsync_ActualsCountEachTransactionOnce()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);
        var account = new AccountFaker(helper.demoUser.Id).Generate();
        var hidden = new AccountFaker(helper.demoUser.Id).Generate();
        hidden.HideTransactions = true;
        var otherUser = new ApplicationUserFaker().Generate();
        var otherAccount = new AccountFaker(otherUser.Id).Generate();
        helper.UserDataContext.Users.Add(otherUser);
        helper.UserDataContext.Accounts.AddRange(account, hidden, otherAccount);
        helper.UserDataContext.Transactions.AddRange(
            Transaction(account.ID, -40, new DateOnly(2026, 4, 3), "Auto & Transport", "Gas & Fuel"),
            Transaction(account.ID, 500, new DateOnly(2026, 4, 1), "Paycheck", null),
            Transaction(account.ID, -15, new DateOnly(2026, 4, 8), "Bills & Utilities", null),
            Transaction(account.ID, -99, new DateOnly(2026, 4, 9), "Hide from Budgets", null),
            Transaction(account.ID, -70, new DateOnly(2026, 4, 10), "Transfer", null),
            Transaction(
                account.ID,
                -25,
                new DateOnly(2026, 4, 11),
                "Auto & Transport",
                "Parking",
                deleted: true
            ),
            Transaction(hidden.ID, -80, new DateOnly(2026, 4, 12), "Gas & Fuel", null),
            Transaction(otherAccount.ID, -80, new DateOnly(2026, 4, 12), "Gas & Fuel", null)
        );
        await helper.UserDataContext.SaveChangesAsync();

        var plan = await service.ReadPlanAsync(helper.demoUser.Id, 2026, 1);
        var april = new DateOnly(2026, 4, 1);

        Cell(Parent(plan, "expense", "Auto & Transport"), april).Actual.Should().Be(40);
        Cell(Child(plan, "expense", "Auto & Transport", "Gas & Fuel"), april).Actual.Should().Be(40);
        Cell(Parent(plan, "expense", "Bills & Utilities"), april).Actual.Should().Be(15);
        Cell(Parent(plan, "income", "Income"), april).Actual.Should().Be(500);
        plan.Sections.SelectMany(section => section.Parents).Should().NotContain(parent =>
            parent.Category == "Transfer" || parent.Category == "Hide from Budgets"
        );

        var month = plan.MonthTotals.Single(total => total.Month == april);
        month.ActualIncome.Should().Be(500);
        month.ActualExpense.Should().Be(55);
        month.ActualSavings.Should().Be(445);
    }

    [Fact]
    public async Task UpsertCellAsync_CreatesFromExpressionAndClearKeepsManualParentSlack()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);
        var april = new DateOnly(2026, 4, 15);

        await service.UpsertCellAsync(
            helper.demoUser.Id,
            new YearPlanCellUpdateRequest
            {
                Month = april,
                Category = "gas & fuel",
                Amount = "80+20",
            }
        );

        var gas = helper.UserDataContext.Budgets.Single(b => b.Category == "Gas & Fuel");
        gas.Limit.Should().Be(100);
        gas.Month.Should().Be(new DateOnly(2026, 4, 1));
        helper.UserDataContext.Budgets.Single(b => b.Category == "Auto & Transport").Limit.Should().Be(100);

        await service.UpsertCellAsync(
            helper.demoUser.Id,
            new YearPlanCellUpdateRequest
            {
                Month = april,
                Category = "Auto & Transport",
                Amount = "250",
            }
        );
        helper.UserDataContext.Budgets.Single(b => b.Category == "Auto & Transport").Limit.Should().Be(250);

        await service.UpsertCellAsync(
            helper.demoUser.Id,
            new YearPlanCellUpdateRequest
            {
                Month = april,
                Category = "Gas & Fuel",
                Amount = " ",
            }
        );

        helper.UserDataContext.Budgets.Should().NotContain(b => b.Category == "Gas & Fuel");
        helper.UserDataContext.Budgets.Single(b => b.Category == "Auto & Transport").Limit.Should().Be(250);
    }

    [Fact]
    public async Task UpsertCellAsync_ClearingChildShrinksParentThatMatchedTheChildren()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);
        var april = new DateOnly(2026, 4, 1);

        await service.UpsertCellAsync(
            helper.demoUser.Id,
            new YearPlanCellUpdateRequest
            {
                Month = april,
                Category = "Gas & Fuel",
                Amount = "60",
            }
        );
        await service.UpsertCellAsync(
            helper.demoUser.Id,
            new YearPlanCellUpdateRequest
            {
                Month = april,
                Category = "Parking",
                Amount = "40",
            }
        );

        await service.UpsertCellAsync(
            helper.demoUser.Id,
            new YearPlanCellUpdateRequest
            {
                Month = april,
                Category = "Gas & Fuel",
                Amount = null,
            }
        );

        helper.UserDataContext.Budgets.Should().NotContain(b => b.Category == "Gas & Fuel");
        helper.UserDataContext.Budgets.Single(b => b.Category == "Parking").Limit.Should().Be(40);
        helper.UserDataContext.Budgets.Single(b => b.Category == "Auto & Transport").Limit.Should().Be(40);
    }

    [Fact]
    public async Task UpsertCellAsync_ClearingParentLeavesChildren()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);
        var april = new DateOnly(2026, 4, 1);
        await service.UpsertCellAsync(
            helper.demoUser.Id,
            new YearPlanCellUpdateRequest
            {
                Month = april,
                Category = "Gas & Fuel",
                Amount = "50",
            }
        );

        await service.UpsertCellAsync(
            helper.demoUser.Id,
            new YearPlanCellUpdateRequest
            {
                Month = april,
                Category = "Auto & Transport",
                Amount = null,
            }
        );

        helper.UserDataContext.Budgets.Should().ContainSingle();
        helper.UserDataContext.Budgets.Single().Category.Should().Be("Gas & Fuel");

        var plan = await service.ReadPlanAsync(helper.demoUser.Id, 2026, 1);
        var cell = Cell(Parent(plan, "expense", "Auto & Transport"), april);
        cell.BudgetId.Should().BeNull();
        cell.Planned.Should().Be(50);
    }

    [Theory]
    [InlineData("-5")]
    [InlineData("amount+1")]
    [InlineData("10/0")]
    [InlineData("nope")]
    public async Task UpsertCellAsync_RejectsAmountsThatAreNotANonNegativeConstant(string amount)
    {
        var helper = new TestHelper();
        var service = CreateService(helper);

        var act = () =>
            service.UpsertCellAsync(
                helper.demoUser.Id,
                new YearPlanCellUpdateRequest
                {
                    Month = new DateOnly(2026, 4, 1),
                    Category = "Gas & Fuel",
                    Amount = amount,
                }
            );

        await act.Should().ThrowAsync<BudgetBoardServiceException>();
        helper.UserDataContext.Budgets.Should().BeEmpty();
    }

    [Fact]
    public async Task CopyYearAsync_FillsMissingMonthsWithoutOverwriting()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);
        AddBudget(helper, "Gas & Fuel", 40, new DateOnly(2026, 4, 1));
        AddBudget(helper, "Auto & Transport", 40, new DateOnly(2026, 4, 1));
        AddBudget(helper, "Parking", 9, new DateOnly(2027, 4, 1));
        await helper.UserDataContext.SaveChangesAsync();

        await service.CopyYearAsync(helper.demoUser.Id, 2026);
        await service.CopyYearAsync(helper.demoUser.Id, 2026);

        helper.UserDataContext.Budgets.Single(b =>
            b.Category == "Gas & Fuel" && b.Month == new DateOnly(2027, 4, 1)
        ).Limit.Should().Be(40);
        helper.UserDataContext.Budgets.Single(b =>
            b.Category == "Auto & Transport" && b.Month == new DateOnly(2027, 4, 1)
        ).Limit.Should().Be(40);
        helper.UserDataContext.Budgets.Single(b => b.Category == "Parking").Limit.Should().Be(9);
        helper.UserDataContext.Budgets.Should().HaveCount(5);
    }

    [Fact]
    public async Task UpdateSettingsAsync_RejectsAMonthOutsideTheCalendar()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);

        var act = () =>
            service.UpdateSettingsAsync(
                helper.demoUser.Id,
                new PlanningSettingsUpdateRequest { FinancialYearStartMonth = 13 }
            );

        await act.Should().ThrowAsync<BudgetBoardServiceException>();
        helper.UserDataContext.PlanningSettings.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadPlanAsync_DefaultsToAJanuaryFinancialYear()
    {
        var helper = new TestHelper();
        var service = CreateService(helper);

        var plan = await service.ReadPlanAsync(helper.demoUser.Id, 2026, 2);

        plan.FinancialYearStartMonth.Should().Be(1);
        plan.Months.Should().HaveCount(24);
        plan.Months[0].Should().Be(new DateOnly(2026, 1, 1));
        plan.YearTotals.Select(total => total.FinancialYear).Should().Equal(2026, 2027);
    }

    private static YearPlanService CreateService(TestHelper helper)
    {
        var budgetService = new BudgetService(
            Mock.Of<ILogger<IBudgetService>>(),
            helper.UserDataContext,
            TestHelper.CreateMockLocalizer<ResponseStrings>(),
            TestHelper.CreateMockLocalizer<LogStrings>()
        );

        return new YearPlanService(
            Mock.Of<ILogger<IYearPlanService>>(),
            helper.UserDataContext,
            budgetService,
            TestHelper.CreateMockLocalizer<ResponseStrings>(),
            TestHelper.CreateMockLocalizer<LogStrings>()
        );
    }

    private static void AddBudget(TestHelper helper, string category, decimal limit, DateOnly month)
    {
        helper.UserDataContext.Budgets.Add(
            new Budget
            {
                Category = category,
                Limit = limit,
                Month = month,
                UserID = helper.demoUser.Id,
            }
        );
    }

    private static Transaction Transaction(
        Guid accountId,
        decimal amount,
        DateOnly date,
        string category,
        string? subcategory,
        bool deleted = false
    ) =>
        new()
        {
            Amount = amount,
            Date = date,
            Category = category,
            Subcategory = subcategory,
            Source = "Manual",
            AccountID = accountId,
            Deleted = deleted ? DateTime.UtcNow : null,
        };

    private static YearPlanParentResponse Parent(IYearPlanResponse plan, string kind, string category) =>
        plan.Sections.Single(section => section.Kind == kind).Parents.Single(parent =>
            parent.Category == category
        );

    private static YearPlanChildResponse Child(
        IYearPlanResponse plan,
        string kind,
        string parent,
        string category
    ) => Parent(plan, kind, parent).Children.Single(child => child.Category == category);

    private static YearPlanCellResponse Cell(YearPlanParentResponse parent, DateOnly month) =>
        parent.Cells.Single(cell => cell.Month == month);

    private static YearPlanCellResponse Cell(YearPlanChildResponse child, DateOnly month) =>
        child.Cells.Single(cell => cell.Month == month);
}
