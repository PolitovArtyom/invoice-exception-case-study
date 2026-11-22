namespace InvoiceDesk.Core.Model;

public sealed record InvoiceFacts(
    string SchemaVersion, string DocumentId, string Type, string Number,
    string IssueDate, string? DueDate, string? Currency, string? PurchaseOrderReference, string? PaymentTerms,
    InvoiceParty Supplier, InvoiceParty Buyer, InvoiceBank Bank, InvoiceLine[] Lines,
    string DeclaredSubtotal, string DeclaredTaxTotal, string DeclaredTotal,
    MissingField[] MissingFields, Evidence[] Evidence);

public sealed record InvoiceParty(string? LegalName, string? RegistrationId, string? Country);

public sealed record InvoiceBank(string? Iban, string? Bic);
