using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Tests.Support;
using ModelContextProtocol.Protocol;
using static InvoiceDesk.Tests.Support.AssessmentAssertions;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;

namespace InvoiceDesk.Tests.Api;

public sealed class CompanyDataRecheckTests
{
    [Fact]
    [Trait("H", "H-41")]
    public async Task Should_replace_full_context_when_new_company_data_is_rechecked()
    {
        var scenario = MatchedInvoiceScenario.Baseline();
        await using var test = await ProposalCase.Start(scenario);
        AssertBaseline(test.Initial);
        Assert.Equal("CTX-BASELINE", Text(test.Initial, "context_version"));

        var unavailable = scenario.CompanyData with
        {
            ContextVersion = "CTX-NO-RECEIPT-DATA",
            GoodsReceipts = new("1.0", ReceiptAvailability.Unavailable, []),
        };
        test.WriteCompanyDataUpdate(InvoiceJson.Write(unavailable));
        var changed = Payload(await Call(test.Mcp, "assess_invoice"));
        var result = changed["result"]!;
        Assert.False(changed["inputs_unchanged"]!.GetValue<bool>());
        Assert.Equal("3", Text(changed, "assessment_id"));
        Assert.Equal(3, result["input_revision"]!.GetValue<int>());
        Assert.Equal("CTX-NO-RECEIPT-DATA", Text(result, "context_version"));
        AssertOutcome(
            result,
            "needs_information",
            "D",
            ["L"],
            [new("QTY_RECEIVED", "L", "unknown")],
            [("goods_receipts", "L")]
        );
        AssertLine(
            result,
            "L",
            "OL",
            "deterministic",
            "10.00",
            "2.00",
            received: null,
            available: null
        );
        AssertTotals(result, "10.00", "2.00", "12.00");
        AssertSameJson(
            JsonNode.Parse(InvoiceJson.Write(unavailable)),
            Payload(await Call(test.Mcp, "get_company_data"))
        );

        var empty = unavailable with
        {
            ContextVersion = "CTX-NO-RECEIPTS",
            GoodsReceipts = new("1.0", ReceiptAvailability.Available, []),
        };
        test.WriteCompanyDataUpdate(InvoiceJson.Write(empty));
        using var response = await Recheck(test.Page, ProposalCase.CaseId);
        response.EnsureSuccessStatusCode();
        var reassessed = (await Body(response))["result"]!;
        Assert.Equal(4, reassessed["input_revision"]!.GetValue<int>());
        Assert.Equal("CTX-NO-RECEIPTS", Text(reassessed, "context_version"));
        AssertOutcome(
            reassessed,
            "blocked",
            "D",
            ["L"],
            [new("QTY_RECEIVED", "L", "fail", "blocked")]
        );
        AssertLine(
            reassessed,
            "L",
            "OL",
            "deterministic",
            "10.00",
            "2.00",
            received: "0",
            available: "0"
        );
        AssertTotals(reassessed, "10.00", "2.00", "12.00");
        Assert.Collection(
            (await test.History()).AsArray(),
            latest => AssertSameJson(reassessed, latest!["result"]),
            unknown => AssertSameJson(result, unknown!["result"]),
            baseline => AssertSameJson(test.Initial, baseline!["result"]),
            loaded => Assert.Null(loaded!["result"])
        );
        var old = await Read(test.Page, ProposalCase.CaseUrl + "/revisions/2");
        AssertSameJson(test.Initial, old["revision"]!["result"]);
        AssertSameJson(
            JsonNode.Parse(InvoiceJson.Write(scenario.CompanyData)),
            old["company_data"]
        );
    }

    [Fact]
    public async Task Should_return_saved_result_when_update_file_is_absent_or_equal()
    {
        var scenario = MatchedInvoiceScenario.Baseline();
        await using var test = await ProposalCase.Start(scenario);
        AssertBaseline(test.Initial);
        var history = await test.History();

        var absent = Payload(await Call(test.Mcp, "assess_invoice"));
        AssertUnchanged(test.Initial, absent);
        var context = JsonNode.Parse(InvoiceJson.Write(scenario.CompanyData))!.AsObject();
        var reordered = new JsonObject(
            context
                .Reverse()
                .Select(property => KeyValuePair.Create(property.Key, property.Value?.DeepClone()))
        );
        reordered["goods_receipts"]!["receipts"]![0]!["accepted_quantity"] = "10.0";
        test.WriteCompanyDataUpdate(
            reordered.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
        );
        using var response = await Recheck(test.Page, ProposalCase.CaseId);
        response.EnsureSuccessStatusCode();
        AssertUnchanged(test.Initial, await Body(response));
        AssertSameJson(history, await test.History());
    }

    [Fact]
    public async Task Should_apply_new_snapshot_when_content_changes_with_the_same_version()
    {
        var baseline = MatchedInvoiceScenario.Baseline();
        var firstReceipt = baseline.CompanyData.GoodsReceipts.Receipts[0];
        var secondReceipt = firstReceipt with { ReceiptId = "R2", AcceptedQuantity = 4m };
        var data = baseline.CompanyData with
        {
            GoodsReceipts = baseline.CompanyData.GoodsReceipts with
            {
                Receipts = [firstReceipt, secondReceipt],
            },
        };
        await using var test = await ProposalCase.Start(baseline with { CompanyData = data });
        AssertReceivedFourteen(test.Initial);
        var reversed = data with
        {
            GoodsReceipts = data.GoodsReceipts with { Receipts = [secondReceipt, firstReceipt] },
        };
        test.WriteCompanyDataUpdate(InvoiceJson.Write(reversed));

        var pageCall = Recheck(test.Page, ProposalCase.CaseId);
        var mcpCall = Call(test.Mcp, "assess_invoice").AsTask();
        await Task.WhenAll(pageCall, mcpCall);
        using var response = await pageCall;
        response.EnsureSuccessStatusCode();
        var pageResult = await Body(response);
        var mcpResult = Payload(await mcpCall);
        Assert.NotEqual(
            pageResult["inputs_unchanged"]!.GetValue<bool>(),
            mcpResult["inputs_unchanged"]!.GetValue<bool>()
        );
        AssertSameJson(pageResult["result"], mcpResult["result"]);
        Assert.Equal("3", Text(pageResult, "assessment_id"));
        Assert.Equal("CTX-BASELINE", Text(pageResult["result"]!, "context_version"));
        AssertReceivedFourteen(pageResult["result"]!);
        AssertSameJson(
            JsonNode.Parse(InvoiceJson.Write(reversed)),
            Payload(await Call(test.Mcp, "get_company_data"))
        );

        var versionOnly = JsonNode.Parse(InvoiceJson.Write(reversed))!;
        versionOnly["context_version"] = "CTX-VERSION-ONLY";
        test.WriteCompanyDataUpdate(versionOnly.ToJsonString());
        var changedVersion = Payload(await Call(test.Mcp, "assess_invoice"));
        Assert.False(changedVersion["inputs_unchanged"]!.GetValue<bool>());
        Assert.Equal("4", Text(changedVersion, "assessment_id"));
        Assert.Equal("CTX-VERSION-ONLY", Text(changedVersion["result"]!, "context_version"));
        AssertReceivedFourteen(changedVersion["result"]!);
        Assert.Equal(4, (await test.History()).AsArray().Count);
    }

    public static TheoryData<string, string, string[]> InvalidUpdates()
    {
        var baseline = JsonNode.Parse(
            InvoiceJson.Write(MatchedInvoiceScenario.Baseline().CompanyData)
        )!;
        var outOfRange = baseline.DeepClone();
        outOfRange["goods_receipts"]!["receipts"]![0]!["accepted_quantity"] = "1000000001";
        baseline["goods_receipts"]!["availability"] = "pending";
        return new()
        {
            { "{", "/context", ["JSON"] },
            { "null", "/context", ["object"] },
            { """{"context_version":"X","context_version":"Y"}""", "/context", ["JSON"] },
            { """{"context_version":"CTX-BROKEN"}""", "/context/goods_receipts", ["required"] },
            {
                outOfRange.ToJsonString(),
                "/context/goods_receipts/receipts/0/accepted_quantity",
                ["Unsupported", "1,000,000,000"]
            },
            {
                baseline.ToJsonString(),
                "/context/goods_receipts/availability",
                ["Allowed values", "available", "unavailable"]
            },
        };
    }

    [Theory]
    [MemberData(nameof(InvalidUpdates))]
    public async Task Should_preserve_state_when_update_file_is_invalid(
        string update,
        string path,
        string[] messages
    )
    {
        var scenario = MatchedInvoiceScenario.Baseline();
        await using var test = await ProposalCase.Start(scenario);
        AssertBaseline(test.Initial);
        var history = await test.History();
        var before = await Read(test.Page, ProposalCase.CaseUrl);
        var data = Payload(await Call(test.Mcp, "get_company_data"));
        test.WriteCompanyDataUpdate(update);

        using var response = await Recheck(test.Page, ProposalCase.CaseId);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var pageError = await Body(response);
        var error = pageError["error"]!;
        Assert.Equal("VALIDATION_ERROR", Text(error, "code"));
        var detail = Assert.Single(error["details"]!.AsArray(), item =>
            Text(item!, "path") == path
        )!;
        Assert.All(messages, message =>
            Assert.Contains(message, Text(detail, "message")!, StringComparison.OrdinalIgnoreCase)
        );
        Assert.Null(pageError["result"]);
        var mcpError = await Call(test.Mcp, "assess_invoice");
        string mcpMessage = Assert.IsType<TextContentBlock>(Assert.Single(mcpError.Content)).Text;
        Assert.True(mcpError.IsError, mcpMessage);
        Assert.Contains(path, mcpMessage, StringComparison.Ordinal);
        Assert.All(messages, message =>
            Assert.Contains(message, mcpMessage, StringComparison.OrdinalIgnoreCase)
        );
        AssertSameJson(history, await test.History());
        AssertSameJson(before, await Read(test.Page, ProposalCase.CaseUrl));
        AssertSameJson(data, Payload(await Call(test.Mcp, "get_company_data")));
    }

    private static void AssertUnchanged(JsonNode expected, JsonNode response)
    {
        Assert.True(response["inputs_unchanged"]!.GetValue<bool>());
        Assert.Equal(expected["input_revision"]!.ToString(), Text(response, "assessment_id"));
        AssertSameJson(expected, response["result"]);
    }

    private static void AssertBaseline(JsonNode result)
    {
        AssertOutcome(result, "clear", "D", ["L"], []);
        AssertLine(result, "L", "OL", "deterministic", "10.00", "2.00");
        AssertTotals(result, "10.00", "2.00", "12.00");
    }

    private static void AssertReceivedFourteen(JsonNode result)
    {
        AssertOutcome(result, "clear", "D", ["L"], []);
        AssertLine(
            result,
            "L",
            "OL",
            "deterministic",
            "10.00",
            "2.00",
            received: "14",
            available: "14"
        );
        AssertTotals(result, "10.00", "2.00", "12.00");
    }
}
