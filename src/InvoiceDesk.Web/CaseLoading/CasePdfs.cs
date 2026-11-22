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
            InvoicePdfAvailable = FindPdf(
                view.ShowDuplicateReading
                    ? view.Revision.DuplicateAttempt?.Reading.Source
                    : view.Revision.Document?.Source
            )
                is not null,
            OriginalPdfAvailable = FindOriginal(view.Case, view.Revision.OriginalInvoice)
                is not null,
        };

    public string? FindDocument(string caseId, long reference) =>
        FindPdf(reader.ReadDocumentSource(caseId, reference));

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
