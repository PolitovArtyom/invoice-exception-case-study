using System.Text.Json.Nodes;
using InvoiceDesk.Tests.Support;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;

namespace InvoiceDesk.Tests.Api;

public sealed class NewInvoiceRegistrationTests
{
    [Fact]
    public async Task Should_evaluate_sourced_facts_when_a_new_supported_invoice_is_registered_over_mcp()
    {
        const string caseId = "case-912";
        using var environment = new CaseTestEnvironment();
        string fixture = environment.CopyOwnCaseFixture(caseId);
        var facts = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture, "facts.json")))!;
        await using var app = environment.Start();
        await using var mcp = await ConnectMcp(app);

        var opened = Payload(await Call(mcp, "open_case", caseId));
        Assert.Null(opened["current_document_id"]);
        Assert.Equal(1, opened["current_revision"]!.GetValue<int>());
        var source = Assert.Single(opened["sources"]!.AsArray())!;
        Assert.True(source["available"]!.GetValue<bool>());

        var response = Payload(
            await Register(
                mcp,
                facts,
                new { root = "own", relative_path = "cases/case-912/documents/document-001.pdf" },
                caseId
            )
        );

        var result = response["result"]!;
        AssessmentAssertions.AssertOutcome(
            result,
            "clear",
            "DOC-912",
            ["L01"],
            [],
            matchedOrderId: "ORDER-912"
        );
        AssessmentAssertions.AssertTotals(result, "7.50", "1.50", "9.00");
        AssessmentAssertions.AssertLine(
            result,
            "L01",
            "ORDER-L01",
            "deterministic",
            "7.50",
            "1.50",
            ordered: "8",
            received: "5",
            available: "5",
            remaining: "8"
        );
        Assert.Equal(2, result["input_revision"]!.GetValue<int>());
        Assert.Equal("CTX-912-v1", result["context_version"]!.GetValue<string>());

        var saved = Payload(await Call(mcp, "get_assessment", caseId))["revision"]!;
        AssertSameJson(result, saved["result"]);
        AssertSameJson(facts, saved["document"]!["facts"]);
        Assert.Equal("agent", saved["document"]!["reading_origin"]!.GetValue<string>());
    }
}
