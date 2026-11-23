using System.Text.Json.Nodes;
using InvoiceDesk.Tests.Support;
using static InvoiceDesk.Tests.Api.OrderMatchingTests;
using static InvoiceDesk.Tests.Support.AssessmentAssertions;

namespace InvoiceDesk.Tests.Api;

public sealed class LineMatchingTests
{
    [Fact]
    [Trait("H", "H-20")]
    public async Task Should_ask_for_line_when_no_exact_target_exists()
    {
        var baseline = MatchedInvoiceScenario.Baseline();
        var orderLine = baseline.CompanyData.PurchaseOrders.Orders[0].Lines[0] with { Sku = null };
        var invoiceLine = baseline.Facts.Lines[0] with
        {
            Sku = null,
            Description = "Different product",
        };
        AssertUnknownLine(
            await baseline.WithOrderLines(orderLine).WithFirstLine(invoiceLine).RegisterAndRead()
        );
    }

    [Theory]
    [InlineData(null, "  Work   GLOVES ", "Gloves", "Work gloves", "explicit_alias")]
    [InlineData(" glove ", "Different description", "Gloves", "Another alias", "sku")]
    [InlineData(null, "Maßband", "Massband", "Another alias", "description")]
    [Trait("H", "H-21")]
    public async Task Should_match_line_when_one_normalized_key_matches(
        string? sku,
        string description,
        string orderDescription,
        string alias,
        string reason
    )
    {
        var baseline = MatchedInvoiceScenario.Baseline();
        var orderLine = baseline.CompanyData.PurchaseOrders.Orders[0].Lines[0] with
        {
            Description = orderDescription,
            ExplicitAliases = [alias],
        };
        var result = await baseline
            .WithOrderLines(orderLine)
            .WithFirstLine(baseline.Facts.Lines[0] with { Sku = sku, Description = description })
            .RegisterAndRead();
        AssertOutcome(result, "clear", "D", ["L"], []);
        AssertLine(result, "L", "OL", "deterministic", "10.00", "2.00");
        AssertTotals(result, "10.00", "2.00", "12.00");
        var match = Assert.Single(result["checks"]!.AsArray(), item =>
            Text(item!, "code") == "LINE_MATCH"
        );
        Assert.Equal(reason, Text(match!["actual"]!["match_reason"]!, "value"));
    }

    [Theory]
    [InlineData(null, "Gloves", "Gloves", "Another alias")]
    [InlineData(null, "Work gloves", "Safety equipment", "Work gloves")]
    [InlineData("GLOVE", "Tape", "Tape", "Another alias")]
    [Trait("H", "H-22")]
    public async Task Should_leave_line_unresolved_when_multiple_exact_targets_or_keys_compete(
        string? sku,
        string description,
        string secondDescription,
        string secondAlias
    )
    {
        var baseline = MatchedInvoiceScenario.Baseline();
        var line = baseline.CompanyData.PurchaseOrders.Orders[0].Lines[0] with
        {
            ExplicitAliases = ["Work gloves"],
        };
        var scenario = baseline
            .WithOrderLines(
                line,
                line with
                {
                    LineId = "OL2",
                    Sku = "TAPE",
                    Description = secondDescription,
                    ExplicitAliases = [secondAlias],
                }
            )
            .WithFirstLine(baseline.Facts.Lines[0] with { Sku = sku, Description = description });
        AssertUnknownLine(await scenario.RegisterAndRead());
        AssertUnknownOrder(await scenario.WithoutOrderReference().RegisterAndRead());
    }

    [Fact]
    [Trait("H", "H-23")]
    public async Task Should_leave_all_lines_unresolved_when_they_compete_for_one_order_line()
    {
        var baseline = MatchedInvoiceScenario.Baseline();
        var first = baseline.Facts.Lines[0];
        var second = first with { LineId = "L2", Description = "Protective equipment" };
        var scenario = baseline with
        {
            Facts = baseline.Facts with
            {
                DeclaredSubtotal = 20.00m,
                DeclaredTaxTotal = 4.00m,
                DeclaredTotal = 24.00m,
            },
        };
        foreach (var lines in new[] { new[] { first, second }, [second, first] })
        {
            var result = await (
                scenario with
                {
                    Facts = scenario.Facts with { Lines = lines },
                }
            ).RegisterAndRead();
            AssertOutcome(
                result,
                "needs_information",
                "D",
                ["L", "L2"],
                [.. ComparisonsWithoutLine("L"), .. ComparisonsWithoutLine("L2")],
                [("line_match", "L"), ("line_match", "L2")],
                lineMatchSources: [null, null]
            );
            AssertUnmatchedLine(result);
            AssertUnmatchedLine(result, "L2");
            AssertTotals(result, "20.00", "4.00", "24.00");
        }
    }

    [Fact]
    [Trait("H", "H-23")]
    public async Task Should_exclude_an_order_when_lines_share_the_same_target_and_no_reference_is_printed()
    {
        var baseline = MatchedInvoiceScenario.Baseline().WithSecondLine().WithoutOrderReference();
        var first = baseline.Facts.Lines[0];
        var scenario = baseline with
        {
            Facts = baseline.Facts with
            {
                Lines =
                [
                    first,
                    baseline.Facts.Lines[1] with
                    {
                        Sku = first.Sku,
                        Description = "Protective equipment",
                    },
                ],
                DeclaredSubtotal = 20.00m,
                DeclaredTaxTotal = 4.00m,
                DeclaredTotal = 24.00m,
            },
        };
        var result = await scenario.RegisterAndRead();

        AssertOutcome(
            result,
            "needs_information",
            "D",
            ["L", "L2"],
            [
                new("ORDER_MATCH", "D", "unknown"),
                Dependent("SUPPLIER_IDENTITY", "D", "ORDER_MATCH"),
                Dependent("BUYER_IDENTITY", "D", "ORDER_MATCH"),
                Dependent("CURRENCY_MATCH", "D", "ORDER_MATCH"),
                .. ComparisonsWithoutOrder("L"),
                .. ComparisonsWithoutOrder("L2"),
            ],
            [("order_reference", "D")],
            matchedOrderId: null,
            orderMatchSource: null,
            lineMatchSources: [null, null]
        );
        AssertUnmatchedLine(result);
        AssertUnmatchedLine(result, "L2");
        AssertTotals(result, "20.00", "4.00", "24.00");
    }

    [Theory]
    [InlineData("SKU-012", "SKU012")]
    [InlineData("SKU-012", "SKU-12")]
    [Trait("H", "H-35")]
    public async Task Should_ask_for_line_when_sku_separator_or_leading_zero_changes(
        string orderSku,
        string printedSku
    )
    {
        var baseline = MatchedInvoiceScenario.Baseline();
        var line = baseline.CompanyData.PurchaseOrders.Orders[0].Lines[0] with { Sku = orderSku };
        AssertUnknownLine(
            await baseline
                .WithOrderLines(line)
                .WithFirstLine(
                    baseline.Facts.Lines[0] with
                    {
                        Sku = printedSku,
                        Description = "Different description",
                    }
                )
                .RegisterAndRead()
        );
    }

    private static void AssertUnknownLine(JsonNode result)
    {
        AssertOutcome(
            result,
            "needs_information",
            "D",
            ["L"],
            ComparisonsWithoutLine("L"),
            [("line_match", "L")],
            lineMatchSources: [null]
        );
        AssertUnmatchedLine(result);
        AssertTotals(result, "10.00", "2.00", "12.00");
    }

    private static ExpectedCheck[] ComparisonsWithoutLine(string id) =>
        [
            new("LINE_MATCH", id, "unknown"),
            Dependent("QTY_RECEIVED", id, "LINE_MATCH"),
            Dependent("QTY_ORDER_REMAINING", id, "LINE_MATCH"),
            Dependent("UNIT_PRICE", id, "LINE_MATCH"),
        ];
}
