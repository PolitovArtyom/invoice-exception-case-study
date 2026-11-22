using System.Text.Json.Nodes;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Tests.Support;
using static InvoiceDesk.Tests.Support.AssessmentAssertions;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;
using static InvoiceDesk.Tests.Support.MatchedInvoiceScenario;

namespace InvoiceDesk.Tests.Api;

public sealed class InvoiceDuplicateTests
{
    private const string CasePath = "/api/cases/" + CaseTestEnvironment.ReviewerCaseId;

    [Fact]
    [Trait("H", "H-36")]
    public async Task Should_return_duplicate_when_equal_facts_are_reimported_with_ignored_changes()
    {
        var scenario = Baseline().WithSecondLine();
        scenario = scenario with
        {
            Facts = scenario.Facts with
            {
                DeclaredSubtotal = 20.00m,
                DeclaredTaxTotal = 4.00m,
                DeclaredTotal = 24.00m,
            },
        };
        using var environment = new CaseTestEnvironment();
        await using var app = environment.Start();
        using var page = app.CreateClient();
        scenario.Prepare(environment, app, "D2");
        await using var mcp = await ConnectMcp(app);
        var original = Payload(await Register(mcp, RawFacts(scenario.Facts), Source()));
        AssertOutcome(original["result"]!, "clear", "D", ["L", "L2"], []);
        AssertTotals(original["result"]!, "20.00", "4.00", "24.00");
        AssertLine(original["result"]!, "L", "OL", "deterministic", "10.00", "2.00");
        AssertLine(original["result"]!, "L2", "OL2", "deterministic", "10.00", "2.00");

        var resend = scenario.Facts with
        {
            Number = $" {scenario.Facts.Number.ToLowerInvariant()} ",
            DocumentId = "D2",
            PaymentTerms = "Alternate printed terms",
            Evidence = [new("/type", "D2", 2, "INVOICE: copy")],
            Lines =
            [
                scenario.Facts.Lines[1] with
                {
                    LineId = "L9",
                    Description = "Second item",
                },
                scenario.Facts.Lines[0] with
                {
                    LineId = "L8",
                    Description = "First item",
                },
            ],
        };
        var duplicate = Payload(await Register(mcp, RawFacts(resend), Source(second: true)));

        AssertDuplicate(duplicate, "D2", "D", "2", 3);
        var history = Payload(await Call(mcp, "get_case_history")).AsArray();
        Assert.Equal(3, history.Count);
        Assert.Null(history[0]!["assessment_id"]);
        Assert.Null(history[0]!["result"]);
        AssertSameJson(original["result"], history[1]!["result"]);
        Assert.Equal(
            history[1]!["document"]!["reference"]!.GetValue<long>(),
            history[0]!["document"]!["reference"]!.GetValue<long>()
        );
        Assert.NotEqual(
            Text(history[1]!["document"]!, "content_sha256"),
            Text(history[0]!["duplicate_attempt"]!["reading"]!, "content_sha256")
        );
        var saved = await Read(page, CasePath);
        AssertSameJson(original["result"], saved["revision"]!["result"]);
    }

    [Fact]
    [Trait("H", "H-36")]
    public async Task Should_assess_conflict_when_repeated_line_multiplicity_changes()
    {
        var scenario = Baseline().WithSecondLine();
        var orderLines = scenario.CompanyData.PurchaseOrders.Orders[0].Lines;
        scenario = scenario.WithOrderLines(
            orderLines[0],
            orderLines[1],
            orderLines[0] with
            {
                LineId = "OL3",
                Sku = "OTHER",
                Description = "Spare",
            }
        );
        var receipts = scenario.CompanyData.GoodsReceipts;
        scenario = scenario with
        {
            Facts = scenario.Facts with
            {
                Lines =
                [
                    scenario.Facts.Lines[0] with
                    {
                        Sku = null,
                    },
                    scenario.Facts.Lines[1] with
                    {
                        Sku = null,
                    },
                    scenario.Facts.Lines[0] with
                    {
                        LineId = "L3",
                        Sku = "SPARE",
                        Description = "Spare",
                    },
                ],
                MissingFields =
                [
                    new("/lines/0/sku", "not_present_in_document"),
                    new("/lines/1/sku", "not_present_in_document"),
                ],
                DeclaredSubtotal = 30.00m,
                DeclaredTaxTotal = 6.00m,
                DeclaredTotal = 36.00m,
            },
            CompanyData = scenario.CompanyData with
            {
                GoodsReceipts = receipts with
                {
                    Receipts =
                    [
                        .. receipts.Receipts,
                        receipts.Receipts[0] with
                        {
                            ReceiptId = "R3",
                            OrderLineId = "OL3",
                        },
                    ],
                },
            },
        };

        using var environment = new CaseTestEnvironment();
        await using var app = environment.Start();
        using var page = app.CreateClient();
        scenario.Prepare(environment, app, "D2");
        await using var mcp = await ConnectMcp(app);
        var original = Payload(await Register(mcp, RawFacts(scenario.Facts), Source()));
        AssertOutcome(original["result"]!, "clear", "D", ["L", "L2", "L3"], []);
        AssertTotals(original["result"]!, "30.00", "6.00", "36.00");
        AssertLine(original["result"]!, "L", "OL", "deterministic", "10.00", "2.00");
        AssertLine(original["result"]!, "L2", "OL2", "deterministic", "10.00", "2.00");
        AssertLine(original["result"]!, "L3", "OL3", "deterministic", "10.00", "2.00");

        var resend = scenario.Facts with
        {
            Number = $" {scenario.Facts.Number.ToLowerInvariant()} ",
            DocumentId = "D2",
            PaymentTerms = "Alternate printed terms",
            Evidence = [new("/type", "D2", 2, "INVOICE: copy")],
            Lines =
            [
                scenario.Facts.Lines[1] with
                {
                    LineId = "L9",
                    Description = "Second item",
                },
                scenario.Facts.Lines[0] with
                {
                    LineId = "L8",
                    Description = "First item",
                },
                .. scenario
                    .Facts.Lines.Skip(2)
                    .Select(line => line with { LineId = "L7", Description = "Third item" }),
            ],
        };
        var duplicate = Payload(await Register(mcp, RawFacts(resend), Source(second: true)));

        AssertDuplicate(duplicate, "D2", "D", "2", 3);
        var history = Payload(await Call(mcp, "get_case_history")).AsArray();
        Assert.Equal(3, history.Count);
        Assert.Null(history[0]!["assessment_id"]);
        Assert.Null(history[0]!["result"]);
        AssertSameJson(original["result"], history[1]!["result"]);
        Assert.Equal(
            history[1]!["document"]!["reference"]!.GetValue<long>(),
            history[0]!["document"]!["reference"]!.GetValue<long>()
        );
        Assert.NotEqual(
            Text(history[1]!["document"]!, "content_sha256"),
            Text(history[0]!["duplicate_attempt"]!["reading"]!, "content_sha256")
        );
        var saved = await Read(page, CasePath);
        AssertSameJson(original["result"], saved["revision"]!["result"]);

        var changedMultiplicity = resend with
        {
            Lines =
            [
                scenario.Facts.Lines[0],
                scenario.Facts.Lines[1] with
                {
                    Sku = "SPARE",
                },
                scenario.Facts.Lines[2],
            ],
            MissingFields = [new("/lines/0/sku", "not_present_in_document")],
        };
        var conflict = Payload(
            await Register(mcp, RawFacts(changedMultiplicity), Source(second: true))
        )["result"]!;
        AssertOutcome(
            conflict,
            "blocked",
            "D2",
            ["L", "L2", "L3"],
            [new("DOCUMENT_CONFLICT", "D2", "fail", "blocked")],
            disposition: "conflict",
            originalDocumentId: "D"
        );
        AssertTotals(conflict, "30.00", "6.00", "36.00");
        AssertLine(conflict, "L", "OL", "deterministic", "10.00", "2.00");
        AssertLine(conflict, "L2", "OL2", "deterministic", "10.00", "2.00");
        AssertLine(conflict, "L3", "OL3", "deterministic", "10.00", "2.00");
        var afterConflict = await Read(page, CasePath);
        AssertSameJson(conflict, afterConflict["revision"]!["result"]);
    }

    [Fact]
    [Trait("H", "H-37")]
    [Trait("Decision", "A-15")]
    public async Task Should_choose_first_equal_original_when_multiple_records_share_a_key()
    {
        var scenario = Baseline();
        var facts = scenario.Facts;
        var changed = facts with { DueDate = facts.IssueDate.AddDays(60) };
        using var environment = new CaseTestEnvironment();
        await using var app = environment.Start();
        using var page = app.CreateClient();
        scenario.Prepare(environment, app);
        await using var mcp = await ConnectMcp(app);

        var original = Payload(await Register(mcp, RawFacts(facts), Source()))["result"]!;
        AssertOutcome(original, "clear", "D", ["L"], []);
        var conflict = Payload(await Register(mcp, RawFacts(changed), Source()))["result"]!;
        AssertOutcome(
            conflict,
            "blocked",
            "D",
            ["L"],
            [new("DOCUMENT_CONFLICT", "D", "fail", "blocked")],
            disposition: "conflict",
            originalDocumentId: "D"
        );

        var originalResend = Payload(await Register(mcp, RawFacts(facts), Source()));
        AssertDuplicate(originalResend, "D", "D", "2", 4);
        var afterOriginalResend = await Read(page, CasePath);
        Assert.Null(afterOriginalResend["latest_duplicate_revision"]);

        var changedResend = Payload(await Register(mcp, RawFacts(changed), Source()));
        AssertDuplicate(changedResend, "D", "D", "3", 5);

        var afterResends = await Read(page, CasePath);
        Assert.Equal(5, afterResends["current_revision"]!.GetValue<int>());
        AssertSameJson(conflict, afterResends["revision"]!["result"]);
        Assert.Equal(5, afterResends["latest_duplicate_revision"]!.GetValue<int>());
    }

    [Fact]
    [Trait("Decision", "A-15")]
    public async Task Should_choose_first_equal_history_record_when_earlier_history_differs()
    {
        var baseline = Baseline();
        var scenario = baseline.WithCountedInvoice(baseline.Facts, "HISTORY-EQUAL");
        var original = scenario.CompanyData.InvoiceHistory.Documents[0];
        var earlier = original with
        {
            DocumentId = "HISTORY-UNEQUAL",
            NormalizedFacts = original.NormalizedFacts with
            {
                DueDate = original.NormalizedFacts.DueDate!.Value.AddDays(1),
            },
        };
        var history = scenario.CompanyData.InvoiceHistory with
        {
            Documents = [earlier, original, original with { DocumentId = "HISTORY-LATER" }],
        };
        scenario = scenario with
        {
            CompanyData = scenario.CompanyData with { InvoiceHistory = history },
        };

        using var environment = new CaseTestEnvironment();
        await using var app = environment.Start();
        using var page = app.CreateClient();
        scenario.Prepare(environment, app);
        await using var mcp = await ConnectMcp(app);
        var response = Payload(await Register(mcp, RawFacts(scenario.Facts), Source()));
        AssertDuplicate(response, "D", "HISTORY-EQUAL", null, 2);
        var revisions = (await Read(page, CasePath + "/history")).AsArray();
        Assert.Equal(2, revisions.Count);
        Assert.All(revisions, revision => Assert.Null(revision!["assessment_id"]));
    }

    [Fact]
    public async Task Should_keep_current_work_visible_when_its_duplicate_is_submitted()
    {
        using var environment = new CaseTestEnvironment();
        await using var app = environment.Start();
        using var page = app.CreateClient();
        var scenario = Baseline().WithLineAmounts(1m, 10.02m, 0.20m, 10.02m, 2.00m, 12.02m);
        scenario.Prepare(environment, app);
        await using var mcp = await ConnectMcp(app);
        var original = Payload(await Register(mcp, RawFacts(scenario.Facts), Source()));
        var result = original["result"]!;
        AssertOutcome(
            result,
            "needs_review",
            "D",
            ["L"],
            [new("UNIT_PRICE", "L", "fail", "needs_review")]
        );
        AssertTotals(result, "10.02", "2.00", "12.02");
        AssertLine(result, "L", "OL", "deterministic", "10.02", "2.00");

        AssertDuplicate(
            Payload(await Register(mcp, RawFacts(scenario.Facts), Source())),
            "D",
            "D",
            "2",
            3
        );

        var summary = Assert.Single((await Read(page, "/api/cases")).AsArray())!;
        Assert.Equal("needs_attention", Text(summary, "stage"));
        Assert.Equal("needs_review", Text(summary, "decision"));
        var current = await Read(page, CasePath);
        Assert.Equal(3, current["current_revision"]!.GetValue<int>());
        Assert.Equal(3, current["latest_duplicate_revision"]!.GetValue<int>());
        Assert.False(current["inputs_changed"]!.GetValue<bool>());
        AssertSameJson(result, current["revision"]!["result"]);
        AssertSameJson(result, Payload(await Call(mcp, "get_assessment"))["revision"]!["result"]);
        AssertSameJson(result, Payload(await Call(mcp, "assess_invoice"))["result"]);
    }

    [Fact]
    public async Task Should_preserve_duplicate_attempt_and_pdf_reference_when_the_app_restarts()
    {
        using var environment = new CaseTestEnvironment();
        string folder = environment.CopyCase("case-008");
        await using (var app = environment.Start())
        {
            using var page = app.CreateClient();
            Assert.Equal("closed", Text(await Read(page, "/api/cases/case-008"), "stage"));
        }

        await using var restarted = environment.Start();
        using var browser = restarted.CreateClient();
        await using var mcp = await ConnectMcp(restarted);
        var closed = await Read(browser, "/api/cases/case-008");
        var revision = closed["revision"]!;
        Assert.Equal("closed", Text(closed, "stage"));
        Assert.True(closed["original_pdf_available"]!.GetValue<bool>());
        Assert.True(closed["invoice_pdf_available"]!.GetValue<bool>());
        var listed = Assert.Single((await Read(browser, "/api/cases")).AsArray())!;
        Assert.Equal("case-008", Text(listed, "case_id"));
        Assert.Equal("closed", Text(listed, "stage"));
        Assert.Equal("ORIGINAL-008", Text(listed, "original_document_id"));
        Assert.Equal(1, closed["current_revision"]!.GetValue<int>());
        Assert.Equal("ORIGINAL-008", Text(revision["original_invoice"]!, "document_id"));
        Assert.Null(revision["assessment_id"]);
        Assert.Null(revision["result"]);
        Assert.Null(revision["document"]);
        AssertSameJson(
            revision,
            Assert.Single(Payload(await Call(mcp, "get_case_history", "case-008")).AsArray())
        );
        var reading = revision["duplicate_attempt"]!["reading"]!;
        Assert.Equal("DOC-008", Text(reading["facts"]!, "document_id"));
        Assert.Equal(
            "cases/case-008/documents/document-001.pdf",
            Text(reading["source"]!, "relative_path")
        );

        foreach (
            var (endpoint, file) in new[]
            {
                ("original-pdf", "original-001.pdf"),
                ("duplicate-pdf", "document-001.pdf"),
            }
        )
        {
            using var pdf = await browser.GetAsync($"/api/cases/case-008/revisions/1/{endpoint}");
            pdf.EnsureSuccessStatusCode();
            Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
            Assert.Equal(
                await File.ReadAllBytesAsync(Path.Combine(folder, "documents", file)),
                await pdf.Content.ReadAsByteArrayAsync()
            );
        }
    }

    [Fact]
    public async Task Should_show_the_resend_and_its_pdf_when_opening_a_saved_duplicate_revision()
    {
        var scenario = Baseline();
        using var environment = new CaseTestEnvironment();
        await using var app = environment.Start();
        using var page = app.CreateClient();
        scenario.Prepare(environment, app, "D2");
        await using var mcp = await ConnectMcp(app);
        var original = Payload(await Register(mcp, RawFacts(scenario.Facts), Source()));
        var resend = scenario.Facts with
        {
            DocumentId = "D2",
            PaymentTerms = "Alternate printed terms",
            Evidence = [new("/type", "D2", 2, "INVOICE copy")],
        };
        var duplicate = Payload(await Register(mcp, RawFacts(resend), Source(second: true)));
        AssertDuplicate(duplicate, "D2", "D", "2", 3);

        var saved = await Read(page, CasePath + "/revisions/3");
        var revision = saved["revision"]!;
        Assert.Equal("needs_attention", Text(saved, "stage"));
        AssertSameJson(RawFacts(scenario.Facts), revision["document"]!["facts"]);
        Assert.True(revision["is_duplicate_attempt"]!.GetValue<bool>());
        Assert.True(saved["show_duplicate_reading"]!.GetValue<bool>());
        Assert.Null(revision["result"]);
        Assert.Equal("D", Text(revision["original_invoice"]!, "document_id"));
        AssertSameJson(RawFacts(resend), revision["duplicate_attempt"]!["reading"]!["facts"]);
        Assert.True(saved["invoice_pdf_available"]!.GetValue<bool>());
        Assert.True(saved["original_pdf_available"]!.GetValue<bool>());

        string duplicateFile = Path.Combine(environment.ScenarioPath, "document-002.pdf");
        Assert.Equal(
            await File.ReadAllBytesAsync(duplicateFile),
            await page.GetByteArrayAsync(CasePath + "/revisions/3/duplicate-pdf")
        );
        Assert.Equal(
            await File.ReadAllBytesAsync(
                Path.Combine(environment.ScenarioPath, "document-001.pdf")
            ),
            await page.GetByteArrayAsync(CasePath + "/revisions/3/original-pdf")
        );
        File.Delete(duplicateFile);
        var withoutResendPdf = await Read(page, CasePath + "/revisions/3");
        Assert.False(withoutResendPdf["invoice_pdf_available"]!.GetValue<bool>());
        Assert.True(withoutResendPdf["original_pdf_available"]!.GetValue<bool>());

        var current = await Read(page, CasePath);
        Assert.True(current["invoice_pdf_available"]!.GetValue<bool>());
        AssertSameJson(original["result"], current["revision"]!["result"]);
        AssertSameJson(RawFacts(scenario.Facts), current["revision"]!["document"]!["facts"]);
    }
}
