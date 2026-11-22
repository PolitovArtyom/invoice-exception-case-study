namespace InvoiceDesk.Core.Model;

public sealed record InvoiceSource(
    string Root,
    string RelativePath,
    string ContentSha256,
    string DocumentId
);

public sealed record InvoiceReading(
    InvoiceFacts Facts,
    string ContentSha256,
    InvoiceSource? Source,
    string ReadingOrigin
)
{
    public const string FixtureReadingOrigin = "loaded from test data, not read by the agent";
}
