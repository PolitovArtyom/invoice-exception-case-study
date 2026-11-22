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
    MatchSource? OrderSource,
    MatchedLine[] Lines,
    InvoiceBusinessKey? BusinessKey,
    InvoiceImportMatch? ConflictingInvoice,
    InvoiceTotals Totals
);

internal sealed record MatchedLine(
    InvoiceLine Invoice,
    PurchaseOrderLine? OrderLine,
    MatchSource? Source,
    LineMatchReason? MatchReason,
    LineAmounts Amounts,
    LineQuantities? Quantities
);
