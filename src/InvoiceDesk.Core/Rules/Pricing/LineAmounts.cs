using InvoiceDesk.Core.Model;

namespace InvoiceDesk.Core.Rules.Pricing;

internal sealed record LineAmounts(decimal Net, decimal Tax)
{
    public static LineAmounts Calculate(InvoiceLine line)
    {
        decimal net = InvoiceDecimal.RoundMoney(InvoiceDecimal.Parse(line.Quantity) * InvoiceDecimal.Parse(line.UnitNetPrice));
        decimal tax = InvoiceDecimal.RoundMoney(net * InvoiceDecimal.Parse(line.TaxRate));
        return new(net, tax);
    }
}
