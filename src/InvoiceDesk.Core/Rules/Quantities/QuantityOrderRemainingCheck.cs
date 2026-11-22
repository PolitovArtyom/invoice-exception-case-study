using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Matching;

namespace InvoiceDesk.Core.Rules.Quantities;

internal sealed class QuantityOrderRemainingCheck : ILineCheck
{
    public CheckCode Code => CheckCode.QuantityOrderRemaining;

    public Verdict Evaluate(CheckContext context, MatchedLine line)
    {
        if (OrderLineComparison.NotComparable(context, line) is { } notComparable)
            return notComparable;

        var invoice = line.Invoice;
        var quantities = line.Quantities!;

        var comparison = new Comparison(
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
        );
        return invoice.Quantity <= quantities.RemainingOnOrder
            ? Verdict.Pass(comparison)
            : Verdict.Fail(Severity.Blocked, comparison);
    }
}
