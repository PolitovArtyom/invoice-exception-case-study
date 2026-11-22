namespace InvoiceDesk.Core.Model.Results;

public sealed record InvoiceResult(
    string SchemaVersion,
    string CaseId,
    int Step,
    string PolicyVersion,
    string ContextVersion,
    int InputRevision,
    string DocumentId,
    ImportDisposition ImportDisposition,
    string? OriginalDocumentId,
    string? ExistingAssessmentRef,
    string? MatchedOrderId,
    MatchSource? OrderMatchSource,
    InvoiceLineResult[] Lines,
    InvoiceTotals? Totals,
    CheckResult[] Checks,
    Decision? Decision,
    InvoiceQuestion[] Questions
);

public sealed record InvoiceQuestion(
    RequiredFact RequiredFact,
    string[] TargetObjectIds,
    string Text
);
