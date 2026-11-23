using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Rules.Parties;

namespace InvoiceDesk.Core.Rules.Duplicates;

internal sealed record InvoiceImportMatch(
    OriginalInvoice Original,
    NormalizedInvoiceFacts Facts,
    bool IsDuplicate
)
{
    public static InvoiceImportMatch? Find(
        InvoiceFacts facts,
        InvoiceBusinessKey key,
        CompanyData companyData,
        IEnumerable<EarlierImport> earlierImports,
        string? activeContentSha256,
        bool hasAccountingAssessment
    )
    {
        bool isReassessment = activeContentSha256 is not null;
        var current = InvoiceContent.Normalize(facts);
        var localVersions = earlierImports
            .Where(invoice => InvoiceParties.Resolve(invoice.Facts, companyData).Key == key)
            .Select(invoice => (invoice.Original, Facts: InvoiceContent.Normalize(invoice.Facts)));
        var candidates = companyData
            .InvoiceHistory.Documents.Where(key.Matches)
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
            .Concat(localVersions)
            .Where(candidate =>
                !isReassessment
                || candidate.Original.DocumentId != facts.DocumentId
                || candidate.Original.ContentSha256 != activeContentSha256
                || (candidate.Original.DocumentReference is null && !hasAccountingAssessment)
            )
            .ToArray();
        InvoiceImportMatch? first = null;
        foreach (var candidate in candidates)
        {
            if (InvoiceContent.Equal(candidate.Facts, current))
            {
                var original = candidate.Original;
                if (original.DocumentReference is null)
                {
                    original = candidates
                        .FirstOrDefault(import =>
                            import.Original.DocumentReference is not null
                            && import.Original.DocumentId == original.DocumentId
                            && import.Original.ContentSha256 == original.ContentSha256
                            && InvoiceContent.Equal(import.Facts, candidate.Facts)
                        )
                        .Original
                        ?? original;
                }

                return new(original, candidate.Facts, !isReassessment);
            }

            first ??= new(candidate.Original, candidate.Facts, false);
        }

        return first;
    }
}
