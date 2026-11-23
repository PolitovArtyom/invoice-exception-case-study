using System.Text.Json.Nodes;
using InvoiceDesk.Core.Validation;
using InvoiceDesk.Tests.Support;
using static InvoiceDesk.Tests.Support.AssessmentAssertions;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;
using RunnerAdapter = InvoiceDesk.Adapter.Adapter;

namespace InvoiceDesk.Tests.Scenarios;

public sealed class AdapterReassessmentTests
{
    [Theory]
    [InlineData("ORIGINAL-008")]
    [InlineData("DOC-008")]
    public async Task Should_block_against_counted_original_when_context_only_step_restores_supplier(
        string originalDocumentId
    )
    {
        using var state = new TemporaryDirectory();
        var firstStep = PublicCases.ReadFirstStep("case-008");
        var counted = firstStep["context"]!["invoice_history"]!["documents"]![0]!;
        counted["document_id"] = originalDocumentId;
        counted["content_sha256"] = firstStep["document"]!["content_sha256"]!.DeepClone();
        ImportWithoutSupplier(firstStep, state.Path);

        var refreshed = ContextOnly(firstStep, 2, "CTX-008-SUPPLIER-RESTORED");
        var assessment = Submit(refreshed, state.Path);
        AssertOutcome(
            assessment,
            "blocked",
            "DOC-008",
            ["L01", "L02", "L03"],
            [new("DOCUMENT_CONFLICT", "DOC-008", "fail", "blocked")],
            matchedOrderId: "ORD-008",
            disposition: "conflict",
            originalDocumentId: originalDocumentId
        );
        var saved = await AssertSavedAssessment(
            state.Path,
            "case-008",
            assessment,
            2,
            duplicate: false
        );
        Assert.Null(saved["revision"]!["original_invoice"]!["document_reference"]);
    }

    [Fact]
    public async Task Should_clear_when_reassessment_finds_earlier_copies_of_the_same_document()
    {
        using var state = new TemporaryDirectory();
        var step = PublicCases.ReadFirstStep("case-008");
        step["context"]!["invoice_history"]!["documents"] = new JsonArray();
        ImportWithoutSupplier(step, state.Path);

        step["step"] = 2;
        ImportWithoutSupplier(step, state.Path);

        var refreshed = ContextOnly(step, 3, "CTX-008-SUPPLIER-RESTORED");
        var assessment = Submit(refreshed, state.Path);
        AssertOutcome(
            assessment,
            "clear",
            "DOC-008",
            ["L01", "L02", "L03"],
            [],
            matchedOrderId: "ORD-008"
        );
        await AssertSavedAssessment(state.Path, "case-008", assessment, 3, duplicate: false);
    }

    [Fact]
    public async Task Should_keep_invoice_clear_when_rechecked_after_the_same_document_is_counted()
    {
        using var state = new TemporaryDirectory();
        var firstStep = PublicCases.ReadFirstStep("case-008");
        var uncounted = firstStep.DeepClone().AsObject();
        uncounted["context"]!["invoice_history"]!["documents"] = new JsonArray();
        AssertCheck(Submit(uncounted, state.Path), "DOCUMENT_CONFLICT", "pass");

        var equalSnapshot = ContextOnly(uncounted, 2, "CTX-008-v1");
        var sameSnapshotAssessment = Submit(equalSnapshot, state.Path);
        AssertOutcome(
            sameSnapshotAssessment,
            "clear",
            "DOC-008",
            ["L01", "L02", "L03"],
            [],
            matchedOrderId: "ORD-008"
        );

        var counted = ContextOnly(firstStep, 3, "CTX-008-COUNTED");
        var sameDocument = counted["context"]!["invoice_history"]!["documents"]![0]!;
        sameDocument["document_id"] = "DOC-008";
        sameDocument["content_sha256"] = firstStep["document"]!["content_sha256"]!.DeepClone();
        var assessment = Submit(counted, state.Path);
        AssertOutcome(
            assessment,
            "clear",
            "DOC-008",
            ["L01", "L02", "L03"],
            [],
            matchedOrderId: "ORD-008"
        );
        await AssertSavedAssessment(state.Path, "case-008", assessment, 3, duplicate: false);
    }

    [Fact]
    public async Task Should_keep_the_resolved_assessment_when_its_invoice_is_resent()
    {
        using var state = new TemporaryDirectory();
        var original = PublicCases.ReadFirstStep();
        ImportWithoutSupplier(original, state.Path);

        var refreshed = ContextOnly(original, 2, "CTX-001-RESTORED");
        var assessment = Submit(refreshed, state.Path);
        Assert.Equal("clear", Text(assessment, "decision"));
        Assert.Equal(2, assessment["input_revision"]!.GetValue<int>());
        AssertCheck(assessment, "SUPPLIER_IDENTITY", "pass");
        AssertCheck(assessment, "DOCUMENT_CONFLICT", "pass");

        var resend = original.DeepClone().AsObject();
        resend.Remove("context");
        resend["step"] = 3;
        resend["document"]!["content_sha256"] = new string('f', 64);
        AssertDuplicate(Submit(resend, state.Path), "DOC-001", "DOC-001", "2", 3);

        await AssertSavedAssessment(state.Path, "case-001", assessment, 3, duplicate: true);
    }

    [Fact]
    public async Task Should_assess_conflict_when_a_context_fix_arrives_with_a_changed_resend()
    {
        using var state = new TemporaryDirectory();
        var step = PublicCases.ReadFirstStep();
        ImportWithoutSupplier(step, state.Path);

        step["step"] = 2;
        step["context"]!["context_version"] = "CTX-001-RESTORED";
        step["document"]!["content_sha256"] = new string('f', 64);
        step["document"]!["facts"]!["due_date"] = "2026-12-01";
        var conflict = Submit(step, state.Path);

        AssertOutcome(
            conflict,
            "blocked",
            "DOC-001",
            ["L01", "L02", "L03", "L04"],
            [new("DOCUMENT_CONFLICT", "DOC-001", "fail", "blocked")],
            matchedOrderId: "ORD-001",
            disposition: "conflict",
            originalDocumentId: "DOC-001"
        );
        Assert.Equal(2, conflict["input_revision"]!.GetValue<int>());
        await AssertSavedAssessment(state.Path, "case-001", conflict, 2, duplicate: false);
    }

    [Fact]
    public async Task Should_return_duplicate_when_a_context_fix_arrives_with_an_equal_resend()
    {
        using var state = new TemporaryDirectory();
        var step = PublicCases.ReadFirstStep();
        var original = ImportWithoutSupplier(step, state.Path);

        step["step"] = 2;
        step["context"]!["context_version"] = "CTX-001-RESTORED";
        step["document"]!["content_sha256"] = new string('f', 64);

        AssertDuplicate(Submit(step, state.Path), "DOC-001", "DOC-001", "1", 2);
        await AssertSavedAssessment(state.Path, "case-001", original, 2, duplicate: true);
    }

    [Fact]
    public void Should_name_the_first_history_original_when_history_and_an_earlier_import_both_differ()
    {
        using var state = new TemporaryDirectory();
        var step = PublicCases.ReadFirstStep("case-009");
        var earlier = step.DeepClone().AsObject();
        earlier["context"]!["invoice_history"]!["documents"] = new JsonArray();
        earlier["document"]!["facts"]!["due_date"] = "2026-12-01";
        Assert.Equal("clear", Text(Submit(earlier, state.Path), "decision"));

        step["step"] = 2;
        var conflict = Submit(step, state.Path);
        AssertOutcome(
            conflict,
            "blocked",
            "DOC-009",
            ["L01", "L02"],
            [new("DOCUMENT_CONFLICT", "DOC-009", "fail", "blocked")],
            matchedOrderId: "ORD-009",
            disposition: "conflict",
            originalDocumentId: "ORIGINAL-009"
        );
    }

    [Fact]
    public async Task Should_assess_retained_inputs_when_a_combined_duplicate_has_no_update_file()
    {
        const string caseId = "case-002";
        const string caseUrl = "/api/cases/" + caseId;
        using var state = new TemporaryDirectory();
        var step = PublicCases.ReadFirstStep(caseId);
        var initial = Submit(step, state.Path);
        AssertOutcome(initial, "clear", "DOC-002", ["L01"], [], matchedOrderId: "ORD-002");
        AssertLine(
            initial,
            "L01",
            "OL-002-01",
            "deterministic",
            "750.00",
            "150.00",
            ordered: "100",
            received: "60",
            available: "60",
            remaining: "100"
        );
        AssertTotals(initial, "750.00", "150.00", "900.00");

        var context = step["context"]!;
        step["step"] = 2;
        context["context_version"] = "CTX-002-EMPTY";
        context["goods_receipts"]!["receipts"] = new JsonArray();
        AssertDuplicate(Submit(step, state.Path), "DOC-002", "DOC-002", "1", 2);

        using var environment = new CaseTestEnvironment();
        string databasePath = Path.Combine(state.Path, "state.sqlite");
        await using var app = environment.Start(databasePath);
        using var page = app.CreateClient();
        var before = await Read(page, caseUrl);
        Assert.True(before["inputs_changed"]!.GetValue<bool>());
        AssertSameJson(initial, before["revision"]!["result"]);

        using var response = await Recheck(page, caseId);
        response.EnsureSuccessStatusCode();
        var rechecked = await Body(response);
        Assert.False(rechecked["inputs_unchanged"]!.GetValue<bool>());
        Assert.Equal("3", Text(rechecked, "assessment_id"));
        var result = rechecked["result"]!;
        Assert.Equal("CTX-002-EMPTY", Text(result, "context_version"));
        Assert.Equal(3, result["input_revision"]!.GetValue<int>());
        AssertOutcome(
            result,
            "blocked",
            "DOC-002",
            ["L01"],
            [new("QTY_RECEIVED", "L01", "fail", "blocked")],
            matchedOrderId: "ORD-002"
        );
        AssertLine(
            result,
            "L01",
            "OL-002-01",
            "deterministic",
            "750.00",
            "150.00",
            ordered: "100",
            received: "0",
            available: "0",
            remaining: "100"
        );
        AssertTotals(result, "750.00", "150.00", "900.00");

        var latest = await Read(page, caseUrl);
        Assert.False(latest["inputs_changed"]!.GetValue<bool>());
        AssertSameJson(context, latest["company_data"]);
        AssertSameJson(before["revision"]!["document"], latest["revision"]!["document"]);
        await using var mcp = await ConnectMcp(app);
        var repeated = Payload(await Call(mcp, "assess_invoice", caseId));
        Assert.True(repeated["inputs_unchanged"]!.GetValue<bool>());
        AssertSameJson(result, repeated["result"]);
        Assert.Collection(
            (await Read(page, caseUrl + "/history")).AsArray(),
            assessment =>
            {
                AssertSameJson(result, assessment!["result"]);
                Assert.Equal("human", Text(assessment["action"]!, "actor"));
                Assert.False(assessment["action"]!["company_data_submitted"]!.GetValue<bool>());
            },
            duplicate =>
            {
                Assert.Null(duplicate!["result"]);
                Assert.NotNull(duplicate["duplicate_attempt"]);
            },
            assessment => AssertSameJson(initial, assessment!["result"])
        );
    }

    private static JsonNode ImportWithoutSupplier(JsonObject original, string statePath)
    {
        var blocked = original.DeepClone().AsObject();
        blocked["context"]!["suppliers"]!["suppliers"] = new JsonArray();
        var result = Submit(blocked, statePath);
        Assert.Equal("blocked", Text(result, "decision"));
        AssertCheck(result, "SUPPLIER_IDENTITY", "fail");
        AssertCheck(result, "DOCUMENT_CONFLICT", "unknown");
        return result;
    }

    [Theory]
    [InlineData("DOC-008")]
    [InlineData("ORIGINAL-008")]
    public async Task Should_assess_received_quantities_when_counted_history_matches_the_active_invoice(
        string historyId
    )
    {
        using var state = new TemporaryDirectory();
        var original = PublicCases.ReadFirstStep("case-008");
        var uncounted = original.DeepClone().AsObject();
        var firstHistory = uncounted["context"]!["invoice_history"]!;
        firstHistory["documents"] = new JsonArray();
        var first = Submit(uncounted, state.Path);
        Assert.Equal("clear", Text(first, "decision"));

        var refreshed = ContextOnly(original, 2, "CTX-008-RECEIPTS");
        var context = refreshed["context"]!;
        context["goods_receipts"]!["receipts"] = new JsonArray();
        var counted = context["invoice_history"]!["documents"]![0]!;
        counted["document_id"] = historyId;
        counted["content_sha256"] = original["document"]!["content_sha256"]!.DeepClone();
        bool sameDocument = historyId == "DOC-008";

        var assessment = Submit(refreshed, state.Path);
        ExpectedCheck[] conflict = sameDocument
            ? []
            : [new("DOCUMENT_CONFLICT", "DOC-008", "fail", "blocked")];
        AssertOutcome(
            assessment,
            "blocked",
            "DOC-008",
            ["L01", "L02", "L03"],
            [
                new("QTY_RECEIVED", "L01", "fail", "blocked"),
                new("QTY_RECEIVED", "L02", "fail", "blocked"),
                new("QTY_RECEIVED", "L03", "fail", "blocked"),
                .. conflict,
            ],
            matchedOrderId: "ORD-008",
            disposition: sameDocument ? "new" : "conflict",
            originalDocumentId: sameDocument ? null : historyId
        );
        AssertTotals(assessment, "1614.00", "322.80", "1936.80");
        Assert.All(assessment["lines"]!.AsArray(), line =>
        {
            Assert.Equal("0", Text(line!, "previously_invoiced"));
            Assert.Equal("0", Text(line!, "received"));
            Assert.Equal("0", Text(line!, "available_to_invoice"));
        });
        await AssertSavedAssessment(state.Path, "case-008", assessment, 2, duplicate: false);
    }

    [Fact]
    public async Task Should_assess_a_conflict_when_counted_history_reuses_the_document_id_for_a_different_version()
    {
        using var state = new TemporaryDirectory();
        var original = PublicCases.ReadFirstStep("case-008");
        var uncounted = original.DeepClone().AsObject();
        uncounted["context"]!["invoice_history"]!["documents"] = new JsonArray();
        Assert.Equal("clear", Text(Submit(uncounted, state.Path), "decision"));

        var refreshed = ContextOnly(original, 2, "CTX-008-EARLIER-VERSION");
        var otherVersion = refreshed["context"]!["invoice_history"]!["documents"]![0]!;
        otherVersion["document_id"] = "DOC-008";
        otherVersion["normalized_facts"]!["due_date"] = "2026-12-31";

        var assessment = Submit(refreshed, state.Path);
        AssertOutcome(
            assessment,
            "blocked",
            "DOC-008",
            ["L01", "L02", "L03"],
            [new("DOCUMENT_CONFLICT", "DOC-008", "fail", "blocked")],
            matchedOrderId: "ORD-008",
            disposition: "conflict",
            originalDocumentId: "DOC-008"
        );
        Assert.All(assessment["lines"]!.AsArray(), line =>
            Assert.Equal("0", Text(line!, "previously_invoiced"))
        );
        await AssertSavedAssessment(state.Path, "case-008", assessment, 2, duplicate: false);
    }

    [Fact]
    public async Task Should_reference_the_local_assessment_when_history_wins_for_the_same_version()
    {
        using var state = new TemporaryDirectory();
        var step = PublicCases.ReadFirstStep("case-008");
        var counted = step["context"]!["invoice_history"]!["documents"]!.DeepClone();
        step["context"]!["invoice_history"]!["documents"] = new JsonArray();
        step["document"]!["facts"]!["document_id"] = "ORIGINAL-008";
        foreach (var evidence in step["document"]!["facts"]!["evidence"]!.AsArray())
            evidence!["document_id"] = "ORIGINAL-008";
        step["document"]!["content_sha256"] = counted[0]!["content_sha256"]!.DeepClone();
        var assessment = Submit(step, state.Path);
        Assert.Equal("clear", Text(assessment, "decision"));

        step["step"] = 2;
        step["context"]!["invoice_history"]!["documents"] = counted;
        step["document"]!["facts"]!["document_id"] = "DOC-008";
        foreach (var evidence in step["document"]!["facts"]!["evidence"]!.AsArray())
            evidence!["document_id"] = "DOC-008";
        step["document"]!["content_sha256"] = new string('f', 64);
        AssertDuplicate(Submit(step, state.Path), "DOC-008", "ORIGINAL-008", "1", 2);
        await AssertSavedAssessment(state.Path, "case-008", assessment, 2, duplicate: true);
    }

    private static JsonObject ContextOnly(JsonObject original, int number, string version)
    {
        var step = original.DeepClone().AsObject();
        step["step"] = number;
        step.Remove("document");
        step["context"]!["context_version"] = version;
        return step;
    }

    private static JsonNode Submit(JsonObject step, string statePath)
    {
        using var stdin = new StringReader(step.ToJsonString());
        using var stdout = new StringWriter();
        Assert.Equal(0, RunnerAdapter.Run(stdin, stdout, statePath));
        var result = JsonNode.Parse(stdout.ToString())!;
        AssignmentSchemas.Instance.Validate("step-output", result);
        return result;
    }

    private static void AssertCheck(JsonNode result, string code, string status)
    {
        var check = Assert.Single(result["checks"]!.AsArray(), item => Text(item!, "code") == code);
        Assert.Equal(status, Text(check!, "status"));
    }

    private static void AssertDuplicate(
        JsonNode result,
        string document,
        string original,
        string assessment,
        int revision
    )
    {
        Assert.Equal("duplicate", Text(result, "import_disposition"));
        Assert.Equal(document, Text(result, "document_id"));
        Assert.Equal(original, Text(result, "original_document_id"));
        Assert.Equal(assessment, Text(result, "existing_assessment_ref"));
        Assert.Equal(revision, result["input_revision"]!.GetValue<int>());
        Assert.Null(result["decision"]);
        Assert.Null(result["totals"]);
        Assert.Null(result["matched_order_id"]);
        Assert.Null(result["order_match_source"]);
        Assert.Empty(result["lines"]!.AsArray());
        Assert.Empty(result["checks"]!.AsArray());
        Assert.Empty(result["questions"]!.AsArray());
    }

    private static async Task<JsonNode> AssertSavedAssessment(
        string statePath,
        string caseId,
        JsonNode assessment,
        int newestRevision,
        bool duplicate
    )
    {
        using var environment = new CaseTestEnvironment();
        await using var app = environment.Start(Path.Combine(statePath, "state.sqlite"));
        using var page = app.CreateClient();
        var saved = await Read(page, $"/api/cases/{caseId}");
        Assert.Equal(newestRevision, saved["current_revision"]!.GetValue<int>());
        AssertSameJson(assessment, saved["revision"]!["result"]);
        await using var mcp = await ConnectMcp(app);
        var history = Payload(await Call(mcp, "get_case_history", caseId)).AsArray();
        Assert.Equal(newestRevision, history.Count);
        if (duplicate)
        {
            Assert.Null(history[0]!["result"]);
            Assert.Null(history[0]!["assessment_id"]);
            Assert.NotNull(history[0]!["duplicate_attempt"]);
            Assert.Equal(
                history[1]!["document"]!["reference"]!.GetValue<long>(),
                history[0]!["document"]!["reference"]!.GetValue<long>()
            );
        }
        else
        {
            Assert.NotNull(history[0]!["assessment_id"]);
            Assert.Null(history[0]!["duplicate_attempt"]);
        }
        return saved;
    }
}
