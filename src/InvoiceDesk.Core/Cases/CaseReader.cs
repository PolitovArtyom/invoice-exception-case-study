using InvoiceDesk.Core.Cases.HumanResolution;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.HumanResolution;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Matching;
using InvoiceDesk.Core.Rules.Parties;
using InvoiceDesk.Core.Storage;

namespace InvoiceDesk.Core.Cases;

public sealed class CaseReader(CaseRepository repository)
{
    public CaseList List()
    {
        var cases = repository
            .List()
            .Select(item =>
                Summarize(item.Case, item.Revision, item.Acceptance, item.InputsChanged)
            )
            .ToArray();
        return new(
            new(
                cases.Count(item => item.Tab == CaseTab.NeedsAttention),
                cases.Count(item => item.Tab == CaseTab.ReadyForAccounting),
                cases.Count(item => item.Tab == CaseTab.Closed)
            ),
            cases
        );
    }

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
        return revision?.Result is null
            ? null
            : ReadView(
                invoiceCase,
                revision,
                historical: revisionNumber != invoiceCase.CurrentRevision
            );
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
        var acceptance = historical
            ? revision.Acceptance
            : repository.ReadCurrentAcceptance(invoiceCase, revision);
        var correctable =
            !historical && saved.Document is { } active && !saved.IsDuplicateAttempt
                ? FactCorrections.Fields(active.Facts)
                : [];
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
            saved.Document?.Facts,
            !superseded && !inputsChanged
        );
        bool showDuplicateReading =
            saved.DuplicateAttempt is not null
            && (saved.Document is null || historical && saved.IsDuplicateAttempt);

        return new(
            invoiceCase.Metadata,
            invoiceCase.CurrentRevision,
            Stage(revision, acceptance, !historical && inputsChanged),
            saved,
            inputsChanged,
            latestDuplicateRevision,
            superseded,
            showDuplicateReading,
            proposals,
            effective,
            openOrders,
            CaseIssues.For(revision.Result, saved.Document?.Facts),
            acceptance,
            !historical && ResolutionEligibility.CanAccept(revision, acceptance, inputsChanged),
            correctable.Length > 0,
            correctable
        );
    }

    private static (
        ProposalView[] Proposals,
        string? Effective,
        PurchaseOrder[] OpenOrders
    ) ReadProposals(CaseRevision revision, InvoiceFacts? facts, bool canReview)
    {
        if (facts is null)
            return ([], null, []);

        var data = revision.Snapshot.Data;
        var parties = InvoiceParties.Resolve(facts, data);
        var registered = revision.Proposals?.Items ?? [];
        var options =
            registered.Length == 0
                ? []
                : new ProposalMatching(
                    facts,
                    revision.Document!.ImportRevision,
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

    public CompanyData? ReadCompanyData(string caseId, int? revisionNumber = null)
    {
        int? number = revisionNumber ?? repository.Find(caseId)?.CurrentRevision;
        return number is { } revision ? repository.ReadCompanyData(caseId, revision) : null;
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
                    revision.ReadFacts()!,
                    document.ReadingOrigin
                ),
            revision.Result is null ? null : AssessmentId(revision.Number),
            revision.Result,
            revision.OriginalInvoice,
            revision.DuplicateAttempt,
            revision.Acceptance,
            revision.Correction
        );
    }

    private static CaseSummary Summarize(
        InvoiceCase invoiceCase,
        CaseRevision revision,
        DiscrepancyAcceptance? acceptance,
        bool inputsChanged
    )
    {
        var facts = revision.ReadFacts() ?? revision.DuplicateAttempt?.Reading.Facts;
        var stage = Stage(revision, acceptance, inputsChanged);
        var mainIssue = CaseIssues.MainIssue(revision.Result);
        return new(
            invoiceCase.Id,
            invoiceCase.Origin,
            facts?.Number,
            facts?.Supplier.LegalName,
            facts?.IssueDate,
            facts?.DeclaredTotal,
            facts?.Currency,
            revision.Result?.Decision,
            stage,
            Tab(stage),
            stage == CaseStage.Closed ? revision.OriginalInvoice?.DocumentId : null,
            mainIssue?.Check.Code,
            mainIssue?.Owner,
            revision.Result?.InputRevision,
            revision.Result?.ContextVersion,
            acceptance?.Person
        );
    }

    private static CaseTab Tab(CaseStage stage) =>
        stage switch
        {
            CaseStage.ReadyForAccounting or CaseStage.AcceptedWithDiscrepancy =>
                CaseTab.ReadyForAccounting,
            CaseStage.Closed => CaseTab.Closed,
            _ => CaseTab.NeedsAttention,
        };

    private static CaseStage Stage(
        CaseRevision revision,
        DiscrepancyAcceptance? acceptance,
        bool inputsChanged
    )
    {
        if (acceptance is not null && !inputsChanged)
            return CaseStage.AcceptedWithDiscrepancy;
        if (revision.Document is null)
            return revision.DuplicateAttempt is null ? CaseStage.NotRead : CaseStage.Closed;

        return !inputsChanged && revision.Result?.Decision == Decision.Clear
            ? CaseStage.ReadyForAccounting
            : CaseStage.NeedsAttention;
    }
}
