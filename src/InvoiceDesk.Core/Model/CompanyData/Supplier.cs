namespace InvoiceDesk.Core.Model.CompanyData;

public sealed record SupplierDirectory(string SchemaVersion, Supplier[] Suppliers);

public sealed record Supplier(
    string SupplierId,
    string LegalName,
    string Country,
    string RegistrationId,
    ApprovedBankAccount[] ApprovedBankAccounts
);

public sealed record ApprovedBankAccount(string Iban, string? Bic);
