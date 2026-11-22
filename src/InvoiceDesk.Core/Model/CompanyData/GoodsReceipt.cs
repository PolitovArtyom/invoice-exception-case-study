namespace InvoiceDesk.Core.Model.CompanyData;

public sealed record ReceiptDirectory(string SchemaVersion, string Availability, GoodsReceipt[] Receipts);

public sealed record GoodsReceipt(
    string ReceiptId, string OrderId, string OrderLineId, string AcceptedQuantity, string Unit,
    string AcceptedAt, string Status);
