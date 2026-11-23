using System.Globalization;
using InvoiceDesk.Core.Model.HumanResolution;
using InvoiceDesk.Tests.Support;
using static InvoiceDesk.Tests.Support.AssessmentAssertions;

namespace InvoiceDesk.Tests.Api;

public sealed class InvoiceCalculationTests
{
    [Fact]
    [Trait("H", "H-09")]
    public async Task Should_ask_for_bank_account_when_iban_is_missing()
    {
        var result = await MatchedInvoiceScenario.Baseline().WithMissingIban().RegisterAndRead();

        AssertOutcome(
            result,
            "needs_information",
            "D",
            ["L"],
            [new("BANK_ACCOUNT", "D", "unknown")],
            [("bank_account", "D")]
        );
        AssertTotals(result, "10.00", "2.00", "12.00");
        AssertLine(result, "L", "OL", "deterministic", "10.00", "2.00");
    }

    [Fact]
    [Trait("H", "H-26")]
    public async Task Should_need_information_when_price_review_and_missing_bank_coexist()
    {
        var result = await MatchedInvoiceScenario
            .Baseline()
            .WithLineAmounts(1m, 10.02m, 0.20m, 10.02m, 2.00m, 12.02m)
            .WithMissingIban()
            .RegisterAndRead();

        AssertOutcome(
            result,
            "needs_information",
            "D",
            ["L"],
            [new("UNIT_PRICE", "L", "fail", "needs_review"), new("BANK_ACCOUNT", "D", "unknown")],
            [("bank_account", "D")]
        );
        AssertTotals(result, "10.02", "2.00", "12.02");
        AssertLine(result, "L", "OL", "deterministic", "10.02", "2.00");
    }

    [Theory]
    [Trait("H", "H-27")]
    [InlineData("10.01", "12.01", "0.01", "clear", "pass", null)]
    [InlineData("9.99", "11.99", "0.01", "clear", "pass", null)]
    [InlineData("10.02", "12.02", "0.02", "needs_review", "fail", "needs_review")]
    [InlineData("9.98", "11.98", "0.02", "needs_review", "fail", "needs_review")]
    public async Task Should_apply_inclusive_symmetric_tolerance_when_invoice_price_changes(
        string price,
        string total,
        string difference,
        string decision,
        string status,
        string? severity
    )
    {
        var result = await MatchedInvoiceScenario
            .Baseline()
            .WithLineAmounts(1m, Amount(price), 0.20m, Amount(price), 2.00m, Amount(total))
            .RegisterAndRead();

        AssertOutcome(result, decision, "D", ["L"], [new("UNIT_PRICE", "L", status, severity)]);
        AssertTotals(result, price, "2.00", total);
        AssertLine(result, "L", "OL", "deterministic", price, "2.00");
        var check = Assert.Single(result["checks"]!.AsArray(), item =>
            Text(item!, "code") == "UNIT_PRICE"
        )!;
        Assert.Equal(price, Text(check["actual"]!["unit_net_price"]!, "value"));
        Assert.Equal("10.00", Text(check["expected"]!["unit_net_price"]!, "value"));
        Assert.Equal(difference, Text(check["actual"]!["absolute_difference"]!, "value"));
        Assert.Equal("0.01", Text(check["expected"]!["inclusive_tolerance"]!, "value"));
    }

    [Theory]
    [Trait("H", "H-28")]
    [InlineData("0.325", "1.00", "0.20", "0.33", "0.07", "0.40")]
    [InlineData("1", "0.01", "0.50", "0.01", "0.01", "0.02")]
    public async Task Should_round_half_up_when_net_or_tax_reaches_a_half_cent(
        string quantity,
        string price,
        string rate,
        string net,
        string tax,
        string total
    )
    {
        var result = await MatchedInvoiceScenario
            .Baseline()
            .WithLineAmounts(
                Amount(quantity),
                Amount(price),
                Amount(rate),
                Amount(net),
                Amount(tax),
                Amount(total),
                matchOrderPrice: true
            )
            .RegisterAndRead();

        AssertOutcome(result, "clear", "D", ["L"], []);
        AssertLine(result, "L", "OL", "deterministic", net, tax);
        AssertTotals(result, net, tax, total);
    }

    [Fact]
    [Trait("H", "H-29")]
    public async Task Should_sum_rounded_line_taxes_when_two_small_lines_are_invoiced()
    {
        var scenario = MatchedInvoiceScenario
            .Baseline()
            .WithLineAmounts(1m, 0.03m, 0.20m, 0.03m, 0.01m, 0.04m, matchOrderPrice: true)
            .WithSecondLine();
        var result = await (
            scenario with
            {
                Facts = scenario.Facts with
                {
                    DeclaredSubtotal = 0.06m,
                    DeclaredTaxTotal = 0.02m,
                    DeclaredTotal = 0.08m,
                },
            }
        ).RegisterAndRead();

        AssertOutcome(result, "clear", "D", ["L", "L2"], []);
        AssertLine(result, "L", "OL", "deterministic", "0.03", "0.01");
        AssertLine(result, "L2", "OL2", "deterministic", "0.03", "0.01");
        AssertTotals(result, "0.06", "0.02", "0.08");
    }

    [Theory]
    [Trait("H", "H-30")]
    [InlineData("10.01", "2.00")]
    [InlineData("10.00", "2.01")]
    public async Task Should_block_line_arithmetic_when_declared_net_or_tax_differs(
        string declaredNet,
        string declaredTax
    )
    {
        var scenario = MatchedInvoiceScenario.Baseline();
        var result = await (
            scenario with
            {
                Facts = scenario.Facts with
                {
                    Lines =
                    [
                        scenario.Facts.Lines[0] with
                        {
                            DeclaredNetAmount = Amount(declaredNet),
                            DeclaredTaxAmount = Amount(declaredTax),
                        },
                    ],
                },
            }
        ).RegisterAndRead();

        AssertOutcome(
            result,
            "blocked",
            "D",
            ["L"],
            [new("LINE_ARITHMETIC", "L", "fail", "blocked")]
        );
        AssertLine(result, "L", "OL", "deterministic", "10.00", "2.00");
        AssertTotals(result, "10.00", "2.00", "12.00");
    }

    [Fact]
    [Trait("H", "H-31")]
    public async Task Should_use_invoice_rate_when_optional_line_tax_is_missing()
    {
        var scenario = MatchedInvoiceScenario
            .Baseline()
            .WithLineAmounts(1m, 10.00m, 0.10m, 10.00m, 1.00m, 11.00m);
        var result = await (
            scenario with
            {
                Facts = scenario.Facts with
                {
                    Lines = [scenario.Facts.Lines[0] with { DeclaredTaxAmount = null }],
                    MissingFields =
                    [
                        new("/lines/0/declared_tax_amount", MissingReason.NotPresentInDocument),
                    ],
                },
            }
        ).RegisterAndRead();

        AssertOutcome(result, "clear", "D", ["L"], []);
        AssertLine(result, "L", "OL", "deterministic", "10.00", "1.00");
        AssertTotals(result, "10.00", "1.00", "11.00");
        var check = Assert.Single(result["checks"]!.AsArray(), item =>
            Text(item!, "code") == "LINE_ARITHMETIC"
        )!;
        Assert.Null(check["actual"]!["declared_tax_amount"]!["value"]);
    }

    [Theory]
    [Trait("H", "H-32")]
    [InlineData("10.01", "2.00")]
    [InlineData("10.00", "2.01")]
    public async Task Should_block_totals_when_declared_subtotal_or_tax_total_differs(
        string subtotal,
        string tax
    )
    {
        var scenario = MatchedInvoiceScenario.Baseline();
        var result = await (
            scenario with
            {
                Facts = scenario.Facts with
                {
                    DeclaredSubtotal = Amount(subtotal),
                    DeclaredTaxTotal = Amount(tax),
                },
            }
        ).RegisterAndRead();

        AssertOutcome(
            result,
            "blocked",
            "D",
            ["L"],
            [new("TOTALS_ARITHMETIC", "D", "fail", "blocked")]
        );
        AssertLine(result, "L", "OL", "deterministic", "10.00", "2.00");
        AssertTotals(result, "10.00", "2.00", "12.00");
    }

    private static decimal Amount(string value) =>
        decimal.Parse(value, CultureInfo.InvariantCulture);
}
