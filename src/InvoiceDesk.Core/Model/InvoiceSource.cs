namespace InvoiceDesk.Core.Model;

public sealed record InvoiceSource(
    SourceRoot Root,
    string RelativePath,
    string ContentSha256,
    string DocumentId
);

public sealed record InvoiceReading(
    InvoiceFacts Facts,
    string ContentSha256,
    InvoiceSource? Source,
    ReadingOrigin ReadingOrigin
);

public enum SourceRoot
{
    Public,
    Own,
}

public enum ReadingOrigin
{
    Fixture,
    Agent,
    Runner,
}
