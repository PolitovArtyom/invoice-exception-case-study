using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Storage;

namespace InvoiceDesk.Core.UseCases;

public sealed class CaseQueries(CaseRepository repository)
{
    public CaseSummary[] List() => repository.List().Select(item => Summarize(item.Case, item.Revision)).ToArray();

    public CaseView? Read(string caseId)
    {
        var invoiceCase = repository.Find(caseId);
        if (invoiceCase is null)
            return null;

        var revision = repository.ReadRevision(caseId, invoiceCase.CurrentRevision)!;
        return new(invoiceCase.Metadata, invoiceCase.CurrentRevision, View(revision));
    }

    public CaseView? ReadAssessment(string caseId, string assessmentId)
    {
        var invoiceCase = repository.Find(caseId);
        if (invoiceCase is null || !int.TryParse(assessmentId, out int number) || AssessmentId(number) != assessmentId)
            return null;

        var revision = repository.ReadRevision(caseId, number);
        return revision?.Result is null ? null : new(invoiceCase.Metadata, invoiceCase.CurrentRevision, View(revision));
    }

    public RevisionView[]? ReadHistory(string caseId) =>
        repository.Find(caseId) is null ? null : repository.ReadHistory(caseId).Select(View).ToArray();

    public CompanySnapshot? ReadCompanyData(string caseId)
    {
        var invoiceCase = repository.Find(caseId);
        return invoiceCase is null ? null : repository.ReadRevision(caseId, invoiceCase.CurrentRevision)?.Snapshot.Data;
    }

    public bool Exists(string caseId) => repository.Find(caseId) is not null;

    internal static string AssessmentId(int revision) => revision.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static RevisionView View(CaseRevision revision)
    {
        var document = revision.Document;
        return new(revision.Number, revision.Snapshot.ContextVersion, revision.Action,
            document is null ? null : new(document.Id, document.ContentSha256, document.Source, document.Facts, document.ReadingOrigin),
            revision.Result is null ? null : AssessmentId(revision.Number), revision.Result);
    }

    private static CaseSummary Summarize(InvoiceCase invoiceCase, CaseRevision revision)
    {
        var facts = revision.Document?.Facts;
        string? decision = revision.Result?.Decision;
        string stage = decision == "clear" ? "Ready for accounting" : "Needs attention";
        return new(invoiceCase.Id, invoiceCase.Origin, facts?.Number, facts?.Supplier.LegalName, facts?.Buyer.LegalName,
            facts?.DeclaredTotal, facts?.Currency, decision, stage, revision.Document?.ReadingOrigin);
    }
}
