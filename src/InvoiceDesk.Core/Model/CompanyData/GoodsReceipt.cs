namespace InvoiceDesk.Core.Model.CompanyData;

public sealed record ReceiptDirectory(
    string SchemaVersion,
    ReceiptAvailability Availability,
    GoodsReceipt[] Receipts
);

public sealed record GoodsReceipt(
    string ReceiptId,
    string OrderId,
    string OrderLineId,
    decimal AcceptedQuantity,
    string Unit,
    DateOnly AcceptedAt,
    ReceiptStatus Status
);

public enum ReceiptAvailability
{
    Available,
    Unavailable,
}

public enum ReceiptStatus
{
    Accepted,
}
