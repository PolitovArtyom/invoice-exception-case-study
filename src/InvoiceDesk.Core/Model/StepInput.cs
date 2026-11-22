namespace InvoiceDesk.Core.Model;

public sealed record StepInput(
    string CaseId,
    DateOnly AsOf,
    int Step,
    CompanyData.CompanyData? Context,
    StepDocument? Document,
    StepProposal[]? Proposals,
    StepConfirmation[]? Confirmations
);

public sealed record StepDocument(string ContentSha256, InvoiceFacts Facts);

public sealed record StepProposal(
    string ProposalId,
    RevisionActor Actor,
    string DocumentId,
    string OrderId,
    ProposedLineMatch[] LineMatches
);

public sealed record ProposedLineMatch(string DocumentLineId, string OrderLineId);

public sealed record StepConfirmation(string ProposalId, RevisionActor Actor, string ConfirmedBy);
