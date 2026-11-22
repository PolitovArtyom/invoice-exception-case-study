using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules;

namespace InvoiceDesk.Tests.Rules;

public sealed class InvoiceDecisionTests
{
    [Fact]
    public void Should_follow_core_v1_priority_when_checks_have_mixed_statuses()
    {
        var passed = CreateCheck("pass");
        var notApplicable = CreateCheck("not_applicable");
        var review = CreateCheck("fail", "needs_review");
        var unknown = CreateCheck("unknown");
        var blocked = CreateCheck("fail", "blocked");

        Assert.Equal("blocked", InvoiceDecision.From([review, unknown, blocked]));
        Assert.Equal("needs_information", InvoiceDecision.From([review, unknown]));
        Assert.Equal("needs_review", InvoiceDecision.From([passed, review]));
        Assert.Equal("clear", InvoiceDecision.From([passed, notApplicable]));
    }

    private static InvoiceCheck CreateCheck(string status, string? severity = null) =>
        new("SYNTHETIC", "DOC", status, severity, null,
            new Dictionary<string, CheckOperand>(), new Dictionary<string, CheckOperand>(), []);
}
