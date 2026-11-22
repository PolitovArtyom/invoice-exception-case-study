using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Storage;

namespace InvoiceDesk.Core.Cases;

public sealed record CaseView(
    CaseMetadata Case,
    int CurrentRevision,
    string Stage,
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
    string ReadingOrigin
);

public sealed record CaseSummary(
    string CaseId,
    string Origin,
    string? Number,
    string? Supplier,
    decimal? DeclaredTotal,
    string? Currency,
    Decision? Decision,
    string Stage,
    string? ReadingOrigin
);

public sealed record AssessmentResponse(
    string AssessmentId,
    InvoiceResult Result,
    bool InputsUnchanged
);
