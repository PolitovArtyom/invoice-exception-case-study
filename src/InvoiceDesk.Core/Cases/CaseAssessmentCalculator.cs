using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.HumanResolution;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Rules.Duplicates;
using InvoiceDesk.Core.Rules.Matching;
using InvoiceDesk.Core.Rules.Parties;
using InvoiceDesk.Core.Storage;

namespace InvoiceDesk.Core.Cases;

internal sealed class CaseAssessmentCalculator(
    CaseRepository repository,
    InvoiceEvaluator evaluator
)
{
    public ComputedSubmission Compute(
        InvoiceCase invoiceCase,
        CaseRevision? current,
        CaseSubmission submission
    )
    {
        int revision = invoiceCase.CurrentRevision + 1;
        int step = submission.Step ?? revision;
        var snapshot = submission.CompanyData is null
            ? current!.Snapshot
            : repository.ResolveSnapshot(invoiceCase.Id, submission.CompanyData);
        var document = SubmittedDocument(
            invoiceCase.Id,
            revision,
            current?.Document,
            submission.Reading
        );
        var previousFacts = current?.ReadFacts();
        var facts = submission.Reading?.Facts ?? submission.Correction?.Facts ?? previousFacts;
        var parties = facts is null ? null : InvoiceParties.Resolve(facts, snapshot.Data);
        var import = document is null
            ? null
            : ClassifyImport(
                document,
                facts!,
                snapshot.Data,
                parties!,
                submission.Reading is null,
                submission.Correction is null ? null : previousFacts,
                current?.HasCorrectionConflict == true
            );
        var (active, duplicate, original) = HandleDuplicate(
            document,
            import,
            submission.Reading,
            current
        );

        var correctedFacts = submission.Correction?.Facts
            ?? (active == current?.Document ? current?.CorrectedFacts : null);
        bool hasCorrectionConflict = duplicate is not null
            ? current?.HasCorrectionConflict == true
            : import?.IsCorrectionConflict == true;
        var inputs = new RevisionInputs(
            revision,
            snapshot,
            active,
            current?.Proposals?.Items ?? [],
            correctedFacts,
            hasCorrectionConflict
        );
        var activeFacts = inputs.Facts;
        var activeParties =
            activeFacts is null ? null
            : active == document ? parties
            : InvoiceParties.Resolve(activeFacts, snapshot.Data);
        var matching = active is null
            ? null
            : new ProposalMatching(
                activeFacts!,
                active.ImportRevision,
                snapshot.Data,
                activeParties!
            );
        inputs = PrepareProposals(inputs, matching, submission);

        var result =
            duplicate is not null
                ? InvoiceEvaluator.ImportResult(
                    invoiceCase.Id,
                    step,
                    inputs,
                    duplicate.Reading.Facts.DocumentId,
                    ImportDisposition.Duplicate,
                    original
                )
            : matching is null ? null
            : evaluator.Evaluate(
                invoiceCase.Id,
                step,
                inputs,
                activeParties!,
                import,
                matching.Resolve(inputs.Proposals)
            );
        return new(inputs, result, original, duplicate);
    }

    private static (
        InvoiceDocument? Active,
        DuplicateAttempt? Attempt,
        OriginalInvoice? Original
    ) HandleDuplicate(
        InvoiceDocument? document,
        InvoiceImportMatch? import,
        InvoiceReading? reading,
        CaseRevision? current
    )
    {
        if (import is { IsDuplicate: true })
            return (current?.Document, new DuplicateAttempt(reading!), import.Original);
        if (document is null && current?.DuplicateAttempt is { } attempt)
            return (null, attempt, current.OriginalInvoice);

        return (document, null, import?.Original);
    }

    private static RevisionInputs PrepareProposals(
        RevisionInputs inputs,
        ProposalMatching? matching,
        CaseSubmission submission
    )
    {
        foreach (
            var (proposal, index) in (submission.Proposals ?? []).Select(
                (item, index) => (item, index)
            )
        )
        {
            string path = submission.Step is null ? "/proposal" : $"/proposals/{index}";
            inputs = inputs with
            {
                Proposals = ProposalInputs.Register(inputs, matching, proposal, path),
            };
        }

        foreach (
            var (confirmation, index) in (submission.Confirmations ?? []).Select(
                (item, index) => (item, index)
            )
        )
        {
            string path = submission.Step is null ? "" : $"/confirmations/{index}";
            inputs = inputs with
            {
                Proposals = ProposalInputs.Confirm(
                    inputs,
                    matching,
                    confirmation,
                    path,
                    submission.RequireApplicableConfirmation
                ),
            };
        }

        return inputs;
    }

    private InvoiceImportMatch? ClassifyImport(
        InvoiceDocument document,
        InvoiceFacts facts,
        CompanyData companyData,
        InvoiceParties parties,
        bool isReassessment,
        InvoiceFacts? previousFacts,
        bool hasCorrectionConflict
    )
    {
        if (parties.Key is not { } key)
            return null;

        var earlier = repository.ReadEarlierImports(document.CaseId);
        return InvoiceImportMatch.Find(
            facts,
            key,
            companyData,
            isReassessment
                ? earlier.Where(item => item.Original.DocumentReference != document.Id)
                : earlier,
            isReassessment ? document.ContentSha256 : null,
            previousFacts,
            hasCorrectionConflict
        );
    }

    private static InvoiceDocument? SubmittedDocument(
        string caseId,
        int revision,
        InvoiceDocument? current,
        InvoiceReading? reading
    ) =>
        reading is null
            ? current
            : new InvoiceDocument
            {
                CaseId = caseId,
                ImportRevision = revision,
                ContentSha256 = reading.ContentSha256,
                Source = reading.Source,
                Facts = reading.Facts,
                ReadingOrigin = reading.ReadingOrigin,
            };
}

internal sealed record ComputedSubmission(
    RevisionInputs Inputs,
    InvoiceResult? Result,
    OriginalInvoice? Original,
    DuplicateAttempt? Duplicate
);

internal sealed record CaseSubmission(
    CompanyData? CompanyData = null,
    InvoiceReading? Reading = null,
    StepProposal[]? Proposals = null,
    StepConfirmation[]? Confirmations = null,
    int? Step = null,
    int? ExpectedRevision = null,
    bool RequireApplicableConfirmation = false,
    CorrectedReading? Correction = null
);

internal sealed record CorrectedReading(InvoiceFacts Facts, FactCorrection Audit);
