using System.Text.Json.Nodes;
using InvoiceDesk.Core.Validation;
using InvoiceDesk.Tests.Support;
using static InvoiceDesk.Tests.Support.AssessmentAssertions;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;
using static InvoiceDesk.Tests.Support.MatchedInvoiceScenario;

namespace InvoiceDesk.Tests.Api;

public sealed class InvoiceReimportTests
{
    private const string CasePath = "/api/cases/" + CaseTestEnvironment.ReviewerCaseId;
    private const string ApprovedAlternateIban = "DE00SYNTHETIC0000000002";
    private const string UnapprovedIban = "DE00SYNTHETIC0000000003";

    public static TheoryData<string, string[][], string, string, string, string?> ConflictChanges =>
        new()
        {
            {
                "due date",
                [
                    ["/due_date", "2026-11-25", "2026-12-25"],
                ],
                "10.00",
                "12.00",
                "pass",
                null
            },
            {
                "approved IBAN",
                [
                    ["/bank/iban", ApprovedIban, ApprovedAlternateIban],
                ],
                "10.00",
                "12.00",
                "pass",
                null
            },
            {
                "unapproved IBAN",
                [
                    ["/bank/iban", ApprovedIban, UnapprovedIban],
                ],
                "10.00",
                "12.00",
                "fail",
                "blocked"
            },
            {
                "sku",
                [
                    ["/lines/0/sku", "GLOVE", "CHANGED-SKU"],
                ],
                "10.00",
                "12.00",
                "pass",
                null
            },
            {
                "price and declarations",
                [
                    ["/lines/0/unit_net_price", "10.00", "10.01"],
                    ["/lines/0/declared_net_amount", "10.00", "10.01"],
                    ["/declared_subtotal", "10.00", "10.01"],
                    ["/declared_total", "12.00", "12.01"],
                ],
                "10.01",
                "12.01",
                "pass",
                null
            },
        };

    [Theory]
    [Trait("H", "H-37")]
    [MemberData(nameof(ConflictChanges))]
    public async Task Should_preserve_original_and_assess_conflict_when_compared_facts_change(
        string difference,
        string[][] changes,
        string net,
        string total,
        string bankStatus,
        string? bankSeverity
    )
    {
        Assert.NotEmpty(difference);
        var scenario = Baseline();
        var suppliers = scenario.CompanyData.Suppliers;
        var supplier = suppliers.Suppliers[0];
        scenario = scenario with
        {
            CompanyData = scenario.CompanyData with
            {
                Suppliers = suppliers with
                {
                    Suppliers =
                    [
                        supplier with
                        {
                            ApprovedBankAccounts =
                            [
                                .. supplier.ApprovedBankAccounts,
                                new(ApprovedAlternateIban, "SYNBDE00"),
                            ],
                        },
                    ],
                },
            },
        };
        using var environment = new CaseTestEnvironment();
        await using var app = environment.Start();
        using var page = app.CreateClient();
        scenario.Prepare(environment, app);
        await using var mcp = await ConnectMcp(app);
        var original = Payload(await Register(mcp, RawFacts(scenario.Facts), Source()))["result"]!;
        AssertOutcome(original, "clear", "D", ["L"], []);
        AssertTotals(original, "10.00", "2.00", "12.00");
        AssertLine(original, "L", "OL", "deterministic", "10.00", "2.00");

        var changed = RawFacts(scenario.Facts);
        foreach (var change in changes)
        {
            string pointer = change[0];
            int lastSlash = pointer.LastIndexOf('/');
            Assert.True(JsonFields.TryResolve(changed, pointer[..lastSlash], out var parent));
            parent![pointer[(lastSlash + 1)..]] = change[2];
        }

        var conflict = Payload(await Register(mcp, changed, Source()))["result"]!;
        AssertOutcome(
            conflict,
            "blocked",
            "D",
            ["L"],
            [
                new("DOCUMENT_CONFLICT", "D", "fail", "blocked"),
                new("BANK_ACCOUNT", "D", bankStatus, bankSeverity),
            ],
            disposition: "conflict",
            originalDocumentId: "D"
        );
        AssertTotals(conflict, net, "2.00", total);
        AssertLine(conflict, "L", "OL", "deterministic", net, "2.00");

        var saved = await Read(page, CasePath);
        AssertSameJson(conflict, saved["revision"]!["result"]);
        var history = Payload(await Call(mcp, "get_case_history")).AsArray();
        Assert.Equal(3, history.Count);
        AssertSameJson(original, history[1]!["result"]);
        var savedOriginal = Payload(
            await mcp.CallToolAsync(
                "get_assessment",
                new Dictionary<string, object?>
                {
                    ["case_id"] = CaseTestEnvironment.ReviewerCaseId,
                    ["assessment_id"] = "2",
                }
            )
        );
        Assert.True(savedOriginal["inputs_changed"]!.GetValue<bool>());
        AssertSameJson(original, savedOriginal["revision"]!["result"]);
        var originalDocument = savedOriginal["revision"]!["document"]!;
        var currentDocument = saved["revision"]!["document"]!;
        Assert.NotEqual(
            originalDocument["reference"]!.GetValue<long>(),
            currentDocument["reference"]!.GetValue<long>()
        );
        foreach (var change in changes)
        {
            Assert.True(
                JsonFields.TryResolve(originalDocument["facts"], change[0], out var oldField)
            );
            Assert.True(
                JsonFields.TryResolve(currentDocument["facts"], change[0], out var newField)
            );
            Assert.Equal(change[1], oldField!.GetValue<string>());
            Assert.Equal(change[2], newField!.GetValue<string>());
        }
    }

    [Fact]
    [Trait("H", "H-37")]
    public async Task Should_exclude_same_key_history_from_quantities_when_it_causes_a_conflict()
    {
        var scenario = Baseline();
        var counted = scenario.Facts with { DueDate = scenario.Facts.IssueDate.AddDays(60) };
        var conflict = await scenario.WithCountedInvoice(counted, "HISTORY").RegisterAndRead();
        AssertOutcome(
            conflict,
            "blocked",
            "D",
            ["L"],
            [new("DOCUMENT_CONFLICT", "D", "fail", "blocked")],
            disposition: "conflict",
            originalDocumentId: "HISTORY"
        );
        AssertTotals(conflict, "10.00", "2.00", "12.00");
        AssertLine(conflict, "L", "OL", "deterministic", "10.00", "2.00");
    }
}
