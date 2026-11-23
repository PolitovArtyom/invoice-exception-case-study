namespace InvoiceDesk.Core.Model;

public sealed record InvoiceFacts(
    string SchemaVersion,
    string DocumentId,
    DocumentType Type,
    string Number,
    DateOnly IssueDate,
    DateOnly? DueDate,
    string? Currency,
    string? PurchaseOrderReference,
    string? PaymentTerms,
    InvoiceParty Supplier,
    InvoiceParty Buyer,
    InvoiceBank Bank,
    InvoiceLine[] Lines,
    decimal DeclaredSubtotal,
    decimal DeclaredTaxTotal,
    decimal DeclaredTotal,
    MissingField[] MissingFields,
    Evidence[] Evidence
);

public sealed record InvoiceParty(string? LegalName, string? RegistrationId, string? Country);

public sealed record InvoiceBank(string? Iban, string? Bic);

public enum DocumentType
{
    Invoice,
}
