namespace InvoiceDesk.Core.Model.CompanyData;

public sealed record BuyerDirectory(string SchemaVersion, Buyer[] Buyers);

public sealed record Buyer(
    string BuyerId,
    string LegalName,
    string Country,
    string RegistrationId,
    string[] ExplicitAliases
);
