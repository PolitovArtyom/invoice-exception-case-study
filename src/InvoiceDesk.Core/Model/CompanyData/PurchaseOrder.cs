namespace InvoiceDesk.Core.Model.CompanyData;

public sealed record PurchaseOrderDirectory(string SchemaVersion, PurchaseOrder[] Orders);

public sealed record PurchaseOrder(
    string OrderId,
    string Reference,
    OrderStatus Status,
    string SupplierId,
    string BuyerId,
    string Currency,
    PurchaseOrderLine[] Lines
);

public sealed record PurchaseOrderLine(
    string LineId,
    string? Sku,
    string Description,
    string[] ExplicitAliases,
    decimal Quantity,
    string Unit,
    decimal UnitNetPrice,
    decimal TaxRate
);

public enum OrderStatus
{
    Open,
    Closed,
}
