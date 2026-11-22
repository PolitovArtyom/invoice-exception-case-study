using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules;

namespace InvoiceDesk.Tests.Rules;

public sealed class InvoiceDecisionTests
{
    [Fact]
    public void Should_follow_core_v1_priority_when_checks_have_mixed_statuses()
    {
        var passed = CreateCheck(CheckStatus.Pass);
        var notApplicable = CreateCheck(CheckStatus.NotApplicable);
        var review = CreateCheck(CheckStatus.Fail, Severity.NeedsReview);
        var unknown = CreateCheck(CheckStatus.Unknown);
        var blocked = CreateCheck(CheckStatus.Fail, Severity.Blocked);

        Assert.Equal(Decision.Blocked, InvoiceDecision.From([review, unknown, blocked]));
        Assert.Equal(Decision.NeedsInformation, InvoiceDecision.From([review, unknown]));
        Assert.Equal(Decision.NeedsReview, InvoiceDecision.From([passed, review]));
        Assert.Equal(Decision.Clear, InvoiceDecision.From([passed, notApplicable]));
    }

    [Fact]
    public void Should_ask_once_per_root_object_when_checked_lines_need_the_same_fact()
    {
        var questions = InvoiceQuestions.From([
            ("L1", Verdict.MissingFact(RequiredFact.GoodsReceipts)),
            ("L2", Verdict.MissingFact(RequiredFact.GoodsReceipts)),
            ("L3", Verdict.DependsOn(CheckCode.OrderMatch)),
            ("D", Verdict.MissingFact(RequiredFact.BankAccount)),
        ]);

        Assert.Collection(
            questions,
            first =>
            {
                Assert.Equal(RequiredFact.GoodsReceipts, first.RequiredFact);
                Assert.Equal("L1", Assert.Single(first.TargetObjectIds));
            },
            second =>
            {
                Assert.Equal(RequiredFact.GoodsReceipts, second.RequiredFact);
                Assert.Equal("L2", Assert.Single(second.TargetObjectIds));
            },
            bank =>
            {
                Assert.Equal(RequiredFact.BankAccount, bank.RequiredFact);
                Assert.Equal("D", Assert.Single(bank.TargetObjectIds));
            }
        );
    }

    private static CheckResult CreateCheck(CheckStatus status, Severity? severity = null) =>
        new(CheckCode.BankAccount, "DOC", status, severity, null, [], []);
}
