using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Duplicates;

internal sealed class DocumentConflictCheck : IDocumentCheck
{
    public CheckCode Code => CheckCode.DocumentConflict;

    public Verdict Evaluate(CheckContext context)
    {
        if (context.Supplier is null)
            return Verdict.DependsOn(CheckCode.SupplierIdentity);
        if (context.Buyer is null)
            return Verdict.DependsOn(CheckCode.BuyerIdentity);

        if (context.ConflictingInvoice is not { } conflict)
            return Verdict.Pass(
                new Comparison(
                    Actual:
                    [
                        Operand.Invoice("invoice_number", context.Facts.Number),
                        Operand.Calculated("business_key", context.BusinessKey),
                    ],
                    Expected: [Operand.Policy("prior_matches", "none")]
                )
            );

        var actual = InvoiceContent.Normalize(context.Facts);
        var (originalLines, currentLines) = InvoiceContent.LineDifferences(conflict.Facts, actual);
        return Verdict.Fail(
            Severity.Blocked,
            new Comparison(
                Actual: [Operand.Invoice("normalized_facts", actual with { Lines = currentLines })],
                Expected:
                [
                    Operand.History("original_document_id", conflict.Original.DocumentId),
                    Operand.History(
                        "normalized_facts",
                        conflict.Facts with
                        {
                            Lines = originalLines,
                        }
                    ),
                ]
            )
        );
    }
}
