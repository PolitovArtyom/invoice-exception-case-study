using InvoiceDesk.Core.Model;

namespace InvoiceDesk.Core.Storage;

internal sealed class InvoiceDocument
{
    public long Id { get; init; }

    public required string CaseId { get; init; }

    public required int ImportRevision { get; init; }

    public required string ContentSha256 { get; init; }

    public required InvoiceFacts Facts { get; init; }

    public InvoiceSource? Source { get; init; }

    public required ReadingOrigin ReadingOrigin { get; init; }
}
