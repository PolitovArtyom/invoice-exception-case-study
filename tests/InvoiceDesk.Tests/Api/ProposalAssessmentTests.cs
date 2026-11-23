using System.Text.Json.Nodes;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Tests.Support;
using static InvoiceDesk.Tests.Api.OrderMatchingTests;
using static InvoiceDesk.Tests.Support.AssessmentAssertions;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;
using static InvoiceDesk.Tests.Support.ProposalCase;

namespace InvoiceDesk.Tests.Api;

public sealed class ProposalAssessmentTests
{
    [Fact]
    [Trait("H", "H-19")]
    public async Task Should_confirm_proposed_order_when_explicit_reference_is_unknown()
    {
        var scenario = WithUnknownReference(MatchedInvoiceScenario.Baseline());
        await using var test = await Start(scenario);
        AssertUnknownOrder(test.Initial);

        var proposed = await test.Propose(Proposal("P", "O", [new("L", "OL")]));
        AssertOutcome(
            proposed,
            "needs_review",
            "D",
            ["L"],
            [new("ORDER_MATCH", "D", "fail", "needs_review")],
            orderMatchSource: "agent_proposal"
        );
        AssertLine(proposed, "L", "OL", "deterministic", "10.00", "2.00");
        AssertTotals(proposed, "10.00", "2.00", "12.00");

        var confirmed = await test.Confirm("P", 3);
        AssertOutcome(confirmed, "clear", "D", ["L"], [], orderMatchSource: "human_confirmation");
        AssertLine(confirmed, "L", "OL", "deterministic", "10.00", "2.00");
        AssertTotals(confirmed, "10.00", "2.00", "12.00");
        AssertSameJson(confirmed, (await Read(test.Page, CaseUrl))["revision"]!["result"]);
    }

    [Fact]
    [Trait("H", "H-24")]
    public async Task Should_keep_unit_review_when_match_is_deterministic_proposed_or_confirmed()
    {
        var baseline = MatchedInvoiceScenario.Baseline();
        var scenario = baseline.WithFirstLine(baseline.Facts.Lines[0] with { Unit = "box" });
        AssertDifferentUnits(await scenario.RegisterAndRead(), "deterministic", "10");

        await using var test = await Start(
            scenario.WithFirstLine(
                scenario.Facts.Lines[0] with
                {
                    Sku = null,
                    Description = "Different product label",
                }
            )
        );
        AssertDifferentUnits(
            await test.Propose(Proposal("P", "O", [new("L", "OL")])),
            "agent_proposal",
            "10"
        );
        AssertDifferentUnits(await test.Confirm("P", 3), "human_confirmation", "10");
    }

    [Fact]
    [Trait("H", "H-25")]
    public async Task Should_skip_quantity_and_price_comparison_when_units_differ_and_receipts_are_unavailable()
    {
        var baseline = MatchedInvoiceScenario.Baseline();
        var scenario = baseline.WithFirstLine(baseline.Facts.Lines[0] with { Unit = "box" }) with
        {
            CompanyData = baseline.CompanyData with
            {
                GoodsReceipts = new("1.0", ReceiptAvailability.Unavailable, []),
            },
        };
        AssertDifferentUnits(await scenario.RegisterAndRead(), "deterministic", null);

        var unmatched = scenario.WithFirstLine(
            scenario.Facts.Lines[0] with
            {
                Sku = null,
                Description = "Different product label",
            }
        );
        await using var test = await Start(unmatched);
        AssertDifferentUnits(
            await test.Propose(Proposal("P", "O", [new("L", "OL")])),
            "agent_proposal",
            null
        );
        AssertDifferentUnits(await test.Confirm("P", 3), "human_confirmation", null);
    }

    [Theory]
    [InlineData("B2", "10", "10.00", "BUYER_IDENTITY", "D")]
    [InlineData("B", "0.5", "10.00", "QTY_RECEIVED", "L")]
    [InlineData("B", "10", "10.01", "LINE_ARITHMETIC", "L")]
    [Trait("H", "H-42")]
    public async Task Should_keep_independent_violation_when_a_person_confirms_a_proposal(
        string orderBuyer,
        string received,
        string declaredNet,
        string failedCheck,
        string failedObject
    )
    {
        var baseline = WithUnknownReference(MatchedInvoiceScenario.Baseline());
        var order = baseline.CompanyData.PurchaseOrders.Orders[0];
        var scenario = baseline
            .WithOrders(order with { BuyerId = orderBuyer })
            .WithFirstLine(baseline.Facts.Lines[0] with { DeclaredNetAmount = Parse(declaredNet) });
        var company = scenario.CompanyData;
        scenario = scenario with
        {
            CompanyData = company with
            {
                Buyers = company.Buyers with
                {
                    Buyers =
                    [
                        .. company.Buyers.Buyers,
                        new("B2", "Second buyer", "DE", "BUY-OTHER", []),
                    ],
                },
                GoodsReceipts = company.GoodsReceipts with
                {
                    Receipts =
                    [
                        company.GoodsReceipts.Receipts[0] with
                        {
                            AcceptedQuantity = Parse(received),
                        },
                    ],
                },
            },
        };
        await using var test = await Start(scenario);
        var proposed = await test.Propose(Proposal());
        AssertOutcome(
            proposed,
            "blocked",
            "D",
            ["L"],
            [
                new("ORDER_MATCH", "D", "fail", "needs_review"),
                new(failedCheck, failedObject, "fail", "blocked"),
            ],
            orderMatchSource: "agent_proposal"
        );
        AssertLine(
            proposed,
            "L",
            "OL",
            "deterministic",
            "10.00",
            "2.00",
            received: received,
            available: received
        );
        AssertTotals(proposed, "10.00", "2.00", "12.00");
        var preview = await Read(test.Page, CaseUrl + "/proposals/P/confirmation-preview");
        var confirmed = await test.Confirm("P", 3);
        AssertSameJson(preview["result"], confirmed);
        AssertOutcome(
            confirmed,
            "blocked",
            "D",
            ["L"],
            [new(failedCheck, failedObject, "fail", "blocked")],
            orderMatchSource: "human_confirmation"
        );
        AssertLine(
            confirmed,
            "L",
            "OL",
            "deterministic",
            "10.00",
            "2.00",
            received: received,
            available: received
        );
        AssertTotals(confirmed, "10.00", "2.00", "12.00");
    }

    internal static MatchedInvoiceScenario WithUnknownReference(MatchedInvoiceScenario baseline) =>
        baseline with
        {
            Facts = baseline.Facts with { PurchaseOrderReference = "PO-NOT-FOUND" },
        };

    private static decimal Parse(string value) =>
        decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    private static void AssertDifferentUnits(JsonNode result, string source, string? received)
    {
        AssertOutcome(
            result,
            "needs_review",
            "D",
            ["L"],
            [
                new("LINE_MATCH", "L", "fail", "needs_review"),
                new("QTY_RECEIVED", "L", "not_applicable"),
                new("QTY_ORDER_REMAINING", "L", "not_applicable"),
                new("UNIT_PRICE", "L", "not_applicable"),
            ],
            lineMatchSources: [source]
        );
        AssertLine(
            result,
            "L",
            "OL",
            source,
            "10.00",
            "2.00",
            received: received,
            available: received
        );
        AssertTotals(result, "10.00", "2.00", "12.00");
    }

    [Theory]
    [InlineData("pcs", "accounts_payable")]
    [InlineData("box", "procurement")]
    public async Task Should_show_the_same_owner_on_the_list_and_detail_when_a_line_needs_attention(
        string unit,
        string proposedOwner
    )
    {
        var baseline = MatchedInvoiceScenario.Baseline();
        await using var test = await Start(
            baseline.WithFirstLine(
                baseline.Facts.Lines[0] with
                {
                    Sku = null,
                    Description = "Different product label",
                    Unit = unit,
                }
            )
        );
        await AssertIssueOwner("procurement");

        await test.Propose(Proposal("P", "O", [new("L", "OL")]));
        await AssertIssueOwner(proposedOwner);

        async Task AssertIssueOwner(string owner)
        {
            var list = await Read(test.Page, "/api/cases");
            var summary = Assert.Single(list["cases"]!.AsArray())!;
            Assert.Equal("LINE_MATCH", Text(summary, "main_issue"));
            Assert.Equal(owner, Text(summary, "main_issue_owner"));
            var detail = await Read(test.Page, CaseUrl);
            var issue = Assert.Single(detail["issues"]!.AsArray())!;
            Assert.Equal("LINE_MATCH", Text(issue["check"]!, "code"));
            Assert.Equal("L", Text(issue["check"]!, "object_id"));
            Assert.Equal(owner, Text(issue, "owner"));
        }
    }
}
