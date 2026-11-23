using InvoiceDesk.Core.Cases;
using InvoiceDesk.Core.Model;

namespace InvoiceDesk.Web.CaseLoading;

internal sealed class CasePdfs(CaseReader reader, CaseFolders folders)
{
    public object WithAvailability(CaseView view) =>
        new
        {
            view.Case,
            view.CurrentRevision,
            view.Stage,
            view.Revision,
            view.InputsChanged,
            view.LatestDuplicateRevision,
            view.Superseded,
            view.ShowDuplicateReading,
            view.Proposals,
            view.EffectiveProposalId,
            view.OpenSupplierOrders,
            CompanyData = reader.ReadCompanyData(view.Case.CaseId, view.Revision.Number),
            view.Issues,
            view.Acceptance,
            view.CanAccept,
            view.CanCorrect,
            view.CorrectableFacts,
            InvoicePdfAvailable = FindPdf(
                view.ShowDuplicateReading
                    ? view.Revision.DuplicateAttempt?.Reading.Source
                    : view.Revision.Document?.Source
            )
                is not null,
            SourcePdfs = view.Revision.Document is null && !view.ShowDuplicateReading
                ? view
                    .Case.Sources.Select((source, index) => new { Source = source, Index = index })
                    .Where(item => FindPdf(item.Source) is not null)
                    .Select(item => new { item.Index, item.Source.DocumentId })
                    .ToArray()
                : [],
            OriginalPdfAvailable = FindOriginal(view.Case, view.Revision.OriginalInvoice)
                is not null,
        };

    public string? FindDocument(string caseId, long reference) =>
        FindPdf(reader.ReadDocumentSource(caseId, reference));

    public string? FindSource(string caseId, int index) =>
        FindPdf(reader.ReadMetadata(caseId)?.Sources.ElementAtOrDefault(index));

    public string? FindDuplicate(string caseId, int number) =>
        FindPdf(reader.ReadRevision(caseId, number)?.DuplicateAttempt?.Reading.Source);

    public string? FindOriginal(string caseId, int number) =>
        reader.ReadMetadata(caseId) is { } metadata
            ? FindOriginal(metadata, reader.ReadRevision(caseId, number)?.OriginalInvoice)
            : null;

    private string? FindOriginal(CaseMetadata metadata, OriginalInvoice? original) =>
        original switch
        {
            null => null,
            { DocumentReference: long reference } => FindDocument(metadata.CaseId, reference),
            _ => folders.FindReferencePdf(
                metadata.Origin,
                reader.ReadReferencePdfPaths(metadata.CaseId),
                original.ContentSha256
            ),
        };

    private string? FindPdf(InvoiceSource? source) =>
        source is null ? null : folders.FindPdf(source);
}
