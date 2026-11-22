using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Storage;
using InvoiceDesk.Core.Validation;

namespace InvoiceDesk.Core.UseCases;

public sealed class CaseUpdates(CaseRepository repository, InvoiceEvaluator evaluator, TimeProvider clock)
{
    public bool LoadCase(CaseMetadata metadata, CompanySnapshot company, InvoiceReading? fixture)
    {
        using var transaction = repository.BeginUpdate();
        if (repository.FindForUpdate(metadata.CaseId) is not null)
            return false;

        var invoiceCase = repository.Add(metadata);
        Apply(invoiceCase, null, company, fixture, null, "load_case", "trusted_loader");
        transaction.Commit();
        return true;
    }

    public AssessmentResponse RegisterInvoice(string caseId, InvoiceReading reading)
    {
        using var transaction = repository.BeginUpdate();
        var invoiceCase = repository.FindForUpdate(caseId) ??
            throw new InvoiceValidationException("/case_id", "Case does not exist.");
        var current = repository.ReadRevision(caseId, invoiceCase.CurrentRevision);
        var revision = Apply(invoiceCase, current, null, reading, null, "register_invoice", "agent");
        transaction.Commit();
        return Response(revision.Number, revision.Result!, false);
    }

    public InvoiceResult SubmitCaseInputs(CaseMetadata metadata, int step, CompanySnapshot? company, InvoiceReading? reading)
    {
        using var transaction = repository.BeginUpdate();
        var invoiceCase = repository.FindForUpdate(metadata.CaseId);
        if (invoiceCase is not null && invoiceCase.AsOf != metadata.AsOf)
            throw new InvoiceValidationException("/as_of", "Must match the saved case date.");

        var current = invoiceCase is null ? null : repository.ReadRevision(metadata.CaseId, invoiceCase.CurrentRevision);
        if (company is null && current is null)
            throw new InvoiceValidationException("/context", "Company data is required before the first invoice.");
        if (reading is null && current?.Document is null)
            throw new InvoiceValidationException("/document", "An invoice is required for the first result.");

        invoiceCase ??= repository.Add(metadata);
        var revision = Apply(invoiceCase, current, company, reading, step, "submit_case_inputs", "runner");
        transaction.Commit();
        return revision.Result!;
    }

    public AssessmentResponse AssessInvoice(string caseId)
    {
        var invoiceCase = repository.Find(caseId) ??
            throw new InvoiceValidationException("/case_id", "Case does not exist.");
        var latest = repository.ReadRevision(caseId, invoiceCase.CurrentRevision)!;
        if (latest.Result is null)
            throw new InvoiceValidationException("/case_id", "Register invoice facts before requesting an assessment.");

        return Response(latest.Number, latest.Result, true);
    }

    private CaseRevision Apply(InvoiceCase invoiceCase, CaseRevision? current, CompanySnapshot? company,
        InvoiceReading? reading, int? step, string kind, string actor)
    {
        int number = invoiceCase.CurrentRevision + 1;
        var facts = reading?.Facts ?? current?.Document?.Facts;
        InvoiceResult? result = null;
        if (facts is not null)
        {
            long? exceptDocument = reading is null ? current?.DocumentId : null;
            result = evaluator.Evaluate(new(invoiceCase.Id, step ?? number, number, facts,
                company ?? current!.Snapshot.Data, repository.ReadEarlierImports(invoiceCase.Id, exceptDocument)));
        }

        return repository.Append(invoiceCase, company, current?.Snapshot, reading, current?.Document, result,
            new(kind, actor, clock.GetUtcNow()));
    }

    private static AssessmentResponse Response(int revision, InvoiceResult result, bool unchanged) =>
        new(CaseQueries.AssessmentId(revision), result, unchanged);
}
