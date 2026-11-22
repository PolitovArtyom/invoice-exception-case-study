using System.Text.Json.Nodes;
using InvoiceDesk.Tests.Support;
using static InvoiceDesk.Tests.Api.OrderMatchingTests;
using static InvoiceDesk.Tests.Support.AssessmentAssertions;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;

namespace InvoiceDesk.Tests.Api;

public sealed class PublicProposalLifecycleTests
{
    [Fact]
    public async Task Should_keep_three_assessments_when_010_is_completed_through_mcp_and_page()
    {
        const string caseId = "case-010";
        const string caseUrl = "/api/cases/" + caseId;
        const string proposalId = "PROP-010-01";
        using var environment = new CaseTestEnvironment();
        environment.CopyCase(caseId);
        var steps = PublicCases
            .Steps(caseId)
            .Select(path => JsonNode.Parse(File.ReadAllText(path))!)
            .ToArray();
        var results = new List<JsonNode>();
        await using (var app = environment.Start())
        {
            using var page = app.CreateClient();
            await using var mcp = await ConnectMcp(app);
            var opened = Payload(await Call(mcp, "open_case", caseId));
            Assert.Null(opened["current_document_id"]);
            string source = Text(opened["sources"]![0]!, "relative_path")!;
            var initial = Payload(
                await Register(
                    mcp,
                    steps[0]["document"]!["facts"]!,
                    new { root = "public", relative_path = source },
                    caseId
                )
            )["result"]!;
            AssertOutcome(
                initial,
                "needs_information",
                "DOC-010",
                ["L01", "L02"],
                [
                    new("ORDER_MATCH", "DOC-010", "unknown"),
                    Dependent("SUPPLIER_IDENTITY", "DOC-010", "ORDER_MATCH"),
                    Dependent("BUYER_IDENTITY", "DOC-010", "ORDER_MATCH"),
                    Dependent("CURRENCY_MATCH", "DOC-010", "ORDER_MATCH"),
                    .. ComparisonsWithoutOrder("L01"),
                    .. ComparisonsWithoutOrder("L02"),
                ],
                [("order_reference", "DOC-010")],
                null,
                null,
                lineMatchSources: [null, null]
            );
            AssertLine(initial, "L01", null, null, "35.00", "7.00", null, null, null, null, null);
            AssertLine(initial, "L02", null, null, "375.00", "75.00", null, null, null, null, null);

            var proposed = Payload(await Propose(mcp, steps[1]["proposals"]![0]!, caseId))[
                "result"
            ]!;
            AssertOutcome(
                proposed,
                "needs_review",
                "DOC-010",
                ["L01", "L02"],
                [
                    new("ORDER_MATCH", "DOC-010", "fail", "needs_review"),
                    new("LINE_MATCH", "L01", "fail", "needs_review"),
                ],
                matchedOrderId: "ORD-010",
                orderMatchSource: "agent_proposal",
                lineMatchSources: ["agent_proposal", "deterministic"]
            );
            AssertLines(proposed, "agent_proposal");

            using var response = await Confirm(
                page,
                caseId,
                ProposalCase.Form(proposalId, 3, "Delivery reviewer")
            );
            response.EnsureSuccessStatusCode();
            var confirmed = (await Body(response))["result"]!;
            AssertOutcome(
                confirmed,
                "clear",
                "DOC-010",
                ["L01", "L02"],
                [],
                matchedOrderId: "ORD-010",
                orderMatchSource: "human_confirmation",
                lineMatchSources: ["human_confirmation", "deterministic"]
            );
            AssertLines(confirmed, "human_confirmation");
            results.AddRange([confirmed, proposed, initial]);
            foreach (var result in results)
                AssertTotals(result, "410.00", "82.00", "492.00");

            var recheck = Payload(await Call(mcp, "assess_invoice", caseId));
            Assert.True(recheck["inputs_unchanged"]!.GetValue<bool>());
            AssertSameJson(confirmed, recheck["result"]);
        }

        await using var restarted = environment.Start();
        using var browser = restarted.CreateClient();
        var history = (await Read(browser, caseUrl + "/history")).AsArray();
        Assert.Equal(new[] { 4, 3, 2, 1 }, history.Select(row => row!["number"]!.GetValue<int>()));
        Assert.Null(history[3]!["result"]);
        Assert.Equal(
            "Delivery reviewer",
            Text(history[0]!["action"]!["confirmations"]![0]!, "confirmed_by")
        );
        for (int index = 0; index < results.Count; index++)
        {
            var entry = history[index]!;
            AssertSameJson(results[index], entry["result"]);
            var saved = await Read(
                browser,
                caseUrl + "/revisions/" + entry["number"]!.GetValue<int>()
            );
            AssertSameJson(results[index], saved["revision"]!["result"]);
        }
    }

    private static void AssertLines(JsonNode result, string source)
    {
        AssertLine(
            result,
            "L01",
            "OL-010-01",
            source,
            "35.00",
            "7.00",
            "100",
            "100",
            "0",
            "100",
            "100"
        );
        AssertLine(
            result,
            "L02",
            "OL-010-02",
            "deterministic",
            "375.00",
            "75.00",
            "20",
            "20",
            "0",
            "20",
            "20"
        );
    }
}
