using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Rules.Duplicates;
using InvoiceDesk.Core.Rules.Parties;
using InvoiceDesk.Core.Storage;
using InvoiceDesk.Core.Validation;

namespace InvoiceDesk.Core.Cases;

public sealed class CaseService(
    CaseRepository repository,
    InvoiceEvaluator evaluator,
    TimeProvider clock
)
{
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
            companyData,
            fixture,
            null,
            RevisionKind.LoadCase,
            RevisionActor.TrustedLoader
        );
        transaction.Commit();
        return true;
    }

    public AssessmentResponse RegisterInvoice(string caseId, InvoiceReading reading)
    {
        using var transaction = repository.BeginUpdate();
        var invoiceCase = repository.FindForUpdate(caseId)
            ?? throw new InvoiceValidationException("/case_id", "Case does not exist.");
        var current = repository.FindCurrentRevisionForUpdate(invoiceCase);
        var result = Apply(
            invoiceCase,
            current,
            null,
            reading,
            null,
            RevisionKind.RegisterInvoice,
            RevisionActor.Agent
        );
        transaction.Commit();
        return Response(result!, false);
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
        var result = Apply(
            invoiceCase,
            current,
            companyData,
            reading,
            step,
            RevisionKind.SubmitCaseInputs,
            RevisionActor.Runner
        );
        transaction.Commit();
        return result!;
    }

    private InvoiceResult? Apply(
        InvoiceCase invoiceCase,
        CaseRevision? current,
        CompanyData? companyData,
        InvoiceReading? reading,
        int? step,
        RevisionKind kind,
        RevisionActor actor
    )
    {
        var inputs = ResolveInputs(invoiceCase, current, companyData, reading);
        InvoiceResult? result = null;
        InvoiceImportMatch? importMatch = null;
        if (inputs.Document is { } document)
        {
            var parties = InvoiceParties.Resolve(document.Facts, inputs.CompanyData);
            importMatch = ClassifyImport(document, inputs.CompanyData, parties, reading is null);
            result = evaluator.Evaluate(
                invoiceCase.Id,
                step ?? inputs.Number,
                inputs,
                parties,
                importMatch
            );
        }

        bool isDuplicate = result?.ImportDisposition == ImportDisposition.Duplicate;
        var duplicate = isDuplicate && reading is not null ? new DuplicateAttempt(reading) : null;
        repository.Append(
            invoiceCase,
            isDuplicate ? inputs with { Document = current?.Document } : inputs,
            isDuplicate ? null : result,
            new(kind, actor, clock.GetUtcNow()),
            importMatch?.Original,
            duplicate
        );
        return result;
    }

    private InvoiceImportMatch? ClassifyImport(
        InvoiceDocument document,
        CompanyData companyData,
        InvoiceParties parties,
        bool isReassessment
    )
    {
        if (parties.Key is not { } key)
            return null;

        return InvoiceImportMatch.Find(
            document.Facts,
            key,
            companyData,
            repository.ReadEarlierImports(document.CaseId),
            isReassessment ? document.ContentSha256 : null
        );
    }

    private RevisionInputs ResolveInputs(
        InvoiceCase invoiceCase,
        CaseRevision? current,
        CompanyData? companyData,
        InvoiceReading? reading
    )
    {
        int number = invoiceCase.CurrentRevision + 1;
        var snapshot = companyData is null
            ? current!.Snapshot
            : repository.ResolveSnapshot(invoiceCase.Id, companyData);
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

    private static AssessmentResponse Response(InvoiceResult result, bool unchanged) =>
        new(
            result.Decision is null
                ? null
                : result.InputRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
            result,
            unchanged
        );
}
