using System.Globalization;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Rules.Pricing;

namespace InvoiceDesk.Tests.Rules;

public sealed class LineAmountsTests
{
    [Fact]
    public void Should_calculate_tax_on_rounded_net_when_unrounded_net_would_change_the_tax()
    {
        var line = new InvoiceLine("L01", "Tape", "TAPE", 0.5m, "m", 2.01m, 1.01m, 0.5m, 0.51m);

        var amounts = LineAmounts.Calculate(line);

        Assert.Equal(1.01m, amounts.Net);
        Assert.Equal(0.51m, amounts.Tax);
    }

    [Theory]
    [InlineData("0.325", "1.00", "0.20", "0.33", "0.07")]
    [InlineData("1", "0.01", "0.50", "0.01", "0.01")]
    public void Should_round_half_up_when_net_or_tax_is_exactly_between_cents(
        string quantity,
        string price,
        string rate,
        string net,
        string tax
    )
    {
        var line = new InvoiceLine(
            "L",
            "Tape",
            "TAPE",
            Amount(quantity),
            "m",
            Amount(price),
            Amount(net),
            Amount(rate),
            Amount(tax)
        );

        var amounts = LineAmounts.Calculate(line);

        Assert.Equal(Amount(net), amounts.Net);
        Assert.Equal(Amount(tax), amounts.Tax);
    }

    [Fact]
    public void Should_sum_rounded_taxes_when_the_small_lines_are_taxed_separately()
    {
        var line = new InvoiceLine("L", "Tape", "TAPE", 1m, "m", 0.03m, 0.03m, 0.20m, 0.01m);
        var sums = LineAmounts.Sum([
            LineAmounts.Calculate(line),
            LineAmounts.Calculate(line with { LineId = "L2" }),
        ]);

        Assert.Equal(0.06m, sums.Net);
        Assert.Equal(0.02m, sums.Tax);
    }

    private static decimal Amount(string value) =>
        decimal.Parse(value, CultureInfo.InvariantCulture);
}
