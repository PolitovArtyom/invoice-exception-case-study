using System.Text.Json.Nodes;
using InvoiceDesk.Core.Cases;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Validation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;

namespace InvoiceDesk.Tests.Support;

internal sealed record MatchedInvoiceScenario(InvoiceFacts Facts, CompanyData CompanyData)
{
    public const string ApprovedIban = "DE00SYNTHETIC0000000001";
    private const string CaseId = CaseTestEnvironment.ReviewerCaseId;

    public static MatchedInvoiceScenario Baseline()
    {
        var issueDate = new DateOnly(2026, 10, 26);
        var facts = new InvoiceFacts(
            "1.0",
            "D",
            DocumentType.Invoice,
            $"INV-{Guid.NewGuid():N}",
            issueDate,
            issueDate.AddDays(30),
            "EUR",
            "PO-BASELINE",
            "30 days",
            new("Supplier GmbH", "SUP-REG", "DE"),
            new("Buyer GmbH", "BUY-REG", "DE"),
            new(ApprovedIban, "SYNBDE00"),
            [new("L", "Gloves", "GLOVE", 1m, "pcs", 10.00m, 10.00m, 0.20m, 2.00m)],
            10.00m,
            2.00m,
            12.00m,
            [],
            [new("/type", "D", 1, "INVOICE")]
        );
        var companyData = new CompanyData(
            "CTX-BASELINE",
            new(
                "1.0",
                [
                    new(
                        "O",
                        "PO-BASELINE",
                        OrderStatus.Open,
                        "S",
                        "B",
                        "EUR",
                        [new("OL", "GLOVE", "Gloves", [], 10m, "pcs", 10.00m, 0.20m)]
                    ),
                ]
            ),
            new(
                "1.0",
                ReceiptAvailability.Available,
                [new("R", "O", "OL", 10m, "pcs", issueDate, ReceiptStatus.Accepted)]
            ),
            new(
                "1.0",
                [new("S", "Supplier GmbH", "DE", "SUP-REG", [new(ApprovedIban, "SYNBDE00")])]
            ),
            new("1.0", [new("B", "Buyer GmbH", "DE", "BUY-REG", [])]),
            new("1.0", [])
        );
        return new(facts, companyData);
    }

    public MatchedInvoiceScenario WithoutOrderReference() =>
        this with
        {
            Facts = Facts with
            {
                PurchaseOrderReference = null,
                MissingFields =
                [
                    .. Facts.MissingFields,
                    new("/purchase_order_reference", "not_present_in_document"),
                ],
            },
        };

    public MatchedInvoiceScenario WithFirstLine(InvoiceLine line) =>
        this with
        {
            Facts = Facts with
            {
                Lines = [line, .. Facts.Lines.Skip(1)],
                MissingFields =
                    line.Sku is null && Facts.Lines[0].Sku is not null
                        ? [.. Facts.MissingFields, new("/lines/0/sku", "not_present_in_document")]
                        : Facts.MissingFields,
            },
        };

    public MatchedInvoiceScenario WithOrders(params PurchaseOrder[] orders) =>
        this with
        {
            CompanyData = CompanyData with
            {
                PurchaseOrders = CompanyData.PurchaseOrders with { Orders = orders },
            },
        };

    public MatchedInvoiceScenario WithMissingIban() =>
        this with
        {
            Facts = Facts with
            {
                Bank = Facts.Bank with { Iban = null },
                MissingFields =
                [
                    .. Facts.MissingFields,
                    new("/bank/iban", "not_present_in_document"),
                ],
            },
        };

    public MatchedInvoiceScenario WithLineAmounts(
        decimal quantity,
        decimal price,
        decimal rate,
        decimal net,
        decimal tax,
        decimal total,
        bool matchOrderPrice = false
    )
    {
        var invoice = Facts.Lines[0] with
        {
            Quantity = quantity,
            UnitNetPrice = price,
            TaxRate = rate,
            DeclaredNetAmount = net,
            DeclaredTaxAmount = tax,
        };
        var scenario = this with
        {
            Facts = Facts with
            {
                Lines = [invoice],
                DeclaredSubtotal = net,
                DeclaredTaxTotal = tax,
                DeclaredTotal = total,
            },
        };
        return matchOrderPrice
            ? scenario.WithOrderLines(
                CompanyData.PurchaseOrders.Orders[0].Lines[0] with
                {
                    UnitNetPrice = price,
                }
            )
            : scenario;
    }

    public MatchedInvoiceScenario WithSecondLine()
    {
        var order = CompanyData.PurchaseOrders.Orders[0];
        var receipt = CompanyData.GoodsReceipts.Receipts[0];
        var line = Facts.Lines[0];
        var scenario = WithOrderLines(
            order.Lines[0],
            order.Lines[0] with
            {
                LineId = "OL2",
                Sku = "TAPE",
                Description = "Tape",
            }
        );
        return scenario with
        {
            Facts = Facts with
            {
                Lines = [line, line with { LineId = "L2", Sku = "TAPE", Description = "Tape" }],
            },
            CompanyData = scenario.CompanyData with
            {
                GoodsReceipts = CompanyData.GoodsReceipts with
                {
                    Receipts = [receipt, receipt with { ReceiptId = "R2", OrderLineId = "OL2" }],
                },
            },
        };
    }

    public MatchedInvoiceScenario WithOrderLines(params PurchaseOrderLine[] lines) =>
        this with
        {
            CompanyData = CompanyData with
            {
                PurchaseOrders = CompanyData.PurchaseOrders with
                {
                    Orders = [CompanyData.PurchaseOrders.Orders[0] with { Lines = lines }],
                },
            },
        };

    public MatchedInvoiceScenario WithCountedInvoice(InvoiceFacts original, string documentId) =>
        this with
        {
            CompanyData = CompanyData with
            {
                InvoiceHistory = new(
                    "1.0",
                    [
                        new(
                            documentId,
                            "S",
                            "B",
                            DocumentType.Invoice,
                            original.Number,
                            "EUR",
                            PreviousInvoiceStatus.Counted,
                            "O",
                            new string('a', 64),
                            [new("L", "OL", 4m)],
                            new(
                                original.IssueDate,
                                original.DueDate,
                                "EUR",
                                "PO-BASELINE",
                                ApprovedIban,
                                10.00m,
                                2.00m,
                                12.00m,
                                [new("GLOVE", 1m, "pcs", 10.00m, 0.20m, 10.00m, 2.00m)]
                            )
                        ),
                    ]
                ),
            },
        };

    public async Task<JsonNode> RegisterAndRead()
    {
        using var environment = new CaseTestEnvironment();
        await using var app = environment.Start();
        using var page = app.CreateClient();
        Prepare(environment, app);
        await using var mcp = await ConnectMcp(app);
        var registration = Payload(await Register(mcp, RawFacts(Facts), Source()));
        var saved = await Read(page, $"/api/cases/{CaseId}");
        AssertSameJson(registration["result"], saved["revision"]!["result"]);
        return saved["revision"]!["result"]!;
    }

    public void Prepare(
        CaseTestEnvironment environment,
        WebApplicationFactory<Program> app,
        string? secondDocumentId = null
    )
    {
        Directory.CreateDirectory(environment.ScenarioPath);
        var sources = new List<InvoiceSource>
        {
            CopySource(environment, Facts.DocumentId, second: false),
        };
        if (secondDocumentId is not null)
            sources.Add(CopySource(environment, secondDocumentId, second: true));

        using var scope = app.Services.CreateScope();
        var companyData = IntakeValidation.ReadCompanyData(
            JsonNode.Parse(InvoiceJson.Write(CompanyData)),
            "/context"
        );
        var cases = scope.ServiceProvider.GetRequiredService<CaseService>();
        Assert.True(
            cases.LoadCase(new(CaseId, CaseOrigin.Own, Facts.IssueDate, sources), companyData, null)
        );
    }

    public static JsonObject RawFacts(InvoiceFacts facts) =>
        JsonNode.Parse(InvoiceJson.Write(facts))!.AsObject();

    public static object Source(bool second = false) =>
        new { root = "own", relative_path = SourceFile(second) };

    private static string SourceFile(bool second) =>
        second ? "document-002.pdf" : "document-001.pdf";

    private static InvoiceSource CopySource(
        CaseTestEnvironment environment,
        string documentId,
        bool second
    )
    {
        string path = Path.Combine(environment.ScenarioPath, SourceFile(second));
        File.Copy(
            Path.Combine(
                PublicCases.Folder(second ? "case-008" : PublicCases.CleanCase),
                "documents",
                "document-001.pdf"
            ),
            path
        );
        string sha256 = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))
        );
        return new(SourceRoot.Own, SourceFile(second), sha256, documentId);
    }
}
