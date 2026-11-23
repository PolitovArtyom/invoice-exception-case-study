using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Pricing;

internal sealed class TotalsArithmeticCheck : IDocumentCheck
{
    public CheckCode Code => CheckCode.TotalsArithmetic;

    public Verdict Evaluate(CheckContext context)
    {
        var facts = context.Facts;
        var (subtotal, tax, total) = context.Totals;

        var comparison = new Comparison(
            Actual:
            [
                Operand.Invoice("declared_subtotal", facts.DeclaredSubtotal),
                Operand.Invoice("declared_tax_total", facts.DeclaredTaxTotal),
                Operand.Invoice("declared_total", facts.DeclaredTotal),
            ],
            Expected:
            [
                Operand.Calculated("calculated_subtotal", subtotal),
                Operand.Calculated("calculated_tax_total", tax),
                Operand.Calculated("calculated_total", total),
            ]
        );
        return
            subtotal == facts.DeclaredSubtotal
            && tax == facts.DeclaredTaxTotal
            && total == facts.DeclaredTotal
            ? Verdict.Pass(comparison)
            : Verdict.Fail(Severity.Blocked, comparison);
    }
}
