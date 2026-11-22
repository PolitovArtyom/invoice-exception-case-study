using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Quantities;

internal static class QuantityReceivedCheck
{
    public static InvoiceCheck Evaluate(InvoiceFacts facts, InvoiceLine line, int index, LineQuantities quantities)
    {
        if (quantities.Received is not decimal received || quantities.AvailableToInvoice is not decimal available)
            throw new UnsupportedInvoiceCaseException($"QTY_RECEIVED without receipt data for {line.LineId}");
        Check.Require(InvoiceDecimal.Parse(line.Quantity) <= available, $"QTY_RECEIVED above availability for {line.LineId}");

        return Check.Pass("QTY_RECEIVED", line.LineId, facts,
            new Dictionary<string, CheckOperand>
            {
                ["quantity"] = CheckOperand.Invoice(line.Quantity),
                ["unit"] = CheckOperand.Invoice(line.Unit)
            },
            new Dictionary<string, CheckOperand>
            {
                ["received"] = CheckOperand.Receipts(InvoiceDecimal.Quantity(received)),
                ["previously_invoiced"] = CheckOperand.History(InvoiceDecimal.Quantity(quantities.PreviouslyInvoiced)),
                ["available_to_invoice"] = CheckOperand.Calculated(InvoiceDecimal.Quantity(available))
            }, $"/lines/{index}/quantity", $"/lines/{index}/unit");
    }
}
