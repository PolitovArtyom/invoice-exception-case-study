namespace InvoiceDesk.Core.Model.CompanyData;

public sealed record InvoiceHistory(string SchemaVersion, PreviousInvoice[] Documents);

public sealed record PreviousInvoice(
    string DocumentId,
    string SupplierId,
    string BuyerId,
    DocumentType DocumentType,
    string DocumentNumber,
    string Currency,
    PreviousInvoiceStatus Status,
    string MatchedOrderId,
    string ContentSha256,
    PreviousInvoiceLine[] Lines,
    NormalizedInvoiceFacts NormalizedFacts
);

public sealed record PreviousInvoiceLine(
    string DocumentLineId,
    string OrderLineId,
    decimal Quantity
);

public sealed record NormalizedInvoiceFacts(
    DateOnly IssueDate,
    DateOnly? DueDate,
    string? Currency,
    string? PurchaseOrderReference,
    string? Iban,
    decimal DeclaredSubtotal,
    decimal DeclaredTaxTotal,
    decimal DeclaredTotal,
    NormalizedInvoiceLine[] Lines
);

public sealed record NormalizedInvoiceLine(
    string? Sku,
    decimal Quantity,
    string Unit,
    decimal UnitNetPrice,
    decimal TaxRate,
    decimal DeclaredNetAmount,
    decimal? DeclaredTaxAmount
);

public enum PreviousInvoiceStatus
{
    Counted,
}
