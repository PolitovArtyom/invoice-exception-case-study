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

    private static CheckResult CreateCheck(CheckStatus status, Severity? severity = null) =>
        new(CheckCode.BankAccount, "DOC", status, severity, null, [], []);
}
