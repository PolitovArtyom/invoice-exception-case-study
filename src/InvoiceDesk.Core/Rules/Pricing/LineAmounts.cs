using InvoiceDesk.Core.Model;

namespace InvoiceDesk.Core.Rules.Pricing;

internal sealed record LineAmounts(decimal Net, decimal Tax)
{
    public static LineAmounts Calculate(InvoiceLine line)
    {
        decimal net = RoundMoney(line.Quantity * line.UnitNetPrice);
        decimal tax = RoundMoney(net * line.TaxRate);
        return new(net, tax);
    }

    public static LineAmounts Sum(IEnumerable<LineAmounts> lines)
    {
        decimal subtotal = 0;
        decimal tax = 0;
        foreach (var line in lines)
        {
            subtotal += line.Net;
            tax += line.Tax;
        }

        return new(subtotal, tax);
    }

    private static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
