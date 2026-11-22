using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Normalization;

namespace InvoiceDesk.Core.Rules.Matching;

internal static class OrderMatchCheck
{
    private const string Code = "ORDER_MATCH";

    public static (PurchaseOrder Order, string MatchSource, CheckOutcome Outcome) Evaluate(InvoiceFacts facts, CompanyData companyData)
    {
        var reference = InvoiceText.Identifier(facts.PurchaseOrderReference);
        var matches = companyData.PurchaseOrders.Orders
            .Where(order => InvoiceText.Identifier(order.Reference) == reference)
            .Take(2).ToArray();
        if (matches.Length != 1)
            throw new UnsupportedInvoiceCaseException($"{Code} without a unique explicit purchase order reference");

        var order = matches[0];
        return (order, "deterministic", CheckOutcome.Pass(Code, facts.DocumentId, facts,
            new Dictionary<string, CheckOperand> { ["purchase_order_reference"] = CheckOperand.Invoice(facts.PurchaseOrderReference) },
            new Dictionary<string, CheckOperand>
            {
                ["reference"] = CheckOperand.Order(order.Reference),
                ["order_id"] = CheckOperand.Order(order.OrderId)
            }, "/purchase_order_reference"));
    }
}
