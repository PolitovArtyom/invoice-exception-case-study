using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Rules.Duplicates;

namespace InvoiceDesk.Core.Rules.Quantities;

internal sealed record LineQuantities(
    decimal Ordered,
    decimal? Received,
    decimal PreviouslyInvoiced
)
{
    public decimal? AvailableToInvoice => Received - PreviouslyInvoiced;

    public decimal RemainingOnOrder => Ordered - PreviouslyInvoiced;

    public static LineQuantities Calculate(
        PurchaseOrder order,
        PurchaseOrderLine line,
        CompanyData companyData,
        InvoiceBusinessKey? key
    )
    {
        decimal previous = companyData
            .InvoiceHistory.Documents.Where(invoice =>
                invoice.Status == PreviousInvoiceStatus.Counted
                && invoice.MatchedOrderId == order.OrderId
                && key?.Matches(invoice) != true
            )
            .SelectMany(invoice => invoice.Lines)
            .Where(invoiceLine => invoiceLine.OrderLineId == line.LineId)
            .Sum(invoiceLine => invoiceLine.Quantity);

        decimal? received =
            companyData.GoodsReceipts.Availability == ReceiptAvailability.Available
                ? companyData
                    .GoodsReceipts.Receipts.Where(receipt =>
                        receipt.OrderLineId == line.LineId
                        && receipt.Status == ReceiptStatus.Accepted
                    )
                    .Sum(receipt => receipt.AcceptedQuantity)
                : null;

        return new(line.Quantity, received, previous);
    }
}
