using InvoiceDesk.Core.Model;

namespace InvoiceDesk.Core.Storage;

internal sealed class InvoiceDocument
{
    public long Id { get; init; }

    public required string CaseId { get; init; }

    public required string ExternalDocumentId { get; init; }

    public required int ImportOrder { get; init; }

    public required string ContentSha256 { get; init; }

    public required InvoiceFacts Facts { get; init; }

    public InvoiceSource? Source { get; init; }

    public required string ReadingOrigin { get; init; }
}
