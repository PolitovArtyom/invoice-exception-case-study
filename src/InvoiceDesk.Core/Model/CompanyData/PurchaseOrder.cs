namespace InvoiceDesk.Core.Model.CompanyData;

public sealed record PurchaseOrderDirectory(string SchemaVersion, PurchaseOrder[] Orders);

public sealed record PurchaseOrder(
    string OrderId, string Reference, string Status, string SupplierId, string BuyerId, string Currency,
    PurchaseOrderLine[] Lines);

public sealed record PurchaseOrderLine(
    string LineId, string? Sku, string Description, string[] ExplicitAliases, string Quantity, string Unit,
    string UnitNetPrice, string TaxRate);
