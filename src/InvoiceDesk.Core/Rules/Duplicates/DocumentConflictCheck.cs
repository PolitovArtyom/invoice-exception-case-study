using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Matching;
using InvoiceDesk.Core.Rules.Normalization;

namespace InvoiceDesk.Core.Rules.Duplicates;

internal sealed class DocumentConflictCheck : IDocumentCheck
{
    public CheckCode Code => CheckCode.DocumentConflict;

    public Verdict Evaluate(CheckContext context)
    {
        var key = context.BusinessKey;
        UnsupportedInvoiceCaseException.Require(
            key is not null,
            Code,
            "without a resolved business key"
        );

        var previousInvoiceMatches = context
            .PreviousInvoices.Where(key.Matches)
            .Select(invoice => invoice.DocumentId)
            .ToArray();
        var earlierMatches = context
            .EarlierImports.Where(invoice => Matches(invoice, key))
            .Select(invoice => invoice.Facts.DocumentId)
            .ToArray();
        UnsupportedInvoiceCaseException.Require(
            previousInvoiceMatches.Length == 0 && earlierMatches.Length == 0,
            Code,
            "for a previous invoice or earlier import with the same business key"
        );

        return Verdict.Pass(
            new Comparison(
                Actual:
                [
                    Operand.Invoice("invoice_number", context.Facts.Number),
                    Operand.Calculated("business_key", key),
                    Operand.Calculated("previous_invoice_matches", previousInvoiceMatches),
                    Operand.Calculated("earlier_import_matches", earlierMatches),
                ],
                Expected: [Operand.Policy("prior_matches", "none")]
            )
        );
    }

    private static bool Matches(EarlierInvoice invoice, InvoiceBusinessKey key) =>
        InvoiceText.Identifier(invoice.Facts.Number) == key.Number
        && InvoiceMatches.FindSupplier(invoice.Facts, invoice.CompanyData)?.SupplierId
            == key.SupplierId
        && InvoiceMatches.FindBuyer(invoice.Facts, invoice.CompanyData)?.BuyerId == key.BuyerId;
}
