namespace InvoiceDesk.Core.Model;

public sealed record Evidence(string FieldPath, string DocumentId, int Page, string Excerpt);

public sealed record MissingField(string FieldPath, string Reason);
