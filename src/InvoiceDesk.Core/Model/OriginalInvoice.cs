namespace InvoiceDesk.Core.Model;

public sealed record OriginalInvoice(
    string DocumentId,
    string ContentSha256,
    long? DocumentReference,
    string? AssessmentId
);
