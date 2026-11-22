using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Matching;
using InvoiceDesk.Core.Rules.Parties;
using InvoiceDesk.Core.Storage;
using InvoiceDesk.Core.Validation;

namespace InvoiceDesk.Core.Cases;

public sealed class CaseReader(CaseRepository repository)
{
    public AssessmentResponse AssessInvoice(string caseId)
    {
        var invoiceCase = repository.Find(caseId)
            ?? throw new InvoiceValidationException("/case_id", "Case does not exist.");
        var latest = repository.ReadAssessmentOrLatestRevision(invoiceCase);
        if (latest.Result is null)
            throw new InvoiceValidationException(
                "/case_id",
                "Register invoice facts before requesting an assessment."
            );

        return new(
            AssessmentId(latest.Number),
            latest.Result,
            !repository.InputsChanged(invoiceCase, latest)
        );
    }

    public CaseSummary[] List() =>
        repository
            .List()
            .Select(item => Summarize(item.Case, item.Revision, item.InputsChanged))
            .ToArray();

    public CaseView? Read(string caseId)
    {
        var invoiceCase = repository.Find(caseId);
        if (invoiceCase is null)
            return null;

        var revision = repository.ReadAssessmentOrLatestRevision(invoiceCase);
        return ReadView(invoiceCase, revision);
    }

    public CaseView? ReadAssessment(string caseId, int revisionNumber)
    {
        var invoiceCase = repository.Find(caseId);
        if (invoiceCase is null)
            return null;

        var revision = repository.ReadRevision(caseId, revisionNumber);
        return revision?.Result is null ? null : ReadView(invoiceCase, revision, historical: true);
    }

    public CaseView? ReadRevisionView(string caseId, int revisionNumber)
    {
        var invoiceCase = repository.Find(caseId);
        var revision = repository.ReadRevision(caseId, revisionNumber);
        return invoiceCase is null || revision is null
            ? null
            : ReadView(invoiceCase, revision, historical: true);
    }

    private CaseView ReadView(
        InvoiceCase invoiceCase,
        CaseRevision revision,
        bool historical = false
    )
    {
        bool superseded = historical && revision.Number != invoiceCase.CurrentRevision;
        var saved = View(revision);
        bool inputsChanged = repository.InputsChanged(invoiceCase, revision);
        int? latestDuplicateRevision =
            !historical
            && revision.Document is { } document
            && invoiceCase.CurrentRevision > document.ImportRevision
                ? repository.ReadLatestDuplicateRevision(
                    invoiceCase.Id,
                    document.Id,
                    invoiceCase.CurrentRevision
                )
                : null;
        var (proposals, effective, openOrders) = ReadProposals(
            revision,
            !superseded && !inputsChanged
        );
        bool showDuplicateReading =
            saved.DuplicateAttempt is not null
            && (saved.Document is null || historical && saved.IsDuplicateAttempt);

        return new(
            invoiceCase.Metadata,
            invoiceCase.CurrentRevision,
            Stage(revision, !historical && inputsChanged),
            saved,
            inputsChanged,
            latestDuplicateRevision,
            superseded,
            showDuplicateReading,
            proposals,
            effective,
            openOrders
        );
    }

    private static (
        ProposalView[] Proposals,
        string? Effective,
        PurchaseOrder[] OpenOrders
    ) ReadProposals(CaseRevision revision, bool canReview)
    {
        if (revision.Document is not { } document)
            return ([], null, []);

        var data = revision.Snapshot.Data;
        var parties = InvoiceParties.Resolve(document.Facts, data);
        var registered = revision.Proposals?.Items ?? [];
        var options =
            registered.Length == 0
                ? []
                : new ProposalMatching(
                    document.Facts,
                    document.ImportRevision,
                    data,
                    parties
                ).Options(registered);
        var effective = ProposalMatching.Effective(options)?.Proposal.Proposal.ProposalId;
        bool canPreview = canReview && revision.Result is not null;
        bool clearDeterministically =
            revision.Result
                is { Decision: Decision.Clear, OrderMatchSource: MatchSource.Deterministic } result
            && result.Lines.All(line => line.MatchSource == MatchSource.Deterministic);

        var proposals = options
            .Select(option =>
            {
                var supplier = data.Suppliers.Suppliers.FirstOrDefault(supplier =>
                    supplier.SupplierId == option.Order?.SupplierId
                );
                bool offerPreview =
                    canPreview && option.Applicable && option.Proposal.Confirmation is null;
                return new ProposalView(
                    option.Proposal,
                    option.Order,
                    supplier?.LegalName,
                    option.Applicable,
                    offerPreview,
                    offerPreview && !clearDeterministically
                );
            })
            .ToArray();
        var openOrders = data
            .PurchaseOrders.Orders.Where(order =>
                order.Status == OrderStatus.Open && order.SupplierId == parties.Supplier?.SupplierId
            )
            .ToArray();

        return (proposals, effective, openOrders);
    }

    public RevisionView[]? ReadHistory(string caseId) =>
        repository.Find(caseId) is null
            ? null
            : repository.ReadHistory(caseId).Select(View).ToArray();

    public InvoiceSource? ReadDocumentSource(string caseId, long reference) =>
        repository.ReadDocumentSource(caseId, reference);

    public bool Exists(string caseId) => repository.Find(caseId) is not null;

    public IReadOnlyList<string> ReadReferencePdfPaths(string caseId) =>
        repository.Find(caseId)?.Sources.ReferencePdfPaths ?? [];

    public CaseMetadata? ReadMetadata(string caseId) => repository.Find(caseId)?.Metadata;

    public RevisionView? ReadRevision(string caseId, int number) =>
        repository.ReadRevision(caseId, number) is { } revision ? View(revision) : null;

    public CompanyData? ReadCompanyData(string caseId)
    {
        var invoiceCase = repository.Find(caseId);
        return invoiceCase is null
            ? null
            : repository.ReadRevision(caseId, invoiceCase.CurrentRevision)?.Snapshot.Data;
    }

    internal static string AssessmentId(int revision) =>
        revision.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static RevisionView View(CaseRevision revision)
    {
        var document = revision.Document;
        return new(
            revision.Number,
            revision.Snapshot.Data.ContextVersion,
            revision.Result?.PolicyVersion ?? CaseMetadata.PolicyVersion,
            revision.Action,
            document is null
                ? null
                : new(
                    document.Id,
                    document.ContentSha256,
                    document.Source,
                    document.Facts,
                    document.ReadingOrigin
                ),
            revision.Result is null ? null : AssessmentId(revision.Number),
            revision.Result,
            revision.OriginalInvoice,
            revision.DuplicateAttempt
        );
    }

    private static CaseSummary Summarize(
        InvoiceCase invoiceCase,
        CaseRevision revision,
        bool inputsChanged
    )
    {
        var facts = revision.Document?.Facts ?? revision.DuplicateAttempt?.Reading.Facts;
        var stage = Stage(revision, inputsChanged);
        return new(
            invoiceCase.Id,
            invoiceCase.Origin,
            facts?.Number,
            facts?.Supplier.LegalName,
            facts?.DeclaredTotal,
            facts?.Currency,
            revision.Result?.Decision,
            stage,
            stage == CaseStage.Closed ? revision.OriginalInvoice?.DocumentId : null
        );
    }

    private static CaseStage Stage(CaseRevision revision, bool inputsChanged)
    {
        if (revision.Document is null)
            return revision.DuplicateAttempt is null ? CaseStage.NotRead : CaseStage.Closed;

        return !inputsChanged && revision.Result?.Decision == Decision.Clear
            ? CaseStage.ReadyForAccounting
            : CaseStage.NeedsAttention;
    }
}
