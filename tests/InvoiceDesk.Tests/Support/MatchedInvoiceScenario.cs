using System.Text.Json.Nodes;
using InvoiceDesk.Core.Cases;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Validation;
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

    public async Task<JsonNode> RegisterAndRead()
    {
        using var environment = new CaseTestEnvironment();
        await using var app = environment.Start();
        using var page = app.CreateClient();
        var source = new InvoiceSource(
            SourceRoot.Own,
            "document-001.pdf",
            "09e11592605b5a234e9e2e3df698972f3fd3e8569c6175701676d832b6f83837",
            "D"
        );
        Directory.CreateDirectory(environment.ScenarioPath);
        File.Copy(
            Path.Combine(
                PublicCases.Folder(PublicCases.CleanCase),
                "documents",
                "document-001.pdf"
            ),
            Path.Combine(environment.ScenarioPath, source.RelativePath)
        );
        using (var scope = app.Services.CreateScope())
        {
            var companyData = IntakeValidation.ReadCompanyData(
                JsonNode.Parse(InvoiceJson.Write(CompanyData)),
                "/context"
            );
            var cases = scope.ServiceProvider.GetRequiredService<CaseService>();
            Assert.True(
                cases.LoadCase(
                    new(CaseId, CaseOrigin.Own, Facts.IssueDate, [source]),
                    companyData,
                    null
                )
            );
        }

        await using var mcp = await ConnectMcp(app);
        var registration = Payload(
            await Register(
                mcp,
                JsonNode.Parse(InvoiceJson.Write(Facts))!,
                new { root = "own", relative_path = source.RelativePath }
            )
        );
        var saved = await Read(page, $"/api/cases/{CaseId}");
        AssertSameJson(registration["result"], saved["revision"]!["result"]);
        return saved["revision"]!["result"]!;
    }
}
