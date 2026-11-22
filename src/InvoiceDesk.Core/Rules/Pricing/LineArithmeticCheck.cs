using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Pricing;

internal sealed class LineArithmeticCheck : ILineCheck
{
    public CheckCode Code => CheckCode.LineArithmetic;

    public Verdict Evaluate(CheckContext context, MatchedLine line)
    {
        var invoice = line.Invoice;
        var amounts = line.Amounts;
        UnsupportedInvoiceCaseException.Require(
            amounts.Net == invoice.DeclaredNetAmount
                && (invoice.DeclaredTaxAmount is null || amounts.Tax == invoice.DeclaredTaxAmount),
            Code,
            $"with inconsistent declared amounts for {invoice.LineId}"
        );

        return Verdict.Pass(
            new Comparison(
                Actual:
                [
                    Operand.Invoice("quantity", invoice.Quantity),
                    Operand.Invoice("unit_net_price", invoice.UnitNetPrice),
                    Operand.Invoice("tax_rate", invoice.TaxRate),
                    Operand.Invoice("declared_net_amount", invoice.DeclaredNetAmount),
                    Operand.Invoice("declared_tax_amount", invoice.DeclaredTaxAmount),
                ],
                Expected:
                [
                    Operand.Calculated("calculated_net", amounts.Net),
                    Operand.Calculated("calculated_tax", amounts.Tax),
                ]
            )
        );
    }
}
