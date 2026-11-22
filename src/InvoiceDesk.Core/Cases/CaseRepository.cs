using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace InvoiceDesk.Core.Cases;

public sealed class CaseRepository(InvoiceDatabase database)
{
    internal IDbContextTransaction BeginUpdate() => database.Database.BeginTransaction();

    internal InvoiceCase? Find(string caseId) =>
        database.Cases.AsNoTracking().SingleOrDefault(row => row.Id == caseId);

    internal InvoiceCase? FindForUpdate(string caseId) => database.Cases.Find(caseId);

    internal CaseRevision? FindCurrentRevisionForUpdate(InvoiceCase invoiceCase) =>
        RevisionQuery
            .AsTracking()
            .SingleOrDefault(row =>
                row.CaseId == invoiceCase.Id && row.Number == invoiceCase.CurrentRevision
            );

    internal (InvoiceCase Case, CaseRevision Revision)[] List() =>
        (
            from invoiceCase in database.Cases.AsNoTracking()
            join revision in RevisionQuery
                on new { CaseId = invoiceCase.Id, Number = invoiceCase.CurrentRevision } equals new
                {
                    revision.CaseId,
                    revision.Number,
                }
            orderby invoiceCase.Id
            select new { Case = invoiceCase, Revision = revision }
        )
            .AsEnumerable()
            .Select(item => (item.Case, item.Revision))
            .ToArray();

    internal CaseRevision? ReadRevision(string caseId, int number) =>
        RevisionQuery.SingleOrDefault(row => row.CaseId == caseId && row.Number == number);

    internal CaseRevision[] ReadHistory(string caseId) =>
        RevisionQuery
            .Where(row => row.CaseId == caseId)
            .OrderByDescending(row => row.Number)
            .ToArray();

    internal EarlierInvoice[] ReadEarlierImports(string caseId, long? exceptDocumentId) =>
        database
            .Revisions.AsNoTracking()
            .Where(row =>
                row.CaseId == caseId
                && row.Document != null
                && row.Document.ImportRevision == row.Number
                && row.DocumentId != exceptDocumentId
            )
            .OrderBy(row => row.Number)
            .Select(row => new EarlierInvoice(row.Document!.Facts, row.Snapshot.Data))
            .ToArray();

    internal CaseRevision Append(
        InvoiceCase invoiceCase,
        RevisionInputs inputs,
        InvoiceResult? result,
        RevisionAction action
    )
    {
        if (database.Entry(invoiceCase).State == EntityState.Detached)
            database.Cases.Add(invoiceCase);

        var revision = new CaseRevision
        {
            CaseId = invoiceCase.Id,
            Number = inputs.Number,
            Action = action,
            Snapshot = inputs.Snapshot,
            Document = inputs.Document,
            Result = result,
        };
        database.Revisions.Add(revision);
        invoiceCase.CurrentRevision = inputs.Number;
        database.SaveChanges();
        return revision;
    }

    private IQueryable<CaseRevision> RevisionQuery =>
        database.Revisions.AsNoTracking().Include(row => row.Document).Include(row => row.Snapshot);
}
