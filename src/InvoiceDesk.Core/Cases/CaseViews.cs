using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Cases;

public sealed record CaseView(
    CaseMetadata Case,
    int CurrentRevision,
    CaseStage Stage,
    RevisionView Revision
);

public sealed record RevisionView(
    int Number,
    string ContextVersion,
    RevisionAction Action,
    DocumentView? Document,
    string? AssessmentId,
    InvoiceResult? Result
);

public sealed record DocumentView(
    long Reference,
    string ContentSha256,
    InvoiceSource? Source,
    InvoiceFacts Facts,
    ReadingOrigin ReadingOrigin
);

public sealed record CaseSummary(
    string CaseId,
    CaseOrigin Origin,
    string? Number,
    string? Supplier,
    decimal? DeclaredTotal,
    string? Currency,
    Decision? Decision,
    CaseStage Stage
);

public sealed record AssessmentResponse(
    string AssessmentId,
    InvoiceResult Result,
    bool InputsUnchanged
);

public enum CaseStage
{
    NotRead,
    NeedsAttention,
    ReadyForAccounting,
}
