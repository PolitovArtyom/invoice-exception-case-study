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
    public bool LoadCase(CaseMetadata metadata, CompanyData companyData, InvoiceReading? fixture)
    {
        using var transaction = repository.BeginUpdate();
        if (repository.FindForUpdate(metadata.CaseId) is not null)
            return false;

        var invoiceCase = InvoiceCase.FromMetadata(metadata);
        Apply(invoiceCase, null, companyData, fixture, null, "load_case", "trusted_loader");
        transaction.Commit();
        return true;
    }

    public AssessmentResponse RegisterInvoice(string caseId, InvoiceReading reading)
    {
        using var transaction = repository.BeginUpdate();
        var invoiceCase = repository.FindForUpdate(caseId)
            ?? throw new InvoiceValidationException("/case_id", "Case does not exist.");
        var current = repository.FindCurrentRevisionForUpdate(invoiceCase);
        var revision = Apply(
            invoiceCase,
            current,
            null,
            reading,
            null,
            "register_invoice",
            "agent"
        );
        transaction.Commit();
        return Response(revision.Number, revision.Result!, false);
    }

    public InvoiceResult SubmitCaseInputs(
        CaseMetadata metadata,
        int step,
        CompanyData? companyData,
        InvoiceReading? reading
    )
    {
        using var transaction = repository.BeginUpdate();
        var invoiceCase = repository.FindForUpdate(metadata.CaseId);
        var current = invoiceCase is null
            ? null
            : repository.FindCurrentRevisionForUpdate(invoiceCase);
        if (companyData is null && current is null)
            throw new InvoiceValidationException(
                "/context",
                "Company data is required before the first invoice."
            );
        if (reading is null && current?.Document is null)
            throw new InvoiceValidationException(
                "/document",
                "An invoice is required for the first result."
            );

        invoiceCase ??= InvoiceCase.FromMetadata(metadata);
        var revision = Apply(
            invoiceCase,
            current,
            companyData,
            reading,
            step,
            "submit_case_inputs",
            "runner"
        );
        transaction.Commit();
        return revision.Result!;
    }

    public AssessmentResponse AssessInvoice(string caseId)
    {
        var invoiceCase = repository.Find(caseId)
            ?? throw new InvoiceValidationException("/case_id", "Case does not exist.");
        var latest = repository.ReadRevision(caseId, invoiceCase.CurrentRevision)!;
        if (latest.Result is null)
            throw new InvoiceValidationException(
                "/case_id",
                "Register invoice facts before requesting an assessment."
            );

        return Response(latest.Number, latest.Result, true);
    }

    public CaseSummary[] List() =>
        repository.List().Select(item => Summarize(item.Case, item.Revision)).ToArray();

    public CaseView? Read(string caseId)
    {
        var invoiceCase = repository.Find(caseId);
        if (invoiceCase is null)
            return null;

        var revision = repository.ReadRevision(caseId, invoiceCase.CurrentRevision)!;
        return new(
            invoiceCase.Metadata,
            invoiceCase.CurrentRevision,
            Stage(revision),
            View(revision)
        );
    }

    public CaseView? ReadAssessment(string caseId, int revisionNumber)
    {
        var invoiceCase = repository.Find(caseId);
        if (invoiceCase is null)
            return null;

        var revision = repository.ReadRevision(caseId, revisionNumber);
        return revision?.Result is null
            ? null
            : new(
                invoiceCase.Metadata,
                invoiceCase.CurrentRevision,
                Stage(revision),
                View(revision)
            );
    }

    public RevisionView[]? ReadHistory(string caseId) =>
        repository.Find(caseId) is null
            ? null
            : repository.ReadHistory(caseId).Select(View).ToArray();

    public CompanyData? ReadCompanyData(string caseId)
    {
        var invoiceCase = repository.Find(caseId);
        return invoiceCase is null
            ? null
            : repository.ReadRevision(caseId, invoiceCase.CurrentRevision)?.Snapshot.Data;
    }

    public bool Exists(string caseId) => repository.Find(caseId) is not null;

    private static string AssessmentId(int revision) =>
        revision.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static RevisionView View(CaseRevision revision)
    {
        var document = revision.Document;
        return new(
            revision.Number,
            revision.Snapshot.ContextVersion,
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
            revision.Result
        );
    }

    private static CaseSummary Summarize(InvoiceCase invoiceCase, CaseRevision revision)
    {
        var facts = revision.Document?.Facts;
        Decision? decision = revision.Result?.Decision;
        return new(
            invoiceCase.Id,
            invoiceCase.Origin,
            facts?.Number,
            facts?.Supplier.LegalName,
            facts?.DeclaredTotal,
            facts?.Currency,
            decision,
            Stage(revision),
            revision.Document?.ReadingOrigin
        );
    }

    private static string Stage(CaseRevision revision)
    {
        if (revision.Document is null)
            return "not_read";

        return revision.Result?.Decision == Decision.Clear
            ? "ready_for_accounting"
            : "needs_attention";
    }

    private CaseRevision Apply(
        InvoiceCase invoiceCase,
        CaseRevision? current,
        CompanyData? companyData,
        InvoiceReading? reading,
        int? step,
        string kind,
        string actor
    )
    {
        var inputs = ResolveInputs(invoiceCase, current, companyData, reading);
        var result = inputs.Facts is null
            ? null
            : evaluator.Evaluate(
                invoiceCase.Id,
                step ?? inputs.Number,
                inputs,
                repository.ReadEarlierImports(invoiceCase.Id, inputs.Document?.Id)
            );

        return repository.Append(invoiceCase, inputs, result, new(kind, actor, clock.GetUtcNow()));
    }

    private static RevisionInputs ResolveInputs(
        InvoiceCase invoiceCase,
        CaseRevision? current,
        CompanyData? companyData,
        InvoiceReading? reading
    )
    {
        int number = invoiceCase.CurrentRevision + 1;
        var snapshot = companyData is null
            ? current!.Snapshot
            : new CompanyDataSnapshot
            {
                CaseId = invoiceCase.Id,
                ContextVersion = companyData.ContextVersion,
                Data = companyData,
            };
        var document = reading is null
            ? current?.Document
            : new InvoiceDocument
            {
                CaseId = invoiceCase.Id,
                ImportRevision = number,
                ContentSha256 = reading.ContentSha256,
                Source = reading.Source,
                Facts = reading.Facts,
                ReadingOrigin = reading.ReadingOrigin,
            };
        return new(number, snapshot, document);
    }

    private static AssessmentResponse Response(
        int revision,
        InvoiceResult result,
        bool unchanged
    ) => new(AssessmentId(revision), result, unchanged);
}
