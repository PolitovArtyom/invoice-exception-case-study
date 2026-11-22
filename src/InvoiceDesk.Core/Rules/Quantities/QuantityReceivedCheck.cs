using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Quantities;

internal sealed class QuantityReceivedCheck : ILineCheck
{
    public CheckCode Code => CheckCode.QuantityReceived;

    public Verdict Evaluate(CheckContext context, MatchedLine line)
    {
        var invoice = line.Invoice;
        var quantities = line.Quantities;
        UnsupportedInvoiceCaseException.Require(
            quantities is { Received: not null, AvailableToInvoice: not null },
            Code,
            $"without receipt data for {invoice.LineId}"
        );
        UnsupportedInvoiceCaseException.Require(
            invoice.Quantity <= quantities.AvailableToInvoice,
            Code,
            $"above availability for {invoice.LineId}"
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
                    Operand.Receipts("received", quantities.Received.Value),
                    Operand.History("previously_invoiced", quantities.PreviouslyInvoiced),
                    Operand.Calculated("available_to_invoice", quantities.AvailableToInvoice.Value),
                ]
            )
        );
    }
}
