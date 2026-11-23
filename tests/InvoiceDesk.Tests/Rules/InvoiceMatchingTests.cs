using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Matching;
using InvoiceDesk.Core.Rules.Parties;
using InvoiceDesk.Tests.Support;

namespace InvoiceDesk.Tests.Rules;

public sealed class InvoiceMatchingTests
{
    [Fact]
    public void Should_leave_single_target_unresolved_when_an_ambiguous_line_shares_the_target()
    {
        var baseline = MatchedInvoiceScenario.Baseline();
        var line = baseline.Facts.Lines[0];
        var facts = baseline.Facts with
        {
            Lines = [line with { Description = "Tape" }, line with { LineId = "L2" }],
        };
        var order = baseline.CompanyData.PurchaseOrders.Orders[0];
        var other = order.Lines[0] with { LineId = "OL2", Sku = "TAPE", Description = "Tape" };

        Assert.Equal(
            new OrderLineMatch?[] { null, null },
            InvoiceMatches.FindLines(facts, order with { Lines = [.. order.Lines, other] })
        );
    }

    [Fact]
    public void Should_use_deterministic_target_when_it_becomes_available_after_confirmation()
    {
        var baseline = MatchedInvoiceScenario.Baseline();
        var order = baseline.CompanyData.PurchaseOrders.Orders[0];
        var former = order.Lines[0] with
        {
            LineId = "OL-OLD",
            Sku = "FORMER",
            Description = "Former",
        };
        var updated = baseline.WithOrderLines(former, order.Lines[0]);
        var proposal = new StepProposal("P", RevisionActor.Agent, "D", "O", [new("L", "OL-OLD")]);

        var matching = new ProposalMatching(
            updated.Facts,
            2,
            updated.CompanyData,
            InvoiceParties.Resolve(updated.Facts, updated.CompanyData)
        );
        var matches = matching.Resolve([new(proposal, 2, new("Ana", 1))]);

        Assert.Equal(MatchSource.Deterministic, matches.OrderSource);
        var line = Assert.IsType<OrderLineMatch>(Assert.Single(matches.Lines));
        Assert.Equal("OL", line.OrderLine.LineId);
        Assert.Equal(MatchSource.Deterministic, line.Source);
    }
}
