using BudgetBoard.Database.Models;
using BudgetBoard.Service.Helpers;
using BudgetBoard.Service.Interfaces;
using BudgetBoard.Service.Models;
using BudgetBoard.Service.Resources;
using FluentAssertions;

namespace BudgetBoard.IntegrationTests;

public class AutomaticRuleActionValidatorTests
{
    [Theory]
    [InlineData("tags", "set")]
    [InlineData("amount", "add")]
    [InlineData("merchant", "add")]
    public void Validate_InvalidActionCombination_ThrowsError(string field, string actionOperator)
    {
        var action = new RuleParameterCreateRequest
        {
            Field = field,
            Operator = actionOperator,
            Value = "[]",
        };
        var act = () =>
            AutomaticRuleActionValidator.Validate(
                [action],
                TestHelper.CreateMockLocalizer<ResponseStrings>()
            );

        act.Should()
            .Throw<BudgetBoardServiceException>()
            .WithMessage("AutomaticRuleInvalidActionCombinationError*");
    }

    [Theory]
    [InlineData("{}", "AutomaticRuleInvalidTagsError")]
    [InlineData("[1]", "AutomaticRuleInvalidTagsError")]
    [InlineData("[\" \" ]", "TagValueEmptyError")]
    [InlineData("[]", "AutomaticRuleInvalidTagsError")]
    public void Validate_InvalidTagPayload_ThrowsExpectedError(string value, string expectedError)
    {
        var action = new RuleParameterCreateRequest
        {
            Field = AutomaticRuleConstants.TransactionFields.Tags,
            Operator = AutomaticRuleConstants.ActionOperators.Add,
            Value = value,
        };
        var act = () =>
            AutomaticRuleActionValidator.Validate(
                [action],
                TestHelper.CreateMockLocalizer<ResponseStrings>()
            );

        act.Should().Throw<BudgetBoardServiceException>().WithMessage(expectedError + "*");
    }

    [Fact]
    public void Validate_NoteAdd_AcceptsTextAndRejectsEmptyText()
    {
        var valid = () =>
            AutomaticRuleActionValidator.Validate(
                [
                    new RuleParameterCreateRequest
                    {
                        Field = AutomaticRuleConstants.TransactionFields.Description,
                        Operator = AutomaticRuleConstants.ActionOperators.Add,
                        Value = "reimbursable",
                    },
                ],
                TestHelper.CreateMockLocalizer<ResponseStrings>()
            );
        valid.Should().NotThrow();

        var empty = () =>
            AutomaticRuleActionValidator.Validate(
                [
                    new RuleParameterCreateRequest
                    {
                        Field = AutomaticRuleConstants.TransactionFields.Note,
                        Operator = AutomaticRuleConstants.ActionOperators.Add,
                        Value = " ",
                    },
                ],
                TestHelper.CreateMockLocalizer<ResponseStrings>()
            );
        empty
            .Should()
            .Throw<BudgetBoardServiceException>()
            .WithMessage("AutomaticRuleEmptyNoteError*");
    }

    [Fact]
    public void Validate_TagPayloadTooLong_ThrowsError()
    {
        var action = new RuleParameterCreateRequest
        {
            Field = AutomaticRuleConstants.TransactionFields.Tags,
            Operator = AutomaticRuleConstants.ActionOperators.Add,
            Value = $"[\"{new string('x', Tag.MaxValueLength + 1)}\"]",
        };
        var act = () =>
            AutomaticRuleActionValidator.Validate(
                [action],
                TestHelper.CreateMockLocalizer<ResponseStrings>()
            );

        act.Should().Throw<BudgetBoardServiceException>().WithMessage("TagValueTooLongError*");
    }

    [Fact]
    public async Task Handler_TagOperatorOnNonTagField_ThrowsInvalidCombination()
    {
        var action = new RuleParameterCreateRequest
        {
            Field = AutomaticRuleConstants.TransactionFields.Merchant,
            Operator = AutomaticRuleConstants.ActionOperators.Add,
            Value = "[]",
        };
        var act = () =>
            AutomaticRuleActionHandler.ApplyActionToTransactions(
                action,
                [],
                [],
                Moq.Mock.Of<ITransactionService>(),
                Guid.NewGuid(),
                TestHelper.CreateMockLocalizer<ResponseStrings>()
            );

        await act.Should()
            .ThrowAsync<BudgetBoardServiceException>()
            .WithMessage("AutomaticRuleInvalidActionCombinationError*");
    }
}
