using BudgetBoard.Database.Models;
using BudgetBoard.IntegrationTests.Fakers;
using BudgetBoard.IntegrationTests.Helpers;
using BudgetBoard.Service;
using BudgetBoard.Service.Interfaces;
using BudgetBoard.Service.Models;
using FluentAssertions;
using Moq;

namespace BudgetBoard.IntegrationTests;

[Collection("IntegrationTests")]
public class AutomaticRuleEngineTests
{
    [Fact]
    public async Task CreateAutomaticRuleAsync_StoresMatchModePriorityAndStopProcessing()
    {
        var helper = new TestHelper();
        var service = AutomaticRuleTestHelpers.BuildService(helper);
        var request = Rule(
            [MerchantEquals("Coffee")],
            [MerchantSet("Cafe")],
            AutomaticRuleConstants.MatchModes.Any,
            priority: 4,
            stopProcessing: true
        );

        await service.CreateAutomaticRuleAsync(helper.demoUser.Id, request);

        var created = helper.UserDataContext.AutomaticRules.Single();
        created.MatchMode.Should().Be(AutomaticRuleConstants.MatchModes.Any);
        created.Priority.Should().Be(4);
        created.StopProcessing.Should().BeTrue();

        var read = await service.ReadAutomaticRulesAsync(helper.demoUser.Id);
        read.Should().ContainSingle();
        read[0].MatchMode.Should().Be(AutomaticRuleConstants.MatchModes.Any);
        read[0].Priority.Should().Be(4);
        read[0].StopProcessing.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAutomaticRuleAsync_WhenPriorityIsOmitted_AppendsAfterExistingRules()
    {
        var helper = new TestHelper();
        var service = AutomaticRuleTestHelpers.BuildService(helper);
        await service.CreateAutomaticRuleAsync(
            helper.demoUser.Id,
            Rule([MerchantEquals("A")], [MerchantSet("B")], priority: 2)
        );

        await service.CreateAutomaticRuleAsync(
            helper.demoUser.Id,
            Rule([MerchantEquals("C")], [MerchantSet("D")])
        );

        helper
            .UserDataContext.AutomaticRules.Select(rule => rule.Priority)
            .Should()
            .BeEquivalentTo([2, 3]);
    }

    [Fact]
    public async Task UpdateAutomaticRuleAsync_UpdatesOrderingWithoutReplacingConditions()
    {
        var helper = new TestHelper();
        var service = AutomaticRuleTestHelpers.BuildService(helper);
        await service.CreateAutomaticRuleAsync(
            helper.demoUser.Id,
            Rule([MerchantEquals("Coffee")], [MerchantSet("Cafe")])
        );
        var ruleId = helper.UserDataContext.AutomaticRules.Single().ID;

        await service.UpdateAutomaticRuleAsync(
            helper.demoUser.Id,
            new AutomaticRuleUpdateRequest
            {
                ID = ruleId,
                MatchMode = AutomaticRuleConstants.MatchModes.Any,
                Priority = 9,
                StopProcessing = true,
            }
        );

        var updated = helper.UserDataContext.AutomaticRules.Single();
        updated.MatchMode.Should().Be(AutomaticRuleConstants.MatchModes.Any);
        updated.Priority.Should().Be(9);
        updated.StopProcessing.Should().BeTrue();
        updated.Conditions.Should().ContainSingle();
    }

    [Fact]
    public async Task RunOneOffAutomaticRuleAsync_WhenMatchModeIsAny_MatchesEitherCondition()
    {
        var helper = new TestHelper();
        var updates = new List<IReadOnlyList<ITransactionUpdateRequest>>();
        var service = ServiceCapturingUpdates(helper, updates);
        var coffee = AddTransaction(helper, "Coffee", 1);
        var priced = AddTransaction(helper, "Tea", 12);
        AddTransaction(helper, "Tea", 3);

        await service.RunOneOffAutomaticRuleAsync(
            helper.demoUser.Id,
            Rule(
                [MerchantEquals("Coffee"), AmountEquals(12)],
                [MerchantSet("Matched")],
                AutomaticRuleConstants.MatchModes.Any
            )
        );

        UpdatedIds(updates).Should().BeEquivalentTo([coffee.ID, priced.ID]);
    }

    [Fact]
    public async Task RunOneOffAutomaticRuleAsync_WhenMatchModeIsAll_RequiresEveryCondition()
    {
        var helper = new TestHelper();
        var updates = new List<IReadOnlyList<ITransactionUpdateRequest>>();
        var service = ServiceCapturingUpdates(helper, updates);
        var both = AddTransaction(helper, "Coffee", 12);
        AddTransaction(helper, "Coffee", 1);

        await service.RunOneOffAutomaticRuleAsync(
            helper.demoUser.Id,
            Rule(
                [MerchantEquals("Coffee"), AmountEquals(12)],
                [MerchantSet("Matched")],
                AutomaticRuleConstants.MatchModes.All
            )
        );

        UpdatedIds(updates).Should().Equal(both.ID);
    }

    [Fact]
    public async Task RunOneOffAutomaticRuleAsync_DescriptionContainsAndRegex_MatchesNotes()
    {
        var helper = new TestHelper();
        var updates = new List<IReadOnlyList<ITransactionUpdateRequest>>();
        var service = ServiceCapturingUpdates(helper, updates);
        var invoice = AddTransaction(helper, "Bank", 20, "Invoice 42");
        AddTransaction(helper, "Bank", 20, "Payroll");

        await service.RunOneOffAutomaticRuleAsync(
            helper.demoUser.Id,
            Rule(
                [
                    new RuleParameterCreateRequest
                    {
                        Field = AutomaticRuleConstants.TransactionFields.Description,
                        Operator = AutomaticRuleConstants.ConditionalOperators.MatchesRegex,
                        Value = @"Invoice \d+",
                    },
                ],
                [MerchantSet("Invoiced")]
            )
        );

        UpdatedIds(updates).Should().Equal(invoice.ID);
    }

    [Fact]
    public async Task RunOneOffAutomaticRuleAsync_AmountBetween_IsInclusive()
    {
        var helper = new TestHelper();
        var updates = new List<IReadOnlyList<ITransactionUpdateRequest>>();
        var service = ServiceCapturingUpdates(helper, updates);
        var low = AddTransaction(helper, "Shop", 10);
        var high = AddTransaction(helper, "Shop", 20);
        AddTransaction(helper, "Shop", 9.99m);
        AddTransaction(helper, "Shop", 20.01m);

        await service.RunOneOffAutomaticRuleAsync(
            helper.demoUser.Id,
            Rule(
                [
                    new RuleParameterCreateRequest
                    {
                        Field = AutomaticRuleConstants.TransactionFields.Amount,
                        Operator = AutomaticRuleConstants.ConditionalOperators.Between,
                        Value = "10|20",
                    },
                ],
                [MerchantSet("In range")]
            )
        );

        UpdatedIds(updates).Should().BeEquivalentTo([low.ID, high.ID]);
    }

    [Fact]
    public async Task RunSavedAutomaticRulesAsync_RunsInPriorityOrderAndStopsProcessing()
    {
        var helper = new TestHelper();
        var updates = new List<string>();
        var mock = new Mock<ITransactionService>();
        mock.Setup(transactionService =>
                transactionService.UpdateTransactionsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IEnumerable<ITransactionUpdateRequest>>()
                )
            )
            .Callback<Guid, IEnumerable<ITransactionUpdateRequest>, bool>(
                (_, requests, _) => updates.Add(requests.Single().MerchantName.Value!)
            )
            .Returns(Task.CompletedTask);
        var service = AutomaticRuleTestHelpers.BuildService(helper, mock.Object);
        var stopped = AddTransaction(helper, "Coffee", 10);
        var continued = AddTransaction(helper, "Tea", 10);
        AddSavedRule(
            helper,
            [MerchantEquals("Coffee")],
            [MerchantSet("First")],
            priority: 1,
            stopProcessing: true
        );
        AddSavedRule(
            helper,
            [AmountEquals(10)],
            [MerchantSet("Second")],
            priority: 2
        );

        await service.RunSavedAutomaticRulesAsync(helper.demoUser.Id);

        updates.Should().Equal("First", "Second");
        mock.Verify(
            transactionService =>
                transactionService.UpdateTransactionsAsync(
                    helper.demoUser.Id,
                    It.Is<IEnumerable<ITransactionUpdateRequest>>(requests =>
                        requests.Any(request => request.ID == stopped.ID)
                        && requests.All(request => request.ID != continued.ID)
                    )
                ),
            Times.Once
        );
        mock.Verify(
            transactionService =>
                transactionService.UpdateTransactionsAsync(
                    helper.demoUser.Id,
                    It.Is<IEnumerable<ITransactionUpdateRequest>>(requests =>
                        requests.All(request => request.ID == continued.ID)
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task PreviewAutomaticRuleAsync_ReturnsMatchesWithoutApplyingActions()
    {
        var helper = new TestHelper();
        var mock = AutomaticRuleTestHelpers.UpdateMock();
        var service = AutomaticRuleTestHelpers.BuildService(helper, mock.Object);
        var match = AddTransaction(helper, "Coffee", 4, "latte");
        AddTransaction(helper, "Tea", 4);

        var preview = await service.PreviewAutomaticRuleAsync(
            helper.demoUser.Id,
            Rule([MerchantEquals("Coffee")], [])
        );

        preview.MatchCount.Should().Be(1);
        preview.Matches.Should().ContainSingle();
        preview.Matches[0].ID.Should().Be(match.ID);
        preview.Matches[0].Description.Should().Be("latte");
        mock.Verify(
            transactionService =>
                transactionService.UpdateTransactionsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IEnumerable<ITransactionUpdateRequest>>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task PreviewAutomaticRuleAsync_LimitsReturnedMatches()
    {
        var helper = new TestHelper();
        var service = AutomaticRuleTestHelpers.BuildService(helper);
        var account = new AccountFaker(helper.demoUser.Id).Generate();
        helper.UserDataContext.Accounts.Add(account);
        for (var index = 0; index < 26; index++)
        {
            var transaction = new TransactionFaker([account.ID]).Generate();
            transaction.MerchantName = "Coffee";
            transaction.Account = account;
            helper.UserDataContext.Transactions.Add(transaction);
        }
        helper.UserDataContext.SaveChanges();

        var preview = await service.PreviewAutomaticRuleAsync(
            helper.demoUser.Id,
            Rule([MerchantEquals("Coffee")], [])
        );

        preview.MatchCount.Should().Be(26);
        preview.Matches.Should().HaveCount(AutomaticRulePreviewResponse.MatchLimit);
    }

    [Fact]
    public async Task RunOneOffAutomaticRuleAsync_AddNote_AppendsWithoutDuplicating()
    {
        var helper = new TestHelper();
        var updates = new List<IReadOnlyList<ITransactionUpdateRequest>>();
        var service = ServiceCapturingUpdates(helper, updates);
        AddTransaction(helper, "Coffee", 4, "existing");

        var request = Rule(
            [MerchantEquals("Coffee")],
            [
                new RuleParameterCreateRequest
                {
                    Field = AutomaticRuleConstants.TransactionFields.Description,
                    Operator = AutomaticRuleConstants.ActionOperators.Add,
                    Value = "added",
                },
            ]
        );
        await service.RunOneOffAutomaticRuleAsync(helper.demoUser.Id, request);

        updates.Single().Single().Notes.Should().Be("existing\nadded");
    }

    [Fact]
    public async Task RunOneOffAutomaticRuleAsync_AddNote_SkipsWhenNoteAlreadyPresent()
    {
        var helper = new TestHelper();
        var mock = AutomaticRuleTestHelpers.UpdateMock();
        var service = AutomaticRuleTestHelpers.BuildService(helper, mock.Object);
        AddTransaction(helper, "Coffee", 4, "existing\nadded");

        await service.RunOneOffAutomaticRuleAsync(
            helper.demoUser.Id,
            Rule(
                [MerchantEquals("Coffee")],
                [
                    new RuleParameterCreateRequest
                    {
                        Field = AutomaticRuleConstants.TransactionFields.Note,
                        Operator = AutomaticRuleConstants.ActionOperators.Add,
                        Value = "added",
                    },
                ]
            )
        );

        mock.Verify(
            transactionService =>
                transactionService.UpdateTransactionsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IEnumerable<ITransactionUpdateRequest>>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task RunOneOffAutomaticRuleAsync_MarkAsTransfer_LinksSingleCounterpart()
    {
        var helper = new TestHelper();
        var mock = AutomaticRuleTestHelpers.UpdateMock();
        mock.Setup(transactionService =>
                transactionService.LinkTransactionsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<ITransactionLinkRequest>()
                )
            )
            .ReturnsAsync(Array.Empty<ITransactionResponse>());
        var service = AutomaticRuleTestHelpers.BuildService(helper, mock.Object);
        var sourceAccount = new AccountFaker(helper.demoUser.Id).Generate();
        var targetAccount = new AccountFaker(helper.demoUser.Id).Generate();
        var source = new TransactionFaker([sourceAccount.ID]).Generate();
        source.MerchantName = "Move";
        source.Amount = 40;
        source.Date = new DateOnly(2026, 8, 1);
        source.Account = sourceAccount;
        var target = new TransactionFaker([targetAccount.ID]).Generate();
        target.Amount = -40;
        target.Date = new DateOnly(2026, 8, 2);
        target.Account = targetAccount;
        helper.UserDataContext.Accounts.AddRange(sourceAccount, targetAccount);
        helper.UserDataContext.Transactions.AddRange(source, target);
        helper.UserDataContext.SaveChanges();

        await service.RunOneOffAutomaticRuleAsync(
            helper.demoUser.Id,
            Rule(
                [MerchantEquals("Move")],
                [
                    new RuleParameterCreateRequest
                    {
                        Field = AutomaticRuleConstants.TransactionFields.Transfer,
                        Operator = AutomaticRuleConstants.ActionOperators.Set,
                        Value = targetAccount.ID.ToString(),
                    },
                ]
            )
        );

        mock.Verify(
            transactionService =>
                transactionService.UpdateTransactionsAsync(
                    helper.demoUser.Id,
                    It.Is<IEnumerable<ITransactionUpdateRequest>>(requests =>
                        requests.Single().Category.Value == TransactionCategoriesConstants.TransferCategory
                    )
                ),
            Times.Once
        );
        mock.Verify(
            transactionService =>
                transactionService.LinkTransactionsAsync(
                    helper.demoUser.Id,
                    It.Is<ITransactionLinkRequest>(request =>
                        request.TransactionID == source.ID && request.LinkedTransactionID == target.ID
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task RunOneOffAutomaticRuleAsync_MarkAsTransfer_DoesNotLinkWhenCounterpartsAreAmbiguous()
    {
        var helper = new TestHelper();
        var mock = AutomaticRuleTestHelpers.UpdateMock();
        var service = AutomaticRuleTestHelpers.BuildService(helper, mock.Object);
        var sourceAccount = new AccountFaker(helper.demoUser.Id).Generate();
        var targetAccount = new AccountFaker(helper.demoUser.Id).Generate();
        var source = new TransactionFaker([sourceAccount.ID]).Generate();
        source.MerchantName = "Move";
        source.Amount = 40;
        source.Date = new DateOnly(2026, 8, 1);
        var first = new TransactionFaker([targetAccount.ID]).Generate();
        first.Amount = -40;
        first.Date = new DateOnly(2026, 8, 1);
        var second = new TransactionFaker([targetAccount.ID]).Generate();
        second.Amount = -40;
        second.Date = new DateOnly(2026, 8, 1);
        helper.UserDataContext.Accounts.AddRange(sourceAccount, targetAccount);
        helper.UserDataContext.Transactions.AddRange(source, first, second);
        helper.UserDataContext.SaveChanges();

        await service.RunOneOffAutomaticRuleAsync(
            helper.demoUser.Id,
            Rule(
                [MerchantEquals("Move")],
                [
                    new RuleParameterCreateRequest
                    {
                        Field = AutomaticRuleConstants.TransactionFields.Transfer,
                        Operator = AutomaticRuleConstants.ActionOperators.Set,
                        Value = targetAccount.ID.ToString(),
                    },
                ]
            )
        );

        mock.Verify(
            transactionService =>
                transactionService.LinkTransactionsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<ITransactionLinkRequest>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task RunOneOffAutomaticRuleAsync_LinkRecurringRule_AssignsMatchingAccountOnly()
    {
        var helper = new TestHelper();
        var service = AutomaticRuleTestHelpers.BuildService(helper);
        var account = new AccountFaker(helper.demoUser.Id).Generate();
        var otherAccount = new AccountFaker(helper.demoUser.Id).Generate();
        var match = new TransactionFaker([account.ID]).Generate();
        match.MerchantName = "Rent";
        var other = new TransactionFaker([otherAccount.ID]).Generate();
        other.MerchantName = "Rent";
        var rule = new RecurringRule
        {
            UserID = helper.demoUser.Id,
            AccountID = account.ID,
            StartDate = new DateOnly(2026, 1, 1),
            MerchantName = "Rent",
            SuggestionStatus = RecurringRuleSuggestionStatus.Accepted,
        };
        helper.UserDataContext.Accounts.AddRange(account, otherAccount);
        helper.UserDataContext.Transactions.AddRange(match, other);
        helper.UserDataContext.RecurringRules.Add(rule);
        helper.UserDataContext.SaveChanges();

        await service.RunOneOffAutomaticRuleAsync(
            helper.demoUser.Id,
            Rule(
                [MerchantEquals("Rent")],
                [
                    new RuleParameterCreateRequest
                    {
                        Field = AutomaticRuleConstants.TransactionFields.RecurringRule,
                        Operator = AutomaticRuleConstants.ActionOperators.Set,
                        Value = rule.ID.ToString(),
                    },
                ]
            )
        );

        helper.UserDataContext.Transactions.Single(transaction => transaction.ID == match.ID)
            .RecurringRuleID.Should()
            .Be(rule.ID);
        helper.UserDataContext.Transactions.Single(transaction => transaction.ID == other.ID)
            .RecurringRuleID.Should()
            .BeNull();
    }

    [Fact]
    public async Task ApplySavedRulesToTransactionsAsync_OnlyEvaluatesRequestedTransactions()
    {
        var helper = new TestHelper();
        var updates = new List<IReadOnlyList<ITransactionUpdateRequest>>();
        var service = ServiceCapturingUpdates(helper, updates);
        var included = AddTransaction(helper, "Coffee", 5);
        AddTransaction(helper, "Coffee", 5);
        AddSavedRule(helper, [MerchantEquals("Coffee")], [MerchantSet("Cafe")]);

        await service.ApplySavedRulesToTransactionsAsync(helper.demoUser.Id, [included.ID]);

        UpdatedIds(updates).Should().Equal(included.ID);
    }

    private static AutomaticRuleService ServiceCapturingUpdates(
        TestHelper helper,
        List<IReadOnlyList<ITransactionUpdateRequest>> updates
    )
    {
        var mock = new Mock<ITransactionService>();
        mock.Setup(transactionService =>
                transactionService.UpdateTransactionsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IEnumerable<ITransactionUpdateRequest>>()
                )
            )
            .Callback<Guid, IEnumerable<ITransactionUpdateRequest>, bool>(
                (_, requests, _) => updates.Add(requests.ToList())
            )
            .Returns(Task.CompletedTask);
        return AutomaticRuleTestHelpers.BuildService(helper, mock.Object);
    }

    private static HashSet<Guid> UpdatedIds(
        IEnumerable<IReadOnlyList<ITransactionUpdateRequest>> updates
    ) => [.. updates.SelectMany(batch => batch).Select(request => request.ID)];

    private static Transaction AddTransaction(
        TestHelper helper,
        string merchant,
        decimal amount,
        string notes = ""
    )
    {
        var account = new AccountFaker(helper.demoUser.Id).Generate();
        var transaction = new TransactionFaker([account.ID]).Generate();
        transaction.MerchantName = merchant;
        transaction.Amount = amount;
        transaction.Notes = notes;
        transaction.Date = new DateOnly(2026, 8, 1);
        transaction.Account = account;
        helper.UserDataContext.Accounts.Add(account);
        helper.UserDataContext.Transactions.Add(transaction);
        helper.UserDataContext.SaveChanges();
        return transaction;
    }

    private static void AddSavedRule(
        TestHelper helper,
        IEnumerable<RuleParameterCreateRequest> conditions,
        IEnumerable<RuleParameterCreateRequest> actions,
        int priority = 1,
        bool stopProcessing = false,
        string matchMode = AutomaticRuleConstants.MatchModes.All
    )
    {
        var ruleId = Guid.NewGuid();
        helper.UserDataContext.AutomaticRules.Add(
            new AutomaticRule
            {
                ID = ruleId,
                UserID = helper.demoUser.Id,
                Priority = priority,
                StopProcessing = stopProcessing,
                MatchMode = matchMode,
                Conditions =
                [
                    .. conditions.Select(condition => new RuleCondition
                    {
                        Field = condition.Field,
                        Operator = condition.Operator,
                        Value = condition.Value,
                        RuleID = ruleId,
                    }),
                ],
                Actions =
                [
                    .. actions.Select(action => new RuleAction
                    {
                        Field = action.Field,
                        Operator = action.Operator,
                        Value = action.Value,
                        RuleID = ruleId,
                    }),
                ],
            }
        );
        helper.UserDataContext.SaveChanges();
    }

    private static AutomaticRuleCreateRequest Rule(
        ICollection<RuleParameterCreateRequest> conditions,
        ICollection<RuleParameterCreateRequest> actions,
        string matchMode = AutomaticRuleConstants.MatchModes.All,
        int? priority = null,
        bool stopProcessing = false
    ) =>
        new()
        {
            Conditions = conditions,
            Actions = actions,
            MatchMode = matchMode,
            Priority = priority,
            StopProcessing = stopProcessing,
        };

    private static RuleParameterCreateRequest MerchantEquals(string value) =>
        new()
        {
            Field = AutomaticRuleConstants.TransactionFields.Merchant,
            Operator = AutomaticRuleConstants.ConditionalOperators.EqualsString,
            Value = value,
        };

    private static RuleParameterCreateRequest AmountEquals(decimal value) =>
        new()
        {
            Field = AutomaticRuleConstants.TransactionFields.Amount,
            Operator = AutomaticRuleConstants.ConditionalOperators.EqualsString,
            Value = value.ToString(),
        };

    private static RuleParameterCreateRequest MerchantSet(string value) =>
        new()
        {
            Field = AutomaticRuleConstants.TransactionFields.Merchant,
            Operator = AutomaticRuleConstants.ActionOperators.Set,
            Value = value,
        };
}
