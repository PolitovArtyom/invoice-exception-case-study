using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Cases;

public sealed record CaseView(
    CaseMetadata Case,
    int CurrentRevision,
    CaseStage Stage,
    RevisionView Revision,
    bool InputsChanged,
    int? LatestDuplicateRevision,
    bool Superseded,
    bool ShowDuplicateReading,
    ProposalView[] Proposals,
    string? EffectiveProposalId,
    PurchaseOrder[] OpenSupplierOrders
);

public sealed record RevisionView(
    int Number,
    string ContextVersion,
    string PolicyVersion,
    RevisionAction Action,
    DocumentView? Document,
    string? AssessmentId,
    InvoiceResult? Result,
    OriginalInvoice? OriginalInvoice,
    DuplicateAttempt? DuplicateAttempt
)
{
    public bool IsDuplicateAttempt =>
        DuplicateAttempt is not null
        && (
            Action.DocumentSubmitted
            || Action.Kind is RevisionKind.RegisterInvoice or RevisionKind.LoadCase
        );
}

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
    CaseStage Stage,
    string? OriginalDocumentId
);

public sealed record AssessmentResponse(
    string? AssessmentId,
    InvoiceResult Result,
    bool InputsUnchanged
);

public enum CaseStage
{
    NotRead,
    NeedsAttention,
    ReadyForAccounting,
    Closed,
}
