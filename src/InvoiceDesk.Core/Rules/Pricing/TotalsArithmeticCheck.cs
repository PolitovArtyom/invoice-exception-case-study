using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Pricing;

internal static class TotalsArithmeticCheck
{
    private const string Code = "TOTALS_ARITHMETIC";

    public static (InvoiceTotals Totals, CheckOutcome Outcome) Evaluate(InvoiceFacts facts, IReadOnlyList<LineAmounts> amounts)
    {
        decimal subtotal = amounts.Sum(line => line.Net);
        decimal tax = amounts.Sum(line => line.Tax);
        decimal total = subtotal + tax;
        CheckOutcome.Require(subtotal == InvoiceDecimal.Parse(facts.DeclaredSubtotal) &&
            tax == InvoiceDecimal.Parse(facts.DeclaredTaxTotal) && total == InvoiceDecimal.Parse(facts.DeclaredTotal),
            $"{Code} with inconsistent declared totals");

        var totals = new InvoiceTotals(InvoiceDecimal.Money(subtotal), InvoiceDecimal.Money(tax), InvoiceDecimal.Money(total));
        return (totals, CheckOutcome.Pass(Code, facts.DocumentId, facts,
            new Dictionary<string, CheckOperand>
            {
                ["declared_subtotal"] = CheckOperand.Invoice(facts.DeclaredSubtotal),
                ["declared_tax_total"] = CheckOperand.Invoice(facts.DeclaredTaxTotal),
                ["declared_total"] = CheckOperand.Invoice(facts.DeclaredTotal)
            },
            new Dictionary<string, CheckOperand>
            {
                ["calculated_subtotal"] = CheckOperand.Calculated(totals.CalculatedSubtotal),
                ["calculated_tax_total"] = CheckOperand.Calculated(totals.CalculatedTaxTotal),
                ["calculated_total"] = CheckOperand.Calculated(totals.CalculatedTotal)
            }, "/declared_subtotal", "/declared_tax_total", "/declared_total"));
    }
}
