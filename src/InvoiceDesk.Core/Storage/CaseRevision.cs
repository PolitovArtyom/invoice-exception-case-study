using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Storage;

internal sealed class CaseRevision
{
    public required string CaseId { get; init; }

    public required int Number { get; init; }

    public required RevisionAction Action { get; init; }

    public InvoiceDocument? Document { get; init; }

    public long? DocumentId { get; init; }

    public required CompanyDataSnapshot Snapshot { get; init; }

    public long SnapshotId { get; init; }

    public OriginalInvoice? OriginalInvoice { get; init; }

    public DuplicateAttempt? DuplicateAttempt { get; init; }

    public InvoiceResult? Result { get; init; }
}
