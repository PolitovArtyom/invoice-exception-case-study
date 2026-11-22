using System.Text.Json.Nodes;
using InvoiceDesk.Tests.Support;
using ModelContextProtocol.Protocol;
using static InvoiceDesk.Tests.Support.AssessmentAssertions;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;
using static InvoiceDesk.Tests.Support.MatchedInvoiceScenario;

namespace InvoiceDesk.Tests.Api;

public sealed class InvoiceFactValidationTests
{
    [Theory]
    [Trait("H", "H-40")]
    [InlineData("lines/0/quantity", "\"0\"")]
    [InlineData("lines/0/quantity", "\"0.000\"")]
    [InlineData("lines/0/quantity", "\"-1\"")]
    [InlineData("lines/0/quantity", "\"1.0001\"")]
    [InlineData("lines/0/unit_net_price", "\"-1.00\"")]
    [InlineData("lines/0/unit_net_price", "\"10.001\"")]
    [InlineData("lines/0/unit_net_price", "10.00")]
    [InlineData("lines/0/unit_net_price", "\"NaN\"")]
    [InlineData("lines/0/unit_net_price", "\"1e1\"")]
    [InlineData("lines/0/unit_net_price", "\"10,00\"")]
    [InlineData("lines/0/quantity", null)]
    [InlineData("lines/0/unknown_property", "true")]
    [InlineData("supplier", "[]")]
    [InlineData("issue_date", "\"2026-02-30\"")]
    [InlineData("evidence/0/document_id", "\"OTHER\"")]
    [InlineData("evidence/0/field_path", "\"/lines/5/quantity\"")]
    public async Task Should_reject_without_writes_when_fact_input_is_invalid(
        string field,
        string? value
    )
    {
        var scenario = Baseline();
        using var environment = new CaseTestEnvironment();
        await using var app = environment.Start();
        using var page = app.CreateClient();
        scenario.Prepare(environment, app);
        await using var mcp = await ConnectMcp(app);
        var baseline = Payload(await Register(mcp, RawFacts(scenario.Facts), Source()));
        AssertOutcome(baseline["result"]!, "clear", "D", ["L"], []);
        var originalHistory = Payload(await Call(mcp, "get_case_history"));
        var invalid = RawFacts(scenario.Facts);
        var parent = invalid.AsObject();
        string[] parts = field.Split('/');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var node = parent[parts[i]]!;
            parent = node is JsonArray array
                ? array[
                    int.Parse(parts[++i], System.Globalization.CultureInfo.InvariantCulture)
                ]!.AsObject()
                : node.AsObject();
        }
        if (value is null)
            Assert.True(parent.Remove(parts[^1]));
        else
            parent[parts[^1]] = JsonNode.Parse(value);

        var rejection = Payload(await Register(mcp, invalid, Source()), isError: true);
        Assert.Equal("error", Assert.Single(rejection.AsObject()).Key);
        Assert.Equal("VALIDATION_ERROR", Text(rejection["error"]!, "code"));
        Assert.Contains(rejection["error"]!["details"]!.AsArray(), detail =>
            Text(detail!, "path") == $"/facts/{field}"
            && !string.IsNullOrWhiteSpace(Text(detail!, "message"))
        );
        var saved = await Read(page, $"/api/cases/{CaseTestEnvironment.ReviewerCaseId}");
        Assert.Equal(2, saved["current_revision"]!.GetValue<int>());
        AssertSameJson(baseline["result"], saved["revision"]!["result"]);
        AssertSameJson(originalHistory, Payload(await Call(mcp, "get_case_history")));
    }

    [Fact]
    public async Task Should_keep_history_when_an_mcp_amount_exceeds_the_supported_limit()
    {
        var scenario = Baseline();
        using var environment = new CaseTestEnvironment();
        await using var app = environment.Start();
        scenario.Prepare(environment, app);
        await using var mcp = await ConnectMcp(app);
        var baseline = Payload(await Register(mcp, RawFacts(scenario.Facts), Source()));
        AssertOutcome(baseline["result"]!, "clear", "D", ["L"], []);
        var history = Payload(await Call(mcp, "get_case_history"));

        var facts = RawFacts(scenario.Facts);
        facts["lines"]![0]!["unit_net_price"] = "1000000000.01";
        var error = await Register(mcp, facts, Source());

        Assert.True(error.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(error.Content)).Text;
        Assert.Contains("amount or quantity above 1,000,000,000", text, StringComparison.Ordinal);
        Assert.Contains("/facts/lines/0/unit_net_price", text, StringComparison.Ordinal);
        AssertSameJson(history, Payload(await Call(mcp, "get_case_history")));
    }
}
