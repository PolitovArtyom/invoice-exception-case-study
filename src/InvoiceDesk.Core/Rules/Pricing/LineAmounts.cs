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

    private static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
