using System.Text.Json.Nodes;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Tests.Support;
using static InvoiceDesk.Tests.Support.AssessmentAssertions;

namespace InvoiceDesk.Tests.Api;

public sealed class OrderMatchingTests
{
    [Fact]
    [Trait("H", "H-13")]
    public async Task Should_ask_for_order_when_explicit_reference_is_unknown()
    {
        var baseline = MatchedInvoiceScenario.Baseline();
        var result = await (
            baseline with
            {
                Facts = baseline.Facts with { PurchaseOrderReference = "PO-NOT-FOUND" },
            }
        ).RegisterAndRead();
        AssertUnknownOrder(result);
    }

    [Fact]
    [Trait("H", "H-14")]
    public async Task Should_select_order_when_one_open_order_matches_every_line()
    {
        var result = await MatchedInvoiceScenario
            .Baseline()
            .WithoutOrderReference()
            .RegisterAndRead();
        AssertBaselineOrder(result);
    }

    [Fact]
    [Trait("H", "H-15")]
    public async Task Should_ask_for_order_when_multiple_orders_fully_match()
    {
        var baseline = MatchedInvoiceScenario.Baseline().WithoutOrderReference();
        var order = baseline.CompanyData.PurchaseOrders.Orders[0];
        var other = order with
        {
            OrderId = "O2",
            Reference = "PO-OTHER",
            Lines = [order.Lines[0] with { LineId = "OL2" }],
        };
        AssertUnknownOrder(await baseline.WithOrders(order, other).RegisterAndRead());
    }

    [Fact]
    [Trait("H", "H-16")]
    public async Task Should_depend_on_supplier_when_reference_and_registration_are_missing()
    {
        var baseline = MatchedInvoiceScenario.Baseline().WithoutOrderReference();
        var result = await (
            baseline with
            {
                Facts = baseline.Facts with
                {
                    Supplier = baseline.Facts.Supplier with { RegistrationId = null },
                    MissingFields =
                    [
                        .. baseline.Facts.MissingFields,
                        new("/supplier/registration_id", "not_present_in_document"),
                    ],
                },
            }
        ).RegisterAndRead();
        AssertOutcome(
            result,
            "needs_information",
            "D",
            ["L"],
            [
                new("SUPPLIER_IDENTITY", "D", "unknown"),
                Dependent("DOCUMENT_CONFLICT", "D", "SUPPLIER_IDENTITY"),
                Dependent("BANK_ACCOUNT", "D", "SUPPLIER_IDENTITY"),
                Dependent("ORDER_MATCH", "D", "SUPPLIER_IDENTITY"),
                Dependent("BUYER_IDENTITY", "D", "ORDER_MATCH"),
                Dependent("CURRENCY_MATCH", "D", "ORDER_MATCH"),
                .. ComparisonsWithoutOrder("L"),
            ],
            [("supplier.registration_id", "D")],
            null,
            null,
            lineMatchSources: [null]
        );
        AssertUnmatchedLine(result);
        AssertTotals(result, "10.00", "2.00", "12.00");
    }

    [Fact]
    [Trait("H", "H-17")]
    public async Task Should_select_order_when_supplier_matches_and_buyer_is_unresolved()
    {
        var baseline = MatchedInvoiceScenario.Baseline().WithoutOrderReference();
        var result = await (
            baseline with
            {
                Facts = baseline.Facts with
                {
                    Buyer = baseline.Facts.Buyer with
                    {
                        RegistrationId = null,
                        LegalName = "Different buyer",
                    },
                    MissingFields =
                    [
                        .. baseline.Facts.MissingFields,
                        new("/buyer/registration_id", "not_present_in_document"),
                    ],
                },
            }
        ).RegisterAndRead();

        AssertOutcome(
            result,
            "needs_information",
            "D",
            ["L"],
            [
                new("BUYER_IDENTITY", "D", "unknown"),
                Dependent("DOCUMENT_CONFLICT", "D", "BUYER_IDENTITY"),
            ],
            [("buyer.registration_id", "D")]
        );
        AssertLine(result, "L", "OL", "deterministic", "10.00", "2.00");
        AssertTotals(result, "10.00", "2.00", "12.00");
    }

    [Fact]
    [Trait("H", "H-18")]
    public async Task Should_select_closed_order_when_reference_is_explicit()
    {
        var baseline = MatchedInvoiceScenario.Baseline();
        var order = baseline.CompanyData.PurchaseOrders.Orders[0];
        var scenario = baseline.WithOrders(order with { Status = OrderStatus.Closed });

        AssertBaselineOrder(await scenario.RegisterAndRead());
        AssertUnknownOrder(await scenario.WithoutOrderReference().RegisterAndRead());
    }

    [Theory]
    [InlineData("PO-012", "PO012")]
    [InlineData("PO-012", "PO-12")]
    [Trait("H", "H-35")]
    public async Task Should_ask_for_order_when_reference_separator_or_leading_zero_changes(
        string orderReference,
        string printedReference
    )
    {
        var baseline = MatchedInvoiceScenario.Baseline();
        var scenario = baseline.WithOrders(
            baseline.CompanyData.PurchaseOrders.Orders[0] with
            {
                Reference = orderReference,
            }
        );
        AssertUnknownOrder(
            await (
                scenario with
                {
                    Facts = scenario.Facts with { PurchaseOrderReference = printedReference },
                }
            ).RegisterAndRead()
        );
    }

    internal static void AssertUnknownOrder(JsonNode result)
    {
        AssertOutcome(
            result,
            "needs_information",
            "D",
            ["L"],
            [
                new("ORDER_MATCH", "D", "unknown"),
                Dependent("SUPPLIER_IDENTITY", "D", "ORDER_MATCH"),
                Dependent("BUYER_IDENTITY", "D", "ORDER_MATCH"),
                Dependent("CURRENCY_MATCH", "D", "ORDER_MATCH"),
                .. ComparisonsWithoutOrder("L"),
            ],
            [("order_reference", "D")],
            null,
            null,
            lineMatchSources: [null]
        );
        AssertUnmatchedLine(result);
        AssertTotals(result, "10.00", "2.00", "12.00");
    }

    private static void AssertBaselineOrder(JsonNode result)
    {
        AssertOutcome(result, "clear", "D", ["L"], []);
        AssertLine(result, "L", "OL", "deterministic", "10.00", "2.00");
        AssertTotals(result, "10.00", "2.00", "12.00");
    }

    internal static void AssertUnmatchedLine(JsonNode result, string lineId = "L") =>
        AssertLine(result, lineId, null, null, "10.00", "2.00", null, null, null, null, null);

    internal static ExpectedCheck Dependent(string code, string objectId, string dependency) =>
        new(code, objectId, "unknown", Dependency: dependency);

    internal static ExpectedCheck[] ComparisonsWithoutOrder(string lineId) =>
        [
            Dependent("LINE_MATCH", lineId, "ORDER_MATCH"),
            Dependent("QTY_RECEIVED", lineId, "ORDER_MATCH"),
            Dependent("QTY_ORDER_REMAINING", lineId, "ORDER_MATCH"),
            Dependent("UNIT_PRICE", lineId, "ORDER_MATCH"),
        ];
}
