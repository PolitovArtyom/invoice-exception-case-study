using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;

namespace InvoiceDesk.Core.Rules;

public sealed record AssessmentInput(string CaseId, int Step, int Revision, InvoiceFacts Facts, CompanySnapshot CompanyData,
    IReadOnlyList<EarlierInvoice> EarlierImports);

public sealed record EarlierInvoice(InvoiceFacts Facts, CompanySnapshot CompanyData);
