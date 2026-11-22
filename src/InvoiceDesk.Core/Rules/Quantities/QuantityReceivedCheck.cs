using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Quantities;

internal static class QuantityReceivedCheck
{
    private const string Code = "QTY_RECEIVED";

    public static CheckOutcome Evaluate(InvoiceFacts facts, InvoiceLine line, int index, LineQuantities quantities)
    {
        if (quantities.Received is not decimal received || quantities.AvailableToInvoice is not decimal available)
            throw new UnsupportedInvoiceCaseException($"{Code} without receipt data for {line.LineId}");
        CheckOutcome.Require(InvoiceDecimal.Parse(line.Quantity) <= available, $"{Code} above availability for {line.LineId}");

        return CheckOutcome.Pass(Code, line.LineId, facts,
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
