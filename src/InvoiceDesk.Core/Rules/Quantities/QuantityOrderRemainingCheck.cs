using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Quantities;

internal static class QuantityOrderRemainingCheck
{
    public static InvoiceCheck Evaluate(InvoiceFacts facts, InvoiceLine line, int index, LineQuantities quantities)
    {
        Check.Require(InvoiceDecimal.Parse(line.Quantity) <= quantities.RemainingOnOrder,
            $"QTY_ORDER_REMAINING above the outstanding order quantity for {line.LineId}");

        return Check.Pass("QTY_ORDER_REMAINING", line.LineId, facts,
            new Dictionary<string, CheckOperand>
            {
                ["quantity"] = CheckOperand.Invoice(line.Quantity),
                ["unit"] = CheckOperand.Invoice(line.Unit)
            },
            new Dictionary<string, CheckOperand>
            {
                ["ordered"] = CheckOperand.Order(InvoiceDecimal.Quantity(quantities.Ordered)),
                ["previously_invoiced"] = CheckOperand.History(InvoiceDecimal.Quantity(quantities.PreviouslyInvoiced)),
                ["remaining_on_order"] = CheckOperand.Calculated(InvoiceDecimal.Quantity(quantities.RemainingOnOrder))
            }, $"/lines/{index}/quantity", $"/lines/{index}/unit");
    }
}
