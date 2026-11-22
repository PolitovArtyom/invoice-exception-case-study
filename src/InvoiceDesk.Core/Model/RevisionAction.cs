namespace InvoiceDesk.Core.Model;

public sealed record RevisionAction(
    RevisionKind Kind,
    RevisionActor Actor,
    DateTimeOffset RecordedAt,
    string[]? ProposalIds = null,
    StepConfirmation[]? Confirmations = null,
    bool CompanyDataSubmitted = false,
    bool DocumentSubmitted = false
);

public enum RevisionKind
{
    LoadCase,
    RegisterInvoice,
    SubmitCaseInputs,
    ProposeMatch,
    ConfirmProposal,
    ReplaceCompanyData,
}

public enum RevisionActor
{
    TrustedLoader,
    Agent,
    Human,
    Runner,
}
