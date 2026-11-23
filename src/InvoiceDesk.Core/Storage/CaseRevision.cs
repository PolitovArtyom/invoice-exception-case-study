using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.HumanResolution;
using InvoiceDesk.Core.Model.Proposals;
using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Storage;

internal sealed class CaseRevision
{
    public required string CaseId { get; init; }

    public required int Number { get; init; }

    public required RevisionAction Action { get; init; }

    public InvoiceDocument? Document { get; init; }

    public long? DocumentId { get; init; }

    public required CompanyDataSnapshot Snapshot { get; init; }

    public long SnapshotId { get; init; }

    public OriginalInvoice? OriginalInvoice { get; init; }

    public DuplicateAttempt? DuplicateAttempt { get; init; }

    public StoredProposals? Proposals { get; init; }

    public InvoiceResult? Result { get; init; }

    public DiscrepancyAcceptance? Acceptance { get; set; }

    public InvoiceFacts? CorrectedFacts { get; init; }

    public FactCorrection? Correction { get; init; }

    public bool HasCorrectionConflict { get; init; }

    public InvoiceFacts? ReadFacts() => CorrectedFacts ?? Document?.Facts;
}

internal sealed record StoredProposals(RegisteredProposal[] Items);
