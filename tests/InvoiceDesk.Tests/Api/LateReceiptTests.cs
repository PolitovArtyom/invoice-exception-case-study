using System.Net;
using System.Text.Json.Nodes;
using InvoiceDesk.Tests.Support;
using ModelContextProtocol.Client;
using static InvoiceDesk.Tests.Support.AssessmentAssertions;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;

namespace InvoiceDesk.Tests.Api;

public sealed class LateReceiptTests
{
    private const string CaseId = "case-101";
    private const string CaseUrl = "/api/cases/" + CaseId;

    [Fact]
    [Trait("Case", "101/page")]
    public Task Should_clear_existing_invoice_when_recheck_picks_up_late_receipts() =>
        AssertLateReceipt(
            async (page, _) =>
            {
                using var response = await Recheck(page, CaseId);
                response.EnsureSuccessStatusCode();
                return await Body(response);
            },
            "human"
        );

    [Fact]
    [Trait("Case", "101/mcp")]
    public Task Should_clear_existing_invoice_when_recheck_picks_up_late_receipts_through_mcp() =>
        AssertLateReceipt(
            async (_, mcp) => Payload(await Call(mcp, "assess_invoice", CaseId)),
            "agent"
        );

    [Fact]
    public async Task Should_explain_that_there_is_no_assessment_when_rechecking_a_closed_duplicate()
    {
        const string duplicateCase = "case-008";
        using var environment = new CaseTestEnvironment();
        environment.CopyCase(duplicateCase);
        await using var app = environment.Start();
        using var page = app.CreateClient();
        await using var mcp = await ConnectMcp(app);
        using var response = await Recheck(page, duplicateCase);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var rejection = await Body(response);
        AssertSameJson(
            rejection,
            Payload(await Call(mcp, "assess_invoice", duplicateCase), isError: true)
        );
        var detail = Assert.Single(rejection["error"]!["details"]!.AsArray())!;
        Assert.Equal("/case_id", Text(detail, "path"));
        Assert.Equal(
            "Closed as a duplicate. There is no assessment to re-check.",
            Text(detail, "message")
        );
        Assert.Single((await Read(page, "/api/cases/case-008/history")).AsArray());
    }

    [Fact]
    public async Task Should_show_current_stage_without_counting_history_when_results_change()
    {
        using var environment = new CaseTestEnvironment();
        environment.CopyCase("case-001");
        environment.CopyCase("case-008");
        environment.CopyCase("case-010");
        environment.CopyLateReceiptCase();
        await using var app = environment.Start();
        using var page = app.CreateClient();
        await using var mcp = await ConnectMcp(app);
        var initial = await Read(page, "/api/cases");
        AssertStages(initial, attention: 2, ready: 1, closed: 1);
        var blocked = Row(initial, CaseId);
        Assert.Equal("own", Text(blocked, "origin"));
        Assert.Equal("2026-11-10", Text(blocked, "issue_date"));
        Assert.Equal("needs_attention", Text(blocked, "stage"));
        Assert.Equal("blocked", Text(blocked, "decision"));
        Assert.Equal("QTY_RECEIVED", Text(blocked, "main_issue"));
        Assert.Equal("receiving", Text(blocked, "main_issue_owner"));
        var unread = Row(initial, "case-010");
        Assert.Equal("not_read", Text(unread, "stage"));
        Assert.Equal("needs_attention", Text(unread, "tab"));
        var closed = Row(initial, "case-008");
        Assert.Equal("closed", Text(closed, "stage"));
        Assert.Equal("closed", Text(closed, "tab"));
        Assert.Equal("ORIGINAL-008", Text(closed, "original_document_id"));

        var resent = Payload(
            await Register(
                mcp,
                PublicCases.CleanFacts(),
                new
                {
                    root = "public",
                    relative_path = "cases/case-001/documents/document-001.pdf",
                },
                "case-001"
            )
        );
        AssertDuplicate(resent, "DOC-001", "DOC-001", "1", 2);
        var afterDuplicate = await Read(page, "/api/cases");
        AssertStages(afterDuplicate, attention: 2, ready: 1, closed: 1);
        var retained = Row(afterDuplicate, "case-001");
        Assert.Equal("ready_for_accounting", Text(retained, "stage"));
        Assert.Equal(1, retained["assessed_revision"]!.GetValue<int>());
        Assert.Equal("CTX-001-v1", Text(retained, "context_version"));

        using var response = await Recheck(page, CaseId);
        response.EnsureSuccessStatusCode();
        AssertOwnResult((await Body(response))["result"]!, "clear", [], "60");
        var readyList = await Read(page, "/api/cases");
        AssertStages(readyList, attention: 1, ready: 2, closed: 1);
        var ready = Row(readyList, CaseId);
        Assert.Equal("ready_for_accounting", Text(ready, "stage"));
        Assert.Equal("ready_for_accounting", Text(ready, "tab"));
        Assert.Equal(2, ready["assessed_revision"]!.GetValue<int>());
        Assert.Equal("CTX-101-v2", Text(ready, "context_version"));

        var data = Payload(await Call(mcp, "get_company_data", CaseId));
        Assert.Empty(data["invoice_history"]!["documents"]!.AsArray());
        data["context_version"] = "CTX-101-v3";
        environment.WriteCompanyDataUpdate(CaseId, data.ToJsonString());
        var checkedAgain = Payload(await Call(mcp, "assess_invoice", CaseId));
        AssertOwnResult(checkedAgain["result"]!, "clear", [], "60");
        AssertStages(await Read(page, "/api/cases"), attention: 1, ready: 2, closed: 1);

        var original = await Read(page, CaseUrl + "/revisions/1");
        environment.WriteCompanyDataUpdate(CaseId, original["company_data"]!.ToJsonString());
        var blockedAgain = Payload(await Call(mcp, "assess_invoice", CaseId));
        AssertOwnResult(
            blockedAgain["result"]!,
            "blocked",
            [new("QTY_RECEIVED", "L01", "fail", "blocked")],
            "0"
        );
        AssertStages(await Read(page, "/api/cases"), attention: 2, ready: 1, closed: 1);
    }

    private static async Task AssertLateReceipt(
        Func<HttpClient, McpClient, Task<JsonNode>> recheck,
        string actor
    )
    {
        using var environment = new CaseTestEnvironment();
        environment.CopyLateReceiptCase();
        await using var app = environment.Start();
        using var page = app.CreateClient();
        await using var mcp = await ConnectMcp(app);
        var before = await Read(page, CaseUrl);
        var first = before["revision"]!;
        var document = first["document"]!;
        Assert.Equal("fixture", Text(document, "reading_origin"));
        var issue = Assert.Single(before["issues"]!.AsArray())!;
        Assert.Equal("receiving", Text(issue, "owner"));
        Assert.Equal("CTX-101-v1", Text(first, "context_version"));
        Assert.Equal(1, first["number"]!.GetValue<int>());
        AssertOwnResult(
            first["result"]!,
            "blocked",
            [new("QTY_RECEIVED", "L01", "fail", "blocked")],
            "0"
        );
        string pdfPath = CaseUrl + "/documents/" + document["reference"] + "/pdf";
        byte[] originalPdf = await page.GetByteArrayAsync(pdfPath);
        Assert.NotEmpty(originalPdf);

        var response = await recheck(page, mcp);
        Assert.False(response["inputs_unchanged"]!.GetValue<bool>());
        Assert.Equal("2", Text(response, "assessment_id"));
        var result = response["result"]!;
        AssertOwnResult(result, "clear", [], "60");
        Assert.Equal("CTX-101-v2", Text(result, "context_version"));
        Assert.Equal(2, result["input_revision"]!.GetValue<int>());

        var after = await Read(page, CaseUrl);
        Assert.Equal("ready_for_accounting", Text(after, "stage"));
        var action = after["revision"]!["action"]!;
        Assert.Equal(actor, Text(action, "actor"));
        Assert.True(action["company_data_submitted"]!.GetValue<bool>());
        AssertSameJson(document, after["revision"]!["document"]);
        AssertSameJson(result, after["revision"]!["result"]);
        Assert.Equal(originalPdf, await page.GetByteArrayAsync(pdfPath));
        Assert.Empty(before["company_data"]!["goods_receipts"]!["receipts"]!.AsArray());
        var receipt = Assert.Single(
            after["company_data"]!["goods_receipts"]!["receipts"]!.AsArray()
        )!;
        Assert.Equal("60", Text(receipt, "accepted_quantity"));
        var historical = await Read(page, CaseUrl + "/revisions/1");
        Assert.True(historical["superseded"]!.GetValue<bool>());
        AssertSameJson(first, historical["revision"]);
        AssertSameJson(before["company_data"], historical["company_data"]);
        Assert.Collection(
            (await Read(page, CaseUrl + "/history")).AsArray(),
            latest => AssertSameJson(after["revision"], latest),
            initial => AssertSameJson(first, initial)
        );
    }

    private static void AssertOwnResult(
        JsonNode result,
        string decision,
        ExpectedCheck[] exceptions,
        string received
    )
    {
        AssertOutcome(result, decision, "DOC-101", ["L01"], exceptions, matchedOrderId: "ORD-101");
        AssertLine(
            result,
            "L01",
            "OL-101-01",
            "deterministic",
            "600.00",
            "120.00",
            ordered: "60",
            received: received,
            available: received,
            remaining: "60"
        );
        AssertTotals(result, "600.00", "120.00", "720.00");
    }

    private static void AssertStages(JsonNode list, int attention, int ready, int closed)
    {
        var counts = list["counts"]!;
        Assert.Equal(attention, counts["needs_attention"]!.GetValue<int>());
        Assert.Equal(ready, counts["ready_for_accounting"]!.GetValue<int>());
        Assert.Equal(closed, counts["closed"]!.GetValue<int>());
        Assert.Equal(attention + ready + closed, list["cases"]!.AsArray().Count);
    }

    private static JsonNode Row(JsonNode list, string caseId) =>
        Assert.Single(list["cases"]!.AsArray(), row => Text(row!, "case_id") == caseId)!;
}
