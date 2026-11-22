namespace InvoiceDesk.Core.Model.Proposals;

public sealed record RegisteredProposal(
    StepProposal Proposal,
    int ImportRevision,
    ProposalConfirmation? Confirmation
);

public sealed record ProposalConfirmation(string ConfirmedBy, int Sequence);
