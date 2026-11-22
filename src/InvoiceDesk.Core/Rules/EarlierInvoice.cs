using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;

namespace InvoiceDesk.Core.Rules;

internal sealed record EarlierInvoice(InvoiceFacts Facts, CompanyData CompanyData);
