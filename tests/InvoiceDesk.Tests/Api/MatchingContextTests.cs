using InvoiceDesk.Core.Validation;
using InvoiceDesk.Tests.Support;
using static InvoiceDesk.Tests.Support.AssessmentAssertions;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;

namespace InvoiceDesk.Tests.Api;

public sealed class MatchingContextTests
{
    [Fact]
    [Trait("Decision", "A-16")]
    public async Task Should_reject_context_when_order_line_ids_collide()
    {
        var scenario = MatchedInvoiceScenario.Baseline();
        await using var test = await ProposalCase.Start(scenario);
        var original = await test.History();
        var order = scenario.CompanyData.PurchaseOrders.Orders[0];
        var other = order with { OrderId = "O2", Reference = "OTHER-PO" };
        var invalid = scenario.WithOrders(order, other).CompanyData;

        var error = Assert.Throws<InvoiceValidationException>(() =>
            test.ReplaceCompanyData(invalid)
        );
        var detail = Assert.Single(error.Details);
        Assert.Equal("/context/purchase_orders/orders", detail.Path);
        Assert.Equal("Each order line ID must be unique.", detail.Message);
        AssertSameJson(original, await test.History());
    }

    [Fact]
    [Trait("Decision", "A-16")]
    public async Task Should_ignore_receipts_when_their_line_is_absent()
    {
        var scenario = MatchedInvoiceScenario.Baseline();
        var company = scenario.CompanyData;
        var original = company.GoodsReceipts.Receipts[0];
        var receipt = original with
        {
            ReceiptId = "STALE",
            OrderLineId = "NO-LINE",
            AcceptedQuantity = 17m,
        };
        var withStaleReceipt = scenario with
        {
            CompanyData = company with
            {
                GoodsReceipts = company.GoodsReceipts with { Receipts = [original, receipt] },
            },
        };

        var result = await withStaleReceipt.RegisterAndRead();
        AssertOutcome(result, "clear", "D", ["L"], []);
        AssertLine(result, "L", "OL", "deterministic", "10.00", "2.00");
        AssertTotals(result, "10.00", "2.00", "12.00");
    }
}
