using InvoiceDesk.Core.Model;

namespace InvoiceDesk.Core.Rules.Duplicates;

internal sealed record EarlierImport(OriginalInvoice Original, InvoiceFacts Facts);
