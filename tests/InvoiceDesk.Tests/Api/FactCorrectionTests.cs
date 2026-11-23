using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using InvoiceDesk.Tests.Support;
using static InvoiceDesk.Tests.Api.DiscrepancyAcceptanceTests;
using static InvoiceDesk.Tests.Support.AssessmentAssertions;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;

namespace InvoiceDesk.Tests.Api;

public sealed class FactCorrectionTests
{
    private const string ReadingCase = "case-102";
    private const string ReadingUrl = "/api/cases/" + ReadingCase;
    private const string PricePath = "/lines/1/unit_net_price";
    private const string CorrectionReason = "Verified the reading against the PDF.";

    [Fact]
    public async Task Should_clear_same_document_when_a_person_corrects_the_misread_price()
    {
        using var environment = new CaseTestEnvironment();
        environment.CopyScenarioCase(ReadingCase);
        await using var app = environment.Start();
        using var page = app.CreateClient();
        var before = await Read(page, ReadingUrl);
        var revision = before["revision"]!;
        var original = revision["document"]!;
        var blocked = revision["result"]!;
        AssertOutcome(
            blocked,
            "blocked",
            "DOC-102",
            ["L01", "L02"],
            [
                new("LINE_ARITHMETIC", "L02", "fail", "blocked"),
                new("TOTALS_ARITHMETIC", "DOC-102", "fail", "blocked"),
                new("UNIT_PRICE", "L02", "fail", "needs_review"),
            ],
            matchedOrderId: "ORD-102"
        );
        AssertTotals(blocked, "148.00", "29.60", "177.60");
        AssertLine(
            blocked,
            "L01",
            "OL-102-01",
            "deterministic",
            "20.00",
            "4.00",
            "2",
            "2",
            available: "2",
            remaining: "2"
        );
        AssertLine(
            blocked,
            "L02",
            "OL-102-02",
            "deterministic",
            "128.00",
            "25.60",
            "2",
            "2",
            available: "2",
            remaining: "2"
        );
        Assert.True(before["can_correct"]!.GetValue<bool>());
        Assert.Equal("64.00", Text(original["facts"]!["lines"]![1]!, "unit_net_price"));

        var corrected = await Correct(page, ReadingCase, 1, PricePath, "6.40");
        AssertCorrectedPrice(corrected["result"]!);
        Assert.Equal("2", Text(corrected, "assessment_id"));
        var after = await Read(page, ReadingUrl);
        Assert.Equal("ready_for_accounting", Text(after, "stage"));
        var newRevision = after["revision"]!;
        var sameDocument = newRevision["document"]!;
        AssertSameJson(original["reference"], sameDocument["reference"]);
        AssertSameJson(original["content_sha256"], sameDocument["content_sha256"]);
        AssertSameJson(original["source"], sameDocument["source"]);
        Assert.Equal("DOC-102", Text(sameDocument["facts"]!, "document_id"));
        Assert.Equal("6.40", Text(sameDocument["facts"]!["lines"]![1]!, "unit_net_price"));
        AssertSameJson(original["facts"]!["evidence"], sameDocument["facts"]!["evidence"]);
        var quote = Assert.Single(sameDocument["facts"]!["evidence"]!.AsArray(), item =>
            Text(item!, "field_path") == PricePath
        )!;
        Assert.Equal("6.40", Text(quote, "excerpt"));
        var pdf = await page.GetByteArrayAsync(
            $"{ReadingUrl}/documents/{sameDocument["reference"]}/pdf"
        );
        Assert.Equal(
            Text(original, "content_sha256"),
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(pdf))
        );
        AssertCorrection(newRevision, PricePath, "64.00", "6.40");

        var historical = await Read(page, ReadingUrl + "/revisions/1");
        Assert.True(historical["superseded"]!.GetValue<bool>());
        Assert.False(historical["can_correct"]!.GetValue<bool>());
        AssertSameJson(revision, historical["revision"]);
        var history = (await Read(page, ReadingUrl + "/history")).AsArray();
        Assert.Equal(2, history.Count);
        AssertSameJson(newRevision, history[0]);
        AssertSameJson(revision, history[1]);
        await using var mcp = await ConnectMcp(app);
        var rechecked = Payload(await Call(mcp, "assess_invoice", ReadingCase));
        Assert.True(rechecked["inputs_unchanged"]!.GetValue<bool>());
        AssertSameJson(corrected["result"], rechecked["result"]);

        var resend = Payload(
            await Register(
                mcp,
                MatchedInvoiceScenario.WithCompleteEvidence(sameDocument["facts"]!),
                new { root = "own", relative_path = "cases/case-102/documents/document-001.pdf" },
                ReadingCase
            )
        );
        AssertDuplicate(resend, "DOC-102", "DOC-102", "2", 3);
        var afterResend = await Read(page, ReadingUrl);
        AssertSameJson(newRevision, afterResend["revision"]);
        var repeatedCorrection = await Correct(
            page,
            ReadingCase,
            3,
            "/payment_terms",
            "Payment by bank transfer"
        );
        AssertCorrectedPrice(repeatedCorrection["result"]!);
        var latest = await Read(page, ReadingUrl);
        Assert.Equal(
            "6.40",
            Text(latest["revision"]!["document"]!["facts"]!["lines"]![1]!, "unit_net_price")
        );

        var changedReading = Payload(
            await Register(
                mcp,
                MatchedInvoiceScenario.WithCompleteEvidence(original["facts"]!),
                new { root = "own", relative_path = "cases/case-102/documents/document-001.pdf" },
                ReadingCase
            )
        )["result"]!;
        Assert.Equal(5, changedReading["input_revision"]!.GetValue<int>());
        Assert.Equal("conflict", Text(changedReading, "import_disposition"));
        var equalReading = await Correct(page, ReadingCase, 5, PricePath, "6.40");
        AssertCorrectedPrice(equalReading["result"]!);
        var resolved = await Read(page, ReadingUrl);
        Assert.Equal("ready_for_accounting", Text(resolved, "stage"));
        Assert.Null(resolved["revision"]!["original_invoice"]);
        AssertSameJson(
            original["content_sha256"],
            resolved["revision"]!["document"]!["content_sha256"]
        );
        Assert.NotEqual(
            original["reference"]!.GetValue<long>(),
            resolved["revision"]!["document"]!["reference"]!.GetValue<long>()
        );
    }

    [Fact]
    public async Task Should_block_against_counted_original_when_correcting_the_invoice_number_reveals_a_match()
    {
        const string number = "INV-ORIGINAL";
        const string originalId = "HISTORY-OTHER";
        var scenario = MatchedInvoiceScenario.Baseline();
        var counted = scenario.Facts with { Number = number };
        scenario = (
            scenario with
            {
                Facts = scenario.Facts with { Number = "MISREAD-1" },
            }
        ).WithCountedInvoice(counted, originalId);
        await using var test = await ProposalCase.Start(scenario);
        AssertOutcome(test.Initial, "clear", "D", ["L"], []);
        AssertTotals(test.Initial, "10.00", "2.00", "12.00");
        AssertLine(
            test.Initial,
            "L",
            "OL",
            "deterministic",
            "10.00",
            "2.00",
            previous: "4",
            available: "6",
            remaining: "6"
        );

        var corrected = await Correct(test.Page, ProposalCase.CaseId, 2, "/number", number);
        Assert.Equal("3", Text(corrected, "assessment_id"));
        AssertCountedConflict(corrected["result"]!);
        var saved = await Read(test.Page, ProposalCase.CaseUrl);
        Assert.Equal("needs_attention", Text(saved, "stage"));
        Assert.Equal(originalId, Text(saved["revision"]!["original_invoice"]!, "document_id"));

        var reassessed = test.ReplaceCompanyData(
            scenario.CompanyData with
            {
                ContextVersion = "CTX-UPDATED",
            }
        );
        AssertCountedConflict(reassessed);

        var paymentTerms = await Correct(
            test.Page,
            ProposalCase.CaseId,
            4,
            "/payment_terms",
            "30 calendar days"
        );
        AssertCountedConflict(paymentTerms["result"]!);

        static void AssertCountedConflict(JsonNode result)
        {
            AssertOutcome(
                result,
                "blocked",
                "D",
                ["L"],
                [new("DOCUMENT_CONFLICT", "D", "fail", "blocked")],
                disposition: "conflict",
                originalDocumentId: originalId
            );
            AssertTotals(result, "10.00", "2.00", "12.00");
            AssertLine(result, "L", "OL", "deterministic", "10.00", "2.00");
        }
    }

    [Fact]
    public async Task Should_preserve_acceptance_when_a_correction_leaves_the_value_unchanged()
    {
        using var environment = new CaseTestEnvironment();
        environment.CopyCase(CaseId);
        await using var app = environment.Start();
        using var page = app.CreateClient();
        await using var mcp = await ConnectMcp(app);
        await RegisterPublicInvoice(mcp, CaseId);
        var before = await Accept(page);
        var history = await Read(page, CaseUrl + "/history");

        using var response = await SendCorrection(
            page,
            CaseId,
            2,
            "/payment_terms",
            "Bank transfer within 30 days of invoice date."
        );
        await AssertRejected(response, "/value", "The value is unchanged.");
        AssertSameJson(before, await Read(page, CaseUrl));
        AssertSameJson(history, await Read(page, CaseUrl + "/history"));
    }

    [Fact]
    public async Task Should_reject_a_missing_reason_when_the_corrected_value_is_present()
    {
        using var environment = new CaseTestEnvironment();
        environment.CopyScenarioCase(ReadingCase);
        await using var app = environment.Start();
        using var page = app.CreateClient();
        var before = await Read(page, ReadingUrl);
        var history = await Read(page, ReadingUrl + "/history");

        using var response = await SendCorrection(
            page,
            ReadingCase,
            1,
            "/due_date",
            "2026-12-04",
            "illegible"
        );
        await AssertRejected(
            response,
            "/missing_reason",
            "Remove the missing reason or clear the value."
        );
        AssertSameJson(before, await Read(page, ReadingUrl));
        AssertSameJson(history, await Read(page, ReadingUrl + "/history"));
    }

    [Fact]
    public async Task Should_preserve_reassessment_classification_when_only_payment_terms_are_corrected()
    {
        var scenario = MatchedInvoiceScenario.Baseline();
        await using var test = await ProposalCase.Start(scenario);
        var view = await Read(test.Page, ProposalCase.CaseUrl);
        string contentSha256 = Text(view["revision"]!["document"]!, "content_sha256")!;
        var counted = scenario.WithCountedInvoice(scenario.Facts, "D").CompanyData;
        var reassessed = test.ReplaceCompanyData(
            counted with
            {
                InvoiceHistory = counted.InvoiceHistory with
                {
                    Documents =
                    [
                        counted.InvoiceHistory.Documents[0] with
                        {
                            ContentSha256 = contentSha256,
                        },
                    ],
                },
            }
        );
        AssertClear(reassessed);

        var corrected = await Correct(
            test.Page,
            ProposalCase.CaseId,
            3,
            "/payment_terms",
            "Bank transfer within 30 days"
        );
        Assert.Equal("4", Text(corrected, "assessment_id"));
        var saved = await Read(test.Page, ProposalCase.CaseUrl);
        AssertSameJson(corrected["result"], saved["revision"]!["result"]);
        AssertClear(saved["revision"]!["result"]!);
        AssertSameJson(reassessed["checks"], saved["revision"]!["result"]!["checks"]);
        Assert.Equal(
            "Bank transfer within 30 days",
            Text(saved["revision"]!["document"]!["facts"]!, "payment_terms")
        );

        static void AssertClear(JsonNode result)
        {
            AssertOutcome(result, "clear", "D", ["L"], []);
            AssertTotals(result, "10.00", "2.00", "12.00");
            AssertLine(result, "L", "OL", "deterministic", "10.00", "2.00");
        }
    }

    [Fact]
    public async Task Should_update_missing_reason_when_a_nullable_fact_is_corrected()
    {
        using var environment = new CaseTestEnvironment();
        environment.CopyScenarioCase(ReadingCase);
        await using var app = environment.Start();
        using var page = app.CreateClient();
        await Correct(page, ReadingCase, 1, PricePath, "6.40");
        var before = await Read(page, ReadingUrl);
        const string dueDate = "/due_date";
        using var rejected = await SendCorrection(page, ReadingCase, 2, dueDate, null);
        await AssertRejected(rejected, "/missing_reason");
        AssertSameJson(before, await Read(page, ReadingUrl));

        var withMissing = await Correct(page, ReadingCase, 2, dueDate, null, "illegible");
        AssertCorrectedPrice(withMissing["result"]!);
        var missing = await Read(page, ReadingUrl);
        var facts = missing["revision"]!["document"]!["facts"]!;
        Assert.Null(facts["due_date"]);
        var reason = Assert.Single(facts["missing_fields"]!.AsArray())!;
        Assert.Equal(dueDate, Text(reason, "field_path"));
        Assert.Equal("illegible", Text(reason, "reason"));
        AssertCorrection(missing["revision"]!, dueDate, "2026-12-03", null);
        var originalEvidence = before["revision"]!["document"]!["facts"]!["evidence"];
        AssertSameJson(originalEvidence, facts["evidence"]);

        var updated = await Correct(page, ReadingCase, 3, dueDate, null, "not_present_in_document");
        AssertCorrectedPrice(updated["result"]!);
        var updatedView = await Read(page, ReadingUrl);
        var updatedReason = Assert.Single(
            updatedView["revision"]!["document"]!["facts"]!["missing_fields"]!.AsArray()
        )!;
        Assert.Equal("not_present_in_document", Text(updatedReason, "reason"));

        var restored = await Correct(page, ReadingCase, 4, dueDate, "2026-12-03");
        AssertCorrectedPrice(restored["result"]!);
        var latest = await Read(page, ReadingUrl);
        var restoredFacts = latest["revision"]!["document"]!["facts"]!;
        Assert.Equal("2026-12-03", Text(restoredFacts, "due_date"));
        Assert.Empty(restoredFacts["missing_fields"]!.AsArray());
        AssertSameJson(originalEvidence, restoredFacts["evidence"]);
        AssertCorrection(latest["revision"]!, dueDate, null, "2026-12-03");
        AssertSameJson(
            missing["revision"],
            (await Read(page, ReadingUrl + "/revisions/3"))["revision"]
        );
    }

    [Theory]
    [InlineData(
        1,
        PricePath,
        "6.40001",
        CorrectionReason,
        "/facts/lines/1/unit_net_price",
        "Use a number with two decimal places, e.g. 6.40."
    )]
    [InlineData(
        1,
        "/currency",
        "EUR\n",
        CorrectionReason,
        "/facts/currency",
        "Use a three-letter currency code, e.g. EUR."
    )]
    [InlineData(
        1,
        "/supplier/country",
        "DE\n",
        CorrectionReason,
        "/facts/supplier/country",
        "Use a two-letter country code, e.g. DE."
    )]
    [InlineData(
        1,
        "/buyer/country",
        "RS\n",
        CorrectionReason,
        "/facts/buyer/country",
        "Use a two-letter country code, e.g. DE."
    )]
    [InlineData(1, "/issue_date", "2026-02-30", CorrectionReason, "/facts/issue_date")]
    [InlineData(1, "/document_id", "DOC-ANOTHER", CorrectionReason, "/field_path")]
    [InlineData(1, "/lines/1/line_id", "L03", CorrectionReason, "/field_path")]
    [InlineData(1, "/evidence/0/excerpt", "altered", CorrectionReason, "/field_path")]
    [InlineData(1, "/context/purchase_orders", "altered", CorrectionReason, "/field_path")]
    [InlineData(1, "/schema_version", "2.0", CorrectionReason, "/field_path")]
    [InlineData(1, PricePath, "6.40", " ", "/reason")]
    [InlineData(0, PricePath, "6.40", CorrectionReason, "/expected_revision")]
    public async Task Should_preserve_history_when_correction_is_invalid_or_targets_protected_data(
        int revision,
        string fieldPath,
        string value,
        string reason,
        string errorPath,
        string? errorMessage = null
    )
    {
        using var environment = new CaseTestEnvironment();
        environment.CopyScenarioCase(ReadingCase);
        await using var app = environment.Start();
        using var page = app.CreateClient();
        var before = await Read(page, ReadingUrl);
        var history = await Read(page, ReadingUrl + "/history");

        using var response = await SendCorrection(
            page,
            ReadingCase,
            revision,
            fieldPath,
            value,
            reason: reason
        );
        await AssertRejected(response, errorPath, errorMessage);
        AssertSameJson(before, await Read(page, ReadingUrl));
        AssertSameJson(history, await Read(page, ReadingUrl + "/history"));
    }

    [Fact]
    public async Task Should_preserve_human_resolution_when_the_app_restarts()
    {
        using var environment = new CaseTestEnvironment();
        environment.CopyScenarioCase(ReadingCase);
        environment.CopyCase(CaseId);
        environment.AddReviewerCase();
        string[] cases = [ReadingCase, CaseId, CaseTestEnvironment.ReviewerCaseId];
        var saved = new Dictionary<string, (JsonNode View, JsonNode History)>();
        await using (var app = environment.Start())
        {
            using var page = app.CreateClient();
            await using var mcp = await ConnectMcp(app);
            Assert.Equal(
                "fixture",
                Text((await Read(page, ReadingUrl))["revision"]!["document"]!, "reading_origin")
            );
            AssertCorrectedPrice(
                (await Correct(page, ReadingCase, 1, PricePath, "6.40"))["result"]!
            );
            await RegisterPublicInvoice(mcp, CaseId);
            AssertAcceptance((await Accept(page))["acceptance"], "2");
            foreach (string id in cases)
                saved[id] = (
                    await Read(page, $"/api/cases/{id}"),
                    await Read(page, $"/api/cases/{id}/history")
                );
        }

        await using var restarted = environment.Start();
        using var browser = restarted.CreateClient();
        foreach (var (id, (view, history)) in saved)
        {
            AssertSameJson(view, await Read(browser, $"/api/cases/{id}"));
            AssertSameJson(history, await Read(browser, $"/api/cases/{id}/history"));
        }

        var unread = await Read(browser, $"/api/cases/{CaseTestEnvironment.ReviewerCaseId}");
        Assert.Equal("not_read", Text(unread, "stage"));
        Assert.Null(unread["revision"]!["document"]);
        Assert.Equal(1, unread["current_revision"]!.GetValue<int>());
    }

    internal static async Task<JsonNode> Correct(
        HttpClient page,
        string caseId,
        int revision,
        string path,
        string? value,
        string? missingReason = null
    )
    {
        using var response = await SendCorrection(
            page,
            caseId,
            revision,
            path,
            value,
            missingReason
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await Body(response);
    }

    private static Task<HttpResponseMessage> SendCorrection(
        HttpClient page,
        string caseId,
        int revision,
        string path,
        string? value,
        string? missingReason = null,
        string reason = CorrectionReason
    ) =>
        page.PostAsJsonAsync(
            $"/api/cases/{caseId}/fact-corrections",
            new
            {
                expected_revision = revision,
                field_path = path,
                value,
                missing_reason = missingReason,
                person = Person,
                reason,
            }
        );

    private static void AssertCorrectedPrice(JsonNode result)
    {
        AssertOutcome(result, "clear", "DOC-102", ["L01", "L02"], [], matchedOrderId: "ORD-102");
        AssertCorrectedAmounts(result);
    }

    private static void AssertCorrectedAmounts(JsonNode result)
    {
        AssertTotals(result, "32.80", "6.56", "39.36");
        AssertLine(
            result,
            "L01",
            "OL-102-01",
            "deterministic",
            "20.00",
            "4.00",
            "2",
            "2",
            available: "2",
            remaining: "2"
        );
        AssertLine(
            result,
            "L02",
            "OL-102-02",
            "deterministic",
            "12.80",
            "2.56",
            "2",
            "2",
            available: "2",
            remaining: "2"
        );
    }

    private static void AssertCorrection(
        JsonNode revision,
        string path,
        string? previous,
        string? value
    )
    {
        var action = revision["action"]!;
        Assert.Equal("correct_fact", Text(action, "kind"));
        Assert.Equal("human", Text(action, "actor"));
        var correction = revision["correction"]!;
        Assert.Equal(path, Text(correction, "field_path"));
        Assert.Equal(previous, Text(correction, "previous_value"));
        Assert.Equal(value, Text(correction, "value"));
        Assert.Equal(Person, Text(correction, "person"));
        Assert.Equal(CorrectionReason, Text(correction, "reason"));
    }
}
