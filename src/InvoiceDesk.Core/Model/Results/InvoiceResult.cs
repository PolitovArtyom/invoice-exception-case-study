namespace InvoiceDesk.Core.Model.Results;

public sealed record InvoiceResult(
    string SchemaVersion, string CaseId, int Step, string PolicyVersion, string ContextVersion, int InputRevision,
    string DocumentId, string ImportDisposition, string? OriginalDocumentId, string? ExistingAssessmentRef,
    string? MatchedOrderId, string? OrderMatchSource, InvoiceLineResult[] Lines, InvoiceTotals? Totals,
    InvoiceCheck[] Checks, string? Decision, InvoiceQuestion[] Questions);

public sealed record InvoiceQuestion(string RequiredFact, string[] TargetObjectIds, string Text);
