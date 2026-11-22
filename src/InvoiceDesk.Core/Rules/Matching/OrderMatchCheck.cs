using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Normalization;

namespace InvoiceDesk.Core.Rules.Matching;

internal static class OrderMatchCheck
{
    public static (PurchaseOrder Order, InvoiceCheck Check) Evaluate(InvoiceFacts facts, CompanySnapshot company)
    {
        var reference = InvoiceText.Identifier(facts.PurchaseOrderReference);
        var matches = company.PurchaseOrders.Orders
            .Where(order => InvoiceText.Identifier(order.Reference) == reference)
            .Take(2).ToArray();
        if (matches.Length != 1)
            throw new UnsupportedInvoiceCaseException("ORDER_MATCH without a unique explicit purchase order reference");

        var order = matches[0];
        return (order, Rules.Check.Pass("ORDER_MATCH", facts.DocumentId, facts,
            new Dictionary<string, CheckOperand> { ["purchase_order_reference"] = CheckOperand.Invoice(facts.PurchaseOrderReference) },
            new Dictionary<string, CheckOperand>
            {
                ["reference"] = CheckOperand.Company(order.Reference),
                ["order_id"] = CheckOperand.Company(order.OrderId)
            }, "/purchase_order_reference"));
    }
}
