using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Matching;

namespace InvoiceDesk.Core.Rules.Quantities;

internal sealed class QuantityReceivedCheck : ILineCheck
{
    public CheckCode Code => CheckCode.QuantityReceived;

    public Verdict Evaluate(CheckContext context, MatchedLine line)
    {
        if (OrderLineComparison.NotComparable(context, line) is { } notComparable)
            return notComparable;

        var invoice = line.Invoice;
        var quantities = line.Quantities!;
        decimal? available = quantities.AvailableToInvoice;
        var comparison = new Comparison(
            Actual:
            [
                Operand.Invoice("quantity", invoice.Quantity),
                Operand.Invoice("unit", invoice.Unit),
            ],
            Expected:
            [
                Operand.Receipts("received", quantities.Received),
                Operand.History("previously_invoiced", quantities.PreviouslyInvoiced),
                Operand.Calculated("available_to_invoice", available),
            ]
        );

        if (available is null)
            return Verdict.MissingFact(RequiredFact.GoodsReceipts, comparison);

        return invoice.Quantity <= available.Value
            ? Verdict.Pass(comparison)
            : Verdict.Fail(Severity.Blocked, comparison);
    }
}
