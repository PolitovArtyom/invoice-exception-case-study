using System.Text.Json;
using System.Text.Json.Nodes;
using InvoiceDesk.Core.Cases.HumanResolution;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.HumanResolution;
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

    public AssessmentResponse RecheckInvoice(string caseId, string? updateJson, RevisionActor actor)
    {
        using var transaction = repository.BeginUpdate();
        var invoiceCase = RequireCase(caseId);
        var current = repository.FindCurrentRevisionForUpdate(invoiceCase)!;
        if (current.Document is null)
            throw new InvoiceValidationException(
                "/case_id",
                current.DuplicateAttempt is null
                    ? "Register invoice facts before requesting an assessment."
                    : "Closed as a duplicate. There is no assessment to re-check."
            );

        var data = updateJson is null ? null : ReadCompanyDataUpdate(updateJson);
        if (data is null || InvoiceJson.SameValues(data, current.Snapshot.Data))
        {
            var saved = repository.ReadAssessmentOrLatestRevision(invoiceCase);
            if (!repository.InputsChanged(invoiceCase, saved))
                return new(CaseReader.AssessmentId(saved.Number), saved.Result!, true);

            data = null;
        }

        var result = Apply(
            invoiceCase,
            current,
            new(CompanyData: data),
            RevisionKind.RecheckInvoice,
            actor
        )!;
        transaction.Commit();
        return Response(result);
    }

    private static CompanyData ReadCompanyDataUpdate(string json)
    {
        JsonNode? data;
        try
        {
            data = JsonNode.Parse(
                json,
                documentOptions: new() { AllowDuplicateProperties = false }
            );
        }
        catch (JsonException)
        {
            throw new InvoiceValidationException(
                "/context",
                "Expected one JSON company data object."
            );
        }

        return IntakeValidation.ReadCompanyData(data, "/context");
    }

    public DiscrepancyAcceptance AcceptDiscrepancy(string caseId, AcceptanceForm form)
    {
        string person = PageActionValidation.RequirePerson(form.Person, "/person");
        string reason = PageActionValidation.RequireReason(form.Reason);
        using var transaction = repository.BeginUpdate();
        var invoiceCase = RequireCase(caseId);
        var current = repository.FindCurrentRevisionForUpdate(invoiceCase)!;
        PageActionValidation.RequireLatestRevision(form.ExpectedRevision, current);
        var assessed = repository.ReadAssessmentOrLatestRevision(invoiceCase);
        string assessmentId = CaseReader.AssessmentId(assessed.Number);
        if (
            form.AssessmentId != assessmentId
            || !ResolutionEligibility.CanAccept(
                assessed,
                current.Acceptance,
                repository.InputsChanged(invoiceCase, assessed)
            )
        )
            throw new InvoiceValidationException(
                "/assessment_id",
                "The current assessment is ineligible for discrepancy acceptance."
            );

        var acceptance = new DiscrepancyAcceptance(assessmentId, person, reason, clock.GetUtcNow());
        repository.AcceptDiscrepancy(current, acceptance);
        transaction.Commit();
        return acceptance;
    }

    public AssessmentResponse CorrectFact(string caseId, FactCorrectionForm form)
    {
        string person = PageActionValidation.RequirePerson(form.Person, "/person");
        string reason = PageActionValidation.RequireReason(form.Reason);
        using var transaction = repository.BeginUpdate();
        var invoiceCase = RequireCase(caseId);
        var current = repository.FindCurrentRevisionForUpdate(invoiceCase)!;
        PageActionValidation.RequireLatestRevision(form.ExpectedRevision, current);
        var facts = current.ReadFacts()
            ?? throw new InvoiceValidationException(
                "/field_path",
                "There is no active invoice reading to correct."
            );
        var (corrected, audit) = FactCorrections.Correct(facts, form, person, reason);
        var result = Apply(
            invoiceCase,
            current,
            new(Correction: new(corrected, audit)),
            RevisionKind.CorrectFact,
            RevisionActor.Human
        )!;
        transaction.Commit();
        return Response(result);
    }

    public AssessmentResponse ConfirmProposal(string caseId, ConfirmationForm form)
    {
        string person = PageActionValidation.RequirePerson(form.ConfirmedBy, "/confirmed_by");
        if (!form.SupportingDocumentsReviewed)
            throw new InvoiceValidationException(
                "/supporting_documents_reviewed",
                "Acknowledge reviewing the delivery documents."
            );

        return Update(
            caseId,
            PageConfirmation(form.ProposalId, person, form.ExpectedRevision),
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
        var invoiceCase = RequireCase(caseId);
        var current = repository.FindCurrentRevisionForUpdate(invoiceCase)!;
        if (submission.ExpectedRevision is { } expected)
            PageActionValidation.RequireLatestRevision(expected, current);

        var result = Apply(invoiceCase, current, submission, kind, actor);
        transaction.Commit();
        return result is null ? null : Response(result);
    }

    private InvoiceCase RequireCase(string caseId) =>
        repository.FindForUpdate(caseId)
        ?? throw new InvoiceValidationException("/case_id", "Case does not exist.");

    private static AssessmentResponse Response(InvoiceResult result) =>
        new(
            result.Decision is null ? null : CaseReader.AssessmentId(result.InputRevision),
            result,
            false
        );

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
            computed.Duplicate,
            submission.Correction?.Audit
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
