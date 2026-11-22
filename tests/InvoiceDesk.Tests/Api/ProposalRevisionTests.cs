using System.Net;
using System.Text.Json.Nodes;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Tests.Support;
using static InvoiceDesk.Tests.Api.ProposalAssessmentTests;
using static InvoiceDesk.Tests.Api.ProposalValidationTests;
using static InvoiceDesk.Tests.Support.AssessmentAssertions;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;
using static InvoiceDesk.Tests.Support.ProposalCase;

namespace InvoiceDesk.Tests.Api;

public sealed class ProposalRevisionTests
{
    [Fact]
    public async Task Should_preview_exact_proposal_without_writes_when_a_person_selects_an_older_proposal()
    {
        var scenario = WithTwoOrders();
        await using var test = await Start(scenario);
        await test.Propose(Proposal("P/older", "O"));
        var latest = await test.Propose(Proposal("P2", "O2"));
        var saved = await test.History();
        AssertOutcome(
            latest,
            "blocked",
            "D",
            ["L"],
            [
                new("ORDER_MATCH", "D", "fail", "needs_review"),
                new("QTY_RECEIVED", "L", "fail", "blocked"),
            ],
            matchedOrderId: "O2",
            orderMatchSource: "agent_proposal"
        );
        AssertLine(
            latest,
            "L",
            "OL2",
            "deterministic",
            "10.00",
            "2.00",
            received: "0",
            available: "0"
        );
        AssertTotals(latest, "10.00", "2.00", "12.00");

        var preview = await Read(test.Page, CaseUrl + "/proposals/P%2Folder/confirmation-preview");
        Assert.Equal("P/older", Text(preview, "proposal_id"));
        Assert.Equal(4, preview["expected_revision"]!.GetValue<int>());
        var hypothetical = preview["result"]!;
        AssertOutcome(
            hypothetical,
            "clear",
            "D",
            ["L"],
            [],
            orderMatchSource: "human_confirmation"
        );
        AssertLine(hypothetical, "L", "OL", "deterministic", "10.00", "2.00");
        AssertTotals(hypothetical, "10.00", "2.00", "12.00");
        var page = await Read(test.Page, CaseUrl);
        AssertSameJson(latest, page["revision"]!["result"]);
        AssertProposalOptions(page, "P2", ["O", "O2"], ("P2", true, true), ("P/older", true, true));
        AssertSameJson(saved, await test.History());
        AssertSameJson(hypothetical, await test.Confirm("P/older", 4));
    }

    [Fact]
    public async Task Should_preview_and_confirm_an_older_proposal_when_the_order_matches_deterministically()
    {
        var baseline = MatchedInvoiceScenario.Baseline();
        var scenario = baseline.WithFirstLine(
            baseline.Facts.Lines[0] with
            {
                LineId = "L01",
                Sku = null,
                Description = "Protective gear",
            }
        );
        await using var test = await Start(scenario);
        await test.Propose(Proposal("OLDER", "O"));
        var latest = await test.Propose(Proposal("NEWER", "O", [new("L01", "OL")]));
        AssertOutcome(
            latest,
            "needs_review",
            "D",
            ["L01"],
            [new("LINE_MATCH", "L01", "fail", "needs_review")],
            lineMatchSources: ["agent_proposal"]
        );
        AssertLine(latest, "L01", "OL", "agent_proposal", "10.00", "2.00");
        AssertTotals(latest, "10.00", "2.00", "12.00");
        var before = await test.History();

        var preview = await Read(test.Page, CaseUrl + "/proposals/OLDER/confirmation-preview");
        Assert.Equal("OLDER", Text(preview, "proposal_id"));
        Assert.Equal(4, preview["expected_revision"]!.GetValue<int>());
        var hypothetical = preview["result"]!;
        AssertOutcome(
            hypothetical,
            "needs_information",
            "D",
            ["L01"],
            [
                new("LINE_MATCH", "L01", "unknown"),
                new("QTY_RECEIVED", "L01", "unknown", Dependency: "LINE_MATCH"),
                new("QTY_ORDER_REMAINING", "L01", "unknown", Dependency: "LINE_MATCH"),
                new("UNIT_PRICE", "L01", "unknown", Dependency: "LINE_MATCH"),
            ],
            [("line_match", "L01")],
            lineMatchSources: [null]
        );
        OrderMatchingTests.AssertUnmatchedLine(hypothetical, "L01");
        AssertTotals(hypothetical, "10.00", "2.00", "12.00");
        AssertSameJson(latest, (await Read(test.Page, CaseUrl))["revision"]!["result"]);
        AssertSameJson(before, await test.History());

        AssertSameJson(hypothetical, await test.Confirm("OLDER", 4));
        var after = (await test.History()).AsArray();
        Assert.Equal(before.AsArray().Count + 1, after.Count);
        Assert.Equal("OLDER", Text(after[0]!["action"]!["confirmations"]![0]!, "proposal_id"));
    }

    [Fact]
    [Trait("Decision", "A-12")]
    public async Task Should_advance_revision_when_a_successful_input_is_repeated()
    {
        var scenario = WithUnknownReference(MatchedInvoiceScenario.Baseline());
        await using var test = await Start(scenario);
        Assert.Equal(2, Revision(test.Initial));
        AssertDuplicate(await test.Register(scenario.Facts), "D", "D", "2", 3);

        AssertProposedBaseline(await test.Propose(Proposal()), 4);
        AssertProposedBaseline(await test.Propose(Proposal()), 5);

        AssertHumanBaseline(await test.Confirm("P", 5), 6);
        AssertHumanBaseline(await test.Confirm("P", 6, "Mina"), 7);
        AssertHumanBaseline(test.ReplaceCompanyData(scenario.CompanyData), 8);
        var refreshed = test.ReplaceCompanyData(scenario.CompanyData);
        AssertHumanBaseline(refreshed, 9);

        var history = (await test.History()).AsArray();
        Assert.Equal(
            Enumerable.Range(1, 9).Reverse(),
            history.Select(row => row!["number"]!.GetValue<int>())
        );
        Assert.Null(history[6]!["result"]);
        Assert.Equal("Mina", Text(history[2]!["action"]!["confirmations"]![0]!, "confirmed_by"));
        Assert.Equal(
            "Ana",
            Text(
                (await Read(test.Page, CaseUrl))["proposals"]![0]!["registration"]![
                    "confirmation"
                ]!,
                "confirmed_by"
            )
        );
        var unchanged = Payload(await Call(test.Mcp, "assess_invoice"));
        Assert.True(unchanged["inputs_unchanged"]!.GetValue<bool>());
        AssertSameJson(refreshed, unchanged["result"]);
        AssertSameJson(history, await test.History());
    }

    [Fact]
    [Trait("Decision", "A-14")]
    public async Task Should_keep_confirmed_precedence_when_proposals_or_context_references_change()
    {
        var scenario = WithTwoOrders();
        var order = scenario.CompanyData.PurchaseOrders.Orders[0];
        var other = scenario.CompanyData.PurchaseOrders.Orders[1];
        await using var test = await Start(scenario);
        await test.Propose(Proposal("P1", "O", [new("L", "OL")]));
        await test.Propose(Proposal("P2", "O2", [new("L", "OL2")]));
        var resent = await test.Propose(Proposal("P1", "O", [new("L", "OL")]));
        AssertOutcome(
            resent,
            "blocked",
            "D",
            ["L"],
            [
                new("ORDER_MATCH", "D", "fail", "needs_review"),
                new("QTY_RECEIVED", "L", "fail", "blocked"),
            ],
            matchedOrderId: "O2",
            orderMatchSource: "agent_proposal"
        );

        AssertHumanWithoutReceipts(await test.Confirm("P2", 5));
        AssertProposalOptions(
            await Read(test.Page, CaseUrl),
            "P2",
            ["O", "O2"],
            ("P2", true, false),
            ("P1", true, true)
        );
        AssertHumanBaseline(await test.Confirm("P1", 6));
        AssertHumanBaseline(await test.Propose(Proposal("P3", "O2")));
        AssertHumanBaseline(await test.Confirm("P2", 8));

        var missingLine = order with { Lines = [order.Lines[0] with { LineId = "OL3" }] };
        var withoutLine = scenario.WithOrders(missingLine, other).CompanyData with
        {
            ContextVersion = "CTX-MISSING-LINE",
        };
        AssertHumanWithoutReceipts(test.ReplaceCompanyData(withoutLine));
        AssertProposalOptions(
            await Read(test.Page, CaseUrl),
            "P2",
            ["O", "O2"],
            ("P3", true, true),
            ("P2", true, false),
            ("P1", false, false)
        );
        using var rejected = await Confirm(test.Page, CaseId, Form("P1", 10));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        AssertValidation(await Body(rejected), "/proposal_id", "no longer applies");

        var withoutOrders = scenario.WithOrders().CompanyData with
        {
            ContextVersion = "CTX-MISSING-ORDERS",
        };
        OrderMatchingTests.AssertUnknownOrder(test.ReplaceCompanyData(withoutOrders));
        AssertHumanBaseline(test.ReplaceCompanyData(scenario.CompanyData));
        Assert.Equal(12, (await test.History()).AsArray().Count);
    }

    [Fact]
    public async Task Should_show_saved_sources_and_results_when_old_revisions_are_requested()
    {
        var scenario = WithUnknownReference(MatchedInvoiceScenario.Baseline());
        await using var test = await Start(scenario);
        var proposed = await test.Propose(Proposal());
        var confirmed = await test.Confirm("P", 3, "Ana Reviewer");
        test.ReplaceCompanyData(scenario.CompanyData with { ContextVersion = "CTX-LATEST" });

        var history = (await test.History()).AsArray();
        Assert.Equal(
            new[] { 5, 4, 3, 2, 1 },
            history.Select(row => row!["number"]!.GetValue<int>())
        );
        Assert.Equal("CTX-LATEST", Text(history[0]!, "context_version"));
        Assert.Equal(
            "Ana Reviewer",
            Text(history[1]!["action"]!["confirmations"]![0]!, "confirmed_by")
        );
        Assert.Equal("human", Text(history[1]!["action"]!, "actor"));
        Assert.Equal("confirm_proposal", Text(history[1]!["action"]!, "kind"));
        Assert.Equal("P", history[2]!["action"]!["proposal_ids"]![0]!.GetValue<string>());

        var historical = await Read(test.Page, CaseUrl + "/revisions/3");
        var saved = historical["revision"]!;
        Assert.True(historical["superseded"]!.GetValue<bool>());
        Assert.Equal(5, historical["current_revision"]!.GetValue<int>());
        Assert.Equal("core-v1", Text(saved, "policy_version"));
        Assert.Equal("CTX-BASELINE", Text(saved, "context_version"));
        Assert.Equal("D", Text(saved["document"]!["facts"]!, "document_id"));
        Assert.Equal("agent", Text(saved["action"]!, "actor"));
        Assert.Equal("propose_match", Text(saved["action"]!, "kind"));
        Assert.Null(historical["proposals"]![0]!["registration"]!["confirmation"]);
        AssertSameJson(proposed, saved["result"]);
        AssertSameJson(
            confirmed,
            (await Read(test.Page, CaseUrl + "/revisions/4"))["revision"]!["result"]
        );
        AssertSameJson(history, await test.History());
    }

    private static void AssertProposalOptions(
        JsonNode page,
        string effective,
        string[] openOrders,
        params (string Id, bool Applicable, bool CanConfirm)[] expected
    )
    {
        Assert.Equal(effective, Text(page, "effective_proposal_id"));
        Assert.Equal(
            openOrders,
            page["open_supplier_orders"]!.AsArray().Select(order => Text(order!, "order_id"))
        );
        Assert.Equal(
            expected,
            page["proposals"]!
                .AsArray()
                .Select(option =>
                    (
                        Text(option!["registration"]!["proposal"]!, "proposal_id")!,
                        option["applicable"]!.GetValue<bool>(),
                        option["can_confirm"]!.GetValue<bool>()
                    )
                )
        );
    }

    private static MatchedInvoiceScenario WithTwoOrders()
    {
        var scenario = WithUnknownReference(MatchedInvoiceScenario.Baseline());
        var order = scenario.CompanyData.PurchaseOrders.Orders[0];
        return scenario.WithOrders(
            order,
            order with
            {
                OrderId = "O2",
                Reference = "PO-OTHER",
                Lines = [order.Lines[0] with { LineId = "OL2" }],
            }
        );
    }

    private static void AssertProposedBaseline(JsonNode result, int revision)
    {
        Assert.Equal(revision, Revision(result));
        AssertOutcome(
            result,
            "needs_review",
            "D",
            ["L"],
            [new("ORDER_MATCH", "D", "fail", "needs_review")],
            orderMatchSource: "agent_proposal"
        );
        AssertLine(result, "L", "OL", "deterministic", "10.00", "2.00");
        AssertTotals(result, "10.00", "2.00", "12.00");
    }

    private static void AssertHumanBaseline(JsonNode result, int revision)
    {
        Assert.Equal(revision, Revision(result));
        AssertHumanBaseline(result);
    }

    private static void AssertHumanBaseline(JsonNode result)
    {
        AssertOutcome(result, "clear", "D", ["L"], [], orderMatchSource: "human_confirmation");
        AssertLine(result, "L", "OL", "deterministic", "10.00", "2.00");
        AssertTotals(result, "10.00", "2.00", "12.00");
    }

    private static void AssertHumanWithoutReceipts(JsonNode result)
    {
        AssertOutcome(
            result,
            "blocked",
            "D",
            ["L"],
            [new("QTY_RECEIVED", "L", "fail", "blocked")],
            matchedOrderId: "O2",
            orderMatchSource: "human_confirmation"
        );
        AssertLine(
            result,
            "L",
            "OL2",
            "deterministic",
            "10.00",
            "2.00",
            received: "0",
            available: "0"
        );
        AssertTotals(result, "10.00", "2.00", "12.00");
    }

    private static int Revision(JsonNode result) => result["input_revision"]!.GetValue<int>();
}
