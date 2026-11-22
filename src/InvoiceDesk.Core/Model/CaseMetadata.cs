namespace InvoiceDesk.Core.Model;

public sealed record CaseMetadata(
    string CaseId,
    string Origin,
    DateOnly AsOf,
    IReadOnlyList<InvoiceSource> Sources
)
{
    public const string PolicyVersion = "core-v1";
}
