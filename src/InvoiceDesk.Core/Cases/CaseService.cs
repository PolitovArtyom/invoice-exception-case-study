using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Storage;
using InvoiceDesk.Core.Validation;

namespace InvoiceDesk.Core.Cases;

public sealed class CaseService(
    CaseRepository repository,
    InvoiceEvaluator evaluator,
    TimeProvider clock
)
{
    private readonly CaseAssessmentCalculator _assessmentCalculator = new(repository, evaluator);

    public bool LoadCase(
        CaseMetadata metadata,
        CompanyData companyData,
        InvoiceReading? fixture,
        IReadOnlyList<string>? referencePdfPaths = null
    )
    {
        using var transaction = repository.BeginUpdate();
        if (repository.FindForUpdate(metadata.CaseId) is not null)
            return false;

        var invoiceCase = InvoiceCase.FromMetadata(metadata, referencePdfPaths);
        Apply(
            invoiceCase,
            null,
            new(companyData, fixture),
            RevisionKind.LoadCase,
            RevisionActor.TrustedLoader
        );
        transaction.Commit();
        return true;
    }

    public AssessmentResponse RegisterInvoice(string caseId, InvoiceReading reading) =>
        Update(caseId, new(Reading: reading), RevisionKind.RegisterInvoice, RevisionActor.Agent)!;

    public AssessmentResponse ProposeMatch(string caseId, StepProposal proposal) =>
        Update(caseId, new(Proposals: [proposal]), RevisionKind.ProposeMatch, RevisionActor.Agent)!;

    public AssessmentResponse? ReplaceCompanyData(string caseId, CompanyData companyData)
    {
        IntakeValidation.ValidateCompanyData(companyData, "/context");
        return Update(
            caseId,
            new(CompanyData: companyData),
            RevisionKind.ReplaceCompanyData,
            RevisionActor.TrustedLoader
        );
    }

    public AssessmentResponse ConfirmProposal(string caseId, ConfirmationForm form)
    {
        if (string.IsNullOrWhiteSpace(form.ConfirmedBy) || form.ConfirmedBy.Length > 128)
            throw new InvoiceValidationException(
                "/confirmed_by",
                "Enter a name of up to 128 characters."
            );
        if (!form.SupportingDocumentsReviewed)
            throw new InvoiceValidationException(
                "/supporting_documents_reviewed",
                "Acknowledge reviewing the delivery documents."
            );

        return Update(
            caseId,
            PageConfirmation(form.ProposalId, form.ConfirmedBy.Trim(), form.ExpectedRevision),
            RevisionKind.ConfirmProposal,
            RevisionActor.Human
        )!;
    }

    public ConfirmationPreview? PreviewConfirmation(string caseId, string proposalId)
    {
        if (repository.Find(caseId) is not { } invoiceCase)
            return null;

        var current = repository.ReadRevision(caseId, invoiceCase.CurrentRevision)!;
        if (current.Proposals?.Items.Any(item => item.Proposal.ProposalId == proposalId) != true)
            return null;

        var computed = _assessmentCalculator.Compute(
            invoiceCase,
            current,
            PageConfirmation(proposalId, "", current.Number)
        );
        return new(current.Number, proposalId, computed.Result!);
    }

    public InvoiceResult SubmitCaseInputs(StepInput step, InvoiceReading? reading)
    {
        using var transaction = repository.BeginUpdate();
        var invoiceCase = repository.FindForUpdate(step.CaseId);
        var current = invoiceCase is null
            ? null
            : repository.FindCurrentRevisionForUpdate(invoiceCase);
        if (step.Context is null && current is null)
            throw new InvoiceValidationException(
                "/context",
                "Company data is required before the first invoice."
            );
        if (reading is null && current is null)
            throw new InvoiceValidationException(
                "/document",
                "An invoice is required for the first result."
            );

        invoiceCase ??= InvoiceCase.FromMetadata(
            new(step.CaseId, CaseOrigin.Assignment, step.AsOf, [])
        );
        var result = Apply(
            invoiceCase,
            current,
            new(step.Context, reading, step.Proposals, step.Confirmations, step.Step),
            RevisionKind.SubmitCaseInputs,
            RevisionActor.Runner
        );
        transaction.Commit();
        return result!;
    }

    private AssessmentResponse? Update(
        string caseId,
        CaseSubmission submission,
        RevisionKind kind,
        RevisionActor actor
    )
    {
        using var transaction = repository.BeginUpdate();
        var invoiceCase = repository.FindForUpdate(caseId)
            ?? throw new InvoiceValidationException("/case_id", "Case does not exist.");
        var current = repository.FindCurrentRevisionForUpdate(invoiceCase)!;
        if (submission.ExpectedRevision is { } expected && expected != current.Number)
            throw new InvoiceValidationException(
                "/expected_revision",
                "The case changed. Open the latest revision and review the proposal again."
            );

        var result = Apply(invoiceCase, current, submission, kind, actor);
        transaction.Commit();
        return result is null
            ? null
            : new(
                result.Decision is null ? null : CaseReader.AssessmentId(result.InputRevision),
                result,
                false
            );
    }

    private InvoiceResult? Apply(
        InvoiceCase invoiceCase,
        CaseRevision? current,
        CaseSubmission submission,
        RevisionKind kind,
        RevisionActor actor
    )
    {
        var computed = _assessmentCalculator.Compute(invoiceCase, current, submission);
        var action = new RevisionAction(
            kind,
            actor,
            clock.GetUtcNow(),
            submission.Proposals?.Select(item => item.ProposalId).ToArray(),
            submission.Confirmations,
            submission.CompanyData is not null,
            submission.Reading is not null
        );
        repository.Append(
            invoiceCase,
            computed.Inputs,
            computed.Result?.Decision is null ? null : computed.Result,
            action,
            computed.Original,
            computed.Duplicate
        );
        return computed.Result;
    }

    private static CaseSubmission PageConfirmation(
        string proposalId,
        string name,
        int expectedRevision
    ) =>
        new(
            Confirmations: [new(proposalId, RevisionActor.Human, name)],
            ExpectedRevision: expectedRevision,
            RequireApplicableConfirmation: true
        );
}
