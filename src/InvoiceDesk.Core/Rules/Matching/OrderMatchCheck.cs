using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Matching;

internal sealed class OrderMatchCheck : IDocumentCheck
{
    public CheckCode Code => CheckCode.OrderMatch;

    public Verdict Evaluate(CheckContext context)
    {
        var order = context.Order;
        UnsupportedInvoiceCaseException.Require(
            order is not null,
            Code,
            "without a unique explicit purchase order reference"
        );

        return Verdict.Pass(
            new Comparison(
                Actual:
                [
                    Operand.Invoice(
                        "purchase_order_reference",
                        context.Facts.PurchaseOrderReference
                    ),
                ],
                Expected:
                [
                    Operand.Order("reference", order.Reference),
                    Operand.Order("order_id", order.OrderId),
                ]
            )
        );
    }
}
