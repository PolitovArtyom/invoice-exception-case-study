namespace InvoiceDesk.Core.Model;

public sealed record CaseMetadata(
    string CaseId,
    CaseOrigin Origin,
    DateOnly AsOf,
    IReadOnlyList<InvoiceSource> Sources
)
{
    public const string PolicyVersion = "core-v1";
}

public enum CaseOrigin
{
    Assignment,
    Own,
}
