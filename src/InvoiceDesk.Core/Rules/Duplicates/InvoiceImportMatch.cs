using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Rules.Parties;

namespace InvoiceDesk.Core.Rules.Duplicates;

internal sealed record InvoiceImportMatch(
    OriginalInvoice Original,
    NormalizedInvoiceFacts Facts,
    bool IsDuplicate,
    bool IsCorrectionConflict = false
)
{
    public static InvoiceImportMatch? Find(
        InvoiceFacts facts,
        InvoiceBusinessKey key,
        CompanyData companyData,
        IEnumerable<EarlierImport> earlierImports,
        string? activeContentSha256,
        InvoiceFacts? previousFacts,
        bool hasCorrectionConflict
    )
    {
        bool isReassessment = activeContentSha256 is not null;
        var current = InvoiceContent.Normalize(facts);
        bool correctionChangedComparison =
            previousFacts is not null
            && (
                InvoiceParties.Resolve(previousFacts, companyData).Key != key
                || !InvoiceContent.Equal(InvoiceContent.Normalize(previousFacts), current)
            );
        var localVersions = earlierImports
            .Where(invoice => InvoiceParties.Resolve(invoice.Facts, companyData).Key == key)
            .Select(invoice => (invoice.Original, Facts: InvoiceContent.Normalize(invoice.Facts)))
            .ToArray();
        var candidates = companyData
            .InvoiceHistory.Documents.Where(key.Matches)
            .Where(invoice =>
                !isReassessment
                || invoice.DocumentId != facts.DocumentId
                || invoice.ContentSha256 != activeContentSha256
            )
            .Select(invoice =>
                (
                    Original: new OriginalInvoice(
                        invoice.DocumentId,
                        invoice.ContentSha256,
                        null,
                        null
                    ),
                    Facts: InvoiceContent.Normalize(invoice.NormalizedFacts)
                )
            )
            .Concat(localVersions);
        InvoiceImportMatch? first = null;
        foreach (var candidate in candidates)
        {
            if (InvoiceContent.Equal(candidate.Facts, current))
            {
                if (isReassessment && !correctionChangedComparison && !hasCorrectionConflict)
                    continue;

                var original = candidate.Original;
                if (original.DocumentReference is null)
                {
                    original = localVersions
                        .FirstOrDefault(import =>
                            import.Original.DocumentId == original.DocumentId
                            && import.Original.ContentSha256 == original.ContentSha256
                            && InvoiceContent.Equal(import.Facts, candidate.Facts)
                        )
                        .Original
                        ?? original;
                }

                return new(
                    original,
                    candidate.Facts,
                    !isReassessment,
                    IsCorrectionConflict: isReassessment
                );
            }

            first ??= new(candidate.Original, candidate.Facts, false);
        }

        return first;
    }
}
