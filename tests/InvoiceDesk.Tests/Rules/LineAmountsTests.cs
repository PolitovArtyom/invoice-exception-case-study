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
}
