using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Rules.Duplicates;
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
    IReadOnlyList<EarlierInvoice> EarlierImports,
    LineAmounts Totals
);

internal sealed record MatchedLine(
    InvoiceLine Invoice,
    PurchaseOrderLine? OrderLine,
    string? MatchReason,
    LineAmounts Amounts,
    LineQuantities? Quantities
);
