using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;

namespace InvoiceDesk.Core.Rules;

internal sealed record EarlierImport(InvoiceFacts Facts, CompanyData CompanyData);
