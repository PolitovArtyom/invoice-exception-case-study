using System.Text.Json.Nodes;
using InvoiceDesk.Tests.Support;
using static InvoiceDesk.Tests.Support.AssessmentAssertions;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;

namespace InvoiceDesk.Tests.Api;

public sealed class PublicDiscrepancyTests
{
    [Fact]
    public async Task Should_preserve_price_failure_when_case_004_contains_instruction_text()
    {
        const string caseId = "case-004";
        using var environment = new CaseTestEnvironment();
        environment.CopyCase(caseId);
        await using var app = environment.Start();
        await using var mcp = await ConnectMcp(app);
        using var page = app.CreateClient();
        var opened = Payload(await Call(mcp, "open_case", caseId));
        Assert.Null(opened["current_document_id"]);
        var source = Assert.Single(opened["sources"]!.AsArray())!;
        var facts = PublicCases.ReadFirstStep(caseId)["document"]!["facts"]!;
        var registered = Payload(
            await Register(
                mcp,
                facts,
                new { root = Text(source, "root"), relative_path = Text(source, "relative_path") },
                caseId
            )
        );
        var view = await Read(page, $"/api/cases/{caseId}");
        var revision = view["revision"]!;
        var result = revision["result"]!;
        AssertSameJson(registered["result"], result);

        AssertOutcome(
            result,
            "needs_review",
            "DOC-004",
            ["L01", "L02"],
            [new("UNIT_PRICE", "L01", "fail", "needs_review")],
            matchedOrderId: "ORD-004"
        );
        Assert.Equal("needs_attention", Text(view, "stage"));
        AssertTotals(result, "1116.80", "223.36", "1340.16");
        AssertLine(
            result,
            "L01",
            "OL-004-01",
            "deterministic",
            "1001.60",
            "200.32",
            "80",
            "80",
            available: "80",
            remaining: "80"
        );
        AssertLine(
            result,
            "L02",
            "OL-004-02",
            "deterministic",
            "115.20",
            "23.04",
            "24",
            "24",
            available: "24",
            remaining: "24"
        );
        var price = Assert.Single(result["checks"]!.AsArray(), item =>
            Text(item!, "code") == "UNIT_PRICE" && Text(item!, "object_id") == "L01"
        )!;
        Assert.Equal("12.52", Text(price["actual"]!["unit_net_price"]!, "value"));
        Assert.Equal("12.50", Text(price["expected"]!["unit_net_price"]!, "value"));
        Assert.Equal("0.02", Text(price["actual"]!["absolute_difference"]!, "value"));
        Assert.Equal(
            "12e736824c80d8c86623ec06b9df275e7b9307e9fdbc21e45115f4abb706fd58",
            Text(revision["document"]!, "content_sha256")
        );
        var history = Payload(await Call(mcp, "get_case_history", caseId));
        Assert.Collection(
            history.AsArray(),
            entry => Assert.Equal("agent", Text(entry!["action"]!, "actor")),
            entry => Assert.Equal("trusted_loader", Text(entry!["action"]!, "actor"))
        );
    }

    [Fact]
    public async Task Should_preserve_printed_total_when_case_005_does_not_add_up()
    {
        const string caseId = "case-005";
        using var environment = new CaseTestEnvironment();
        environment.CopyCase(caseId);
        await using var app = environment.Start();
        using var page = app.CreateClient();
        await using var mcp = await ConnectMcp(app);
        var view = await Read(page, $"/api/cases/{caseId}");
        var revision = view["revision"]!;
        var result = revision["result"]!;
        var assessed = Payload(await Call(mcp, "assess_invoice", caseId));
        AssertSameJson(result, assessed["result"]);

        AssertBlockedTotals(result);
        Assert.Equal("needs_attention", Text(view, "stage"));
        var facts = revision["document"]!["facts"]!;
        var issue = Assert.Single(view["issues"]!.AsArray())!;
        var source = Assert.Single(issue["facts"]!.AsArray())!;
        Assert.Equal("/declared_total", Text(source, "path"));
        Assert.Equal("1673.20", Text(source, "value"));
        Assert.Equal("1663.20", Text(source, "calculated_value"));
        Assert.Equal("1,673.20", Text(Assert.Single(source["quotes"]!.AsArray())!, "excerpt"));
        Assert.Equal("1673.20", Text(facts, "declared_total"));
        Assert.Equal("1386.00", Text(facts, "declared_subtotal"));
        Assert.Equal("277.20", Text(facts, "declared_tax_total"));
    }

    internal static void AssertBlockedTotals(JsonNode result)
    {
        AssertOutcome(
            result,
            "blocked",
            "DOC-005",
            ["L01", "L02", "L03"],
            [new("TOTALS_ARITHMETIC", "DOC-005", "fail", "blocked")],
            matchedOrderId: "ORD-005"
        );
        AssertTotals(result, "1386.00", "277.20", "1663.20");
        AssertLine(
            result,
            "L01",
            "OL-005-01",
            "deterministic",
            "768.00",
            "153.60",
            "20",
            "20",
            available: "20",
            remaining: "20"
        );
        AssertLine(
            result,
            "L02",
            "OL-005-02",
            "deterministic",
            "450.00",
            "90.00",
            "40",
            "40",
            available: "40",
            remaining: "40"
        );
        AssertLine(
            result,
            "L03",
            "OL-005-03",
            "deterministic",
            "168.00",
            "33.60",
            "10",
            "10",
            available: "10",
            remaining: "10"
        );
    }
}
