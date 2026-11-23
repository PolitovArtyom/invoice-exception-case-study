using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Matching;

internal sealed class OrderMatchCheck : IDocumentCheck
{
    public CheckCode Code => CheckCode.OrderMatch;

    public Verdict Evaluate(CheckContext context)
    {
        var order = context.Order;
        if (order is null)
            return context.Facts.PurchaseOrderReference is null && context.Supplier is null
                ? Verdict.DependsOn(CheckCode.SupplierIdentity)
                : Verdict.MissingFact(RequiredFact.OrderReference);

        var comparison = new Comparison(
            Actual:
            [
                Operand.Invoice("purchase_order_reference", context.Facts.PurchaseOrderReference),
            ],
            Expected:
            [
                Operand.Order("reference", order.Reference),
                Operand.Order("order_id", order.OrderId),
            ]
        );
        return context.OrderSource == MatchSource.AgentProposal
            ? Verdict.Fail(Severity.NeedsReview, comparison)
            : Verdict.Pass(comparison);
    }
}
