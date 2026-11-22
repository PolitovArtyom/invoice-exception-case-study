namespace InvoiceDesk.Core.Model.CompanyData;

public sealed record InvoiceHistory(string SchemaVersion, CountedInvoice[] Documents);

public sealed record CountedInvoice(
    string DocumentId, string SupplierId, string BuyerId, string DocumentType, string DocumentNumber,
    string Currency, string Status, string MatchedOrderId, string ContentSha256,
    CountedInvoiceLine[] Lines, NormalizedInvoiceFacts NormalizedFacts);

public sealed record CountedInvoiceLine(string DocumentLineId, string OrderLineId, string Quantity);

public sealed record NormalizedInvoiceFacts(
    string IssueDate, string? DueDate, string? Currency, string? PurchaseOrderReference, string? Iban,
    string DeclaredSubtotal, string DeclaredTaxTotal, string DeclaredTotal, NormalizedInvoiceLine[] Lines);

public sealed record NormalizedInvoiceLine(
    string? Sku, string Quantity, string Unit, string UnitNetPrice, string TaxRate,
    string DeclaredNetAmount, string? DeclaredTaxAmount);
