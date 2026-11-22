using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Pricing;

internal static class LineArithmeticCheck
{
    public static (LineAmounts Amounts, InvoiceCheck Verdict) Evaluate(InvoiceFacts facts, InvoiceLine line, int index)
    {
        var amounts = LineAmounts.Calculate(line);
        Check.Require(amounts.Net == InvoiceDecimal.Parse(line.DeclaredNetAmount) &&
            (line.DeclaredTaxAmount is null || amounts.Tax == InvoiceDecimal.Parse(line.DeclaredTaxAmount)),
            $"LINE_ARITHMETIC with inconsistent declared amounts for {line.LineId}");

        return (amounts, Check.Pass("LINE_ARITHMETIC", line.LineId, facts,
            new Dictionary<string, CheckOperand>
            {
                ["quantity"] = CheckOperand.Invoice(line.Quantity),
                ["unit_net_price"] = CheckOperand.Invoice(line.UnitNetPrice),
                ["tax_rate"] = CheckOperand.Invoice(line.TaxRate),
                ["declared_net_amount"] = CheckOperand.Invoice(line.DeclaredNetAmount),
                ["declared_tax_amount"] = CheckOperand.Invoice(line.DeclaredTaxAmount)
            },
            new Dictionary<string, CheckOperand>
            {
                ["calculated_net"] = CheckOperand.Calculated(InvoiceDecimal.Money(amounts.Net)),
                ["calculated_tax"] = CheckOperand.Calculated(InvoiceDecimal.Money(amounts.Tax))
            }, $"/lines/{index}/quantity", $"/lines/{index}/unit_net_price", $"/lines/{index}/tax_rate",
            $"/lines/{index}/declared_net_amount", $"/lines/{index}/declared_tax_amount"));
    }
}
