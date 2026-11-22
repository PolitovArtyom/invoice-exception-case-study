namespace InvoiceDesk.Core.Model;

public sealed record RevisionAction(
    RevisionKind Kind,
    RevisionActor Actor,
    DateTimeOffset RecordedAt
);

public enum RevisionKind
{
    LoadCase,
    RegisterInvoice,
    SubmitCaseInputs,
}

public enum RevisionActor
{
    TrustedLoader,
    Agent,
    Human,
    Runner,
}
