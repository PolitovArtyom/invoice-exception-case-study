using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Duplicates;
using InvoiceDesk.Core.Rules.Matching;
using InvoiceDesk.Core.Rules.Pricing;
using InvoiceDesk.Core.Rules.Quantities;

namespace InvoiceDesk.Core.Rules;

internal sealed record CheckContext(
    InvoiceFacts Facts,
    CompanyData CompanyData,
    Supplier? Supplier,
    Buyer? Buyer,
    PurchaseOrder? Order,
    MatchedLine[] Lines,
    InvoiceBusinessKey? BusinessKey,
    IReadOnlyList<PreviousInvoice> PreviousInvoices,
    IReadOnlyList<EarlierImport> EarlierImports,
    InvoiceTotals Totals
);

internal sealed record MatchedLine(
    InvoiceLine Invoice,
    PurchaseOrderLine? OrderLine,
    LineMatchReason? MatchReason,
    LineAmounts Amounts,
    LineQuantities? Quantities
);
