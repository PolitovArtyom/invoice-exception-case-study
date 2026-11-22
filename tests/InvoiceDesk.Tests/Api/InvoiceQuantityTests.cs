using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Tests.Support;
using static InvoiceDesk.Tests.Support.AssessmentAssertions;

namespace InvoiceDesk.Tests.Api;

public sealed class InvoiceQuantityTests
{
    [Fact]
    [Trait("H", "H-11")]
    public async Task Should_keep_received_values_null_when_receipt_data_is_unavailable()
    {
        var scenario = WithoutReceiptData(MatchedInvoiceScenario.Baseline());

        var result = await scenario.RegisterAndRead();

        AssertOutcome(
            result,
            "needs_information",
            "D",
            ["L"],
            [new("QTY_RECEIVED", "L", "unknown")],
            [("goods_receipts", "L")]
        );
        AssertTotals(result, "10.00", "2.00", "12.00");
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
    }

    [Fact]
    [Trait("H", "H-11")]
    public async Task Should_ask_once_per_line_when_receipt_data_is_unavailable_for_two_lines()
    {
        var scenario = WithoutReceiptData(TwoLineInvoice());

        var result = await scenario.RegisterAndRead();

        AssertOutcome(
            result,
            "needs_information",
            "D",
            ["L", "L2"],
            [new("QTY_RECEIVED", "L", "unknown"), new("QTY_RECEIVED", "L2", "unknown")],
            [("goods_receipts", "L"), ("goods_receipts", "L2")]
        );
        AssertTotals(result, "20.00", "4.00", "24.00");
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
        AssertLine(
            result,
            "L2",
            "OL2",
            "deterministic",
            "10.00",
            "2.00",
            received: null,
            available: null
        );
    }

    [Fact]
    public async Task Should_count_history_only_for_the_matched_order_and_line_when_other_history_exists()
    {
        var scenario = TwoLineInvoice();
        var date = scenario.Facts.IssueDate.AddDays(-1);
        scenario = scenario with
        {
            CompanyData = scenario.CompanyData with
            {
                InvoiceHistory = new(
                    "1.0",
                    [
                        CountedInvoice("D-OTHER", "O2", "OL", "GLOVE", 5m, date),
                        CountedInvoice("D-SECOND-LINE", "O", "OL2", "TAPE", 3m, date),
                    ]
                ),
            },
        };

        var result = await scenario.RegisterAndRead();

        AssertOutcome(result, "clear", "D", ["L", "L2"], []);
        AssertLine(
            result,
            "L",
            "OL",
            "deterministic",
            "10.00",
            "2.00",
            previous: "0",
            available: "10",
            remaining: "10"
        );
        AssertLine(
            result,
            "L2",
            "OL2",
            "deterministic",
            "10.00",
            "2.00",
            previous: "3",
            available: "7",
            remaining: "7"
        );
        AssertTotals(result, "20.00", "4.00", "24.00");
    }

    [Fact]
    [Trait("H", "H-12")]
    public async Task Should_block_received_quantity_when_available_receipts_are_empty()
    {
        var scenario = MatchedInvoiceScenario.Baseline();
        scenario = scenario with
        {
            CompanyData = scenario.CompanyData with
            {
                GoodsReceipts = scenario.CompanyData.GoodsReceipts with { Receipts = [] },
            },
        };

        var result = await scenario.RegisterAndRead();

        AssertOutcome(result, "blocked", "D", ["L"], [new("QTY_RECEIVED", "L", "fail", "blocked")]);
        AssertLine(
            result,
            "L",
            "OL",
            "deterministic",
            "10.00",
            "2.00",
            received: "0",
            available: "0"
        );
        AssertTotals(result, "10.00", "2.00", "12.00");
    }

    [Theory]
    [Trait("H", "H-33")]
    [InlineData("10", "1", "QTY_RECEIVED")]
    [InlineData("1", "10", "QTY_ORDER_REMAINING")]
    public async Task Should_fail_only_the_exceeded_limit_when_receipts_or_order_balance_is_insufficient(
        string ordered,
        string received,
        string failedCheck
    )
    {
        var scenario = MatchedInvoiceScenario
            .Baseline()
            .WithLineAmounts(2m, 10.00m, 0.20m, 20.00m, 4.00m, 24.00m);
        var orderLine = scenario.CompanyData.PurchaseOrders.Orders[0].Lines[0];
        var receipt = scenario.CompanyData.GoodsReceipts.Receipts[0];
        scenario = scenario.WithOrderLines(
            orderLine with
            {
                Quantity = InvoiceJson.Read<decimal>($"\"{ordered}\""),
            }
        );
        scenario = scenario with
        {
            CompanyData = scenario.CompanyData with
            {
                GoodsReceipts = scenario.CompanyData.GoodsReceipts with
                {
                    Receipts =
                    [
                        receipt with
                        {
                            AcceptedQuantity = InvoiceJson.Read<decimal>($"\"{received}\""),
                        },
                    ],
                },
            },
        };

        var result = await scenario.RegisterAndRead();

        AssertOutcome(result, "blocked", "D", ["L"], [new(failedCheck, "L", "fail", "blocked")]);
        AssertLine(
            result,
            "L",
            "OL",
            "deterministic",
            "20.00",
            "4.00",
            ordered,
            received,
            available: received,
            remaining: ordered
        );
        AssertTotals(result, "20.00", "4.00", "24.00");
    }

    [Fact]
    [Trait("H", "H-34")]
    public async Task Should_keep_negative_balances_when_counted_quantity_exceeds_capacity()
    {
        var scenario = MatchedInvoiceScenario.Baseline();
        var date = scenario.Facts.IssueDate.AddDays(-1);
        var previous = CountedInvoice("D-PREVIOUS", "O", "OL", "GLOVE", 12m, date);
        scenario = scenario with
        {
            CompanyData = scenario.CompanyData with { InvoiceHistory = new("1.0", [previous]) },
        };

        var result = await scenario.RegisterAndRead();

        AssertOutcome(
            result,
            "blocked",
            "D",
            ["L"],
            [
                new("QTY_RECEIVED", "L", "fail", "blocked"),
                new("QTY_ORDER_REMAINING", "L", "fail", "blocked"),
            ]
        );
        AssertLine(
            result,
            "L",
            "OL",
            "deterministic",
            "10.00",
            "2.00",
            previous: "12",
            available: "-2",
            remaining: "-2"
        );
        AssertTotals(result, "10.00", "2.00", "12.00");
    }

    private static MatchedInvoiceScenario TwoLineInvoice()
    {
        var scenario = MatchedInvoiceScenario.Baseline().WithSecondLine();
        return scenario with
        {
            Facts = scenario.Facts with
            {
                DeclaredSubtotal = 20.00m,
                DeclaredTaxTotal = 4.00m,
                DeclaredTotal = 24.00m,
            },
        };
    }

    private static MatchedInvoiceScenario WithoutReceiptData(MatchedInvoiceScenario scenario) =>
        scenario with
        {
            CompanyData = scenario.CompanyData with
            {
                GoodsReceipts = new("1.0", ReceiptAvailability.Unavailable, []),
            },
        };

    private static PreviousInvoice CountedInvoice(
        string documentId,
        string orderId,
        string orderLineId,
        string sku,
        decimal quantity,
        DateOnly date
    )
    {
        decimal net = quantity * 10.00m;
        decimal tax = quantity * 2.00m;
        return new(
            documentId,
            "S",
            "B",
            DocumentType.Invoice,
            $"INV-{documentId}",
            "EUR",
            PreviousInvoiceStatus.Counted,
            orderId,
            new string('0', 64),
            [new("L-PREVIOUS", orderLineId, quantity)],
            new(
                date,
                date.AddDays(30),
                "EUR",
                "PO-BASELINE",
                MatchedInvoiceScenario.ApprovedIban,
                net,
                tax,
                net + tax,
                [new(sku, quantity, "pcs", 10.00m, 0.20m, net, tax)]
            )
        );
    }
}
