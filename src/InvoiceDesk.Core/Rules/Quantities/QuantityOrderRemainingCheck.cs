using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Quantities;

internal sealed class QuantityOrderRemainingCheck : ILineCheck
{
    public CheckCode Code => CheckCode.QuantityOrderRemaining;

    public Verdict Evaluate(CheckContext context, MatchedLine line)
    {
        var invoice = line.Invoice;
        var quantities = line.Quantities;
        UnsupportedInvoiceCaseException.Require(
            quantities is not null,
            Code,
            $"without order quantities for {invoice.LineId}"
        );
        UnsupportedInvoiceCaseException.Require(
            invoice.Quantity <= quantities.RemainingOnOrder,
            Code,
            $"above the outstanding order quantity for {invoice.LineId}"
        );

        return Verdict.Pass(
            new Comparison(
                Actual:
                [
                    Operand.Invoice("quantity", invoice.Quantity),
                    Operand.Invoice("unit", invoice.Unit),
                ],
                Expected:
                [
                    Operand.Order("ordered", quantities.Ordered),
                    Operand.History("previously_invoiced", quantities.PreviouslyInvoiced),
                    Operand.Calculated("remaining_on_order", quantities.RemainingOnOrder),
                ]
            )
        );
    }
}
