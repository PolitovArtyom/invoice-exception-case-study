using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace InvoiceDesk.Core.Storage;

public sealed class CaseRepository(InvoiceDatabase database)
{
    internal IDbContextTransaction BeginUpdate() => database.Database.BeginTransaction();

    internal InvoiceCase? Find(string caseId) => database.Cases.AsNoTracking().SingleOrDefault(row => row.Id == caseId);

    internal InvoiceCase? FindForUpdate(string caseId) => database.Cases.Find(caseId);

    internal InvoiceCase Add(CaseMetadata metadata)
    {
        var invoiceCase = new InvoiceCase
        {
            Id = metadata.CaseId,
            Origin = metadata.Origin,
            AsOf = metadata.AsOf,
            Sources = new(metadata.Sources)
        };
        database.Cases.Add(invoiceCase);
        return invoiceCase;
    }

    internal (InvoiceCase Case, CaseRevision Revision)[] List() =>
        (from invoiceCase in database.Cases.AsNoTracking()
         join revision in RevisionQuery
             on new { CaseId = invoiceCase.Id, Number = invoiceCase.CurrentRevision }
             equals new { revision.CaseId, revision.Number }
         orderby invoiceCase.Id
         select new { Case = invoiceCase, Revision = revision })
        .AsEnumerable().Select(item => (item.Case, item.Revision)).ToArray();

    internal CaseRevision? ReadRevision(string caseId, int number) =>
        RevisionQuery.SingleOrDefault(row => row.CaseId == caseId && row.Number == number);

    internal CaseRevision[] ReadHistory(string caseId) =>
        RevisionQuery.Where(row => row.CaseId == caseId).OrderByDescending(row => row.Number).ToArray();

    internal EarlierInvoice[] ReadEarlierImports(string caseId, long? exceptDocumentId) =>
        database.Revisions.AsNoTracking()
            .Where(row => row.CaseId == caseId && row.Document != null &&
                row.Document.ImportOrder == row.Number && row.DocumentId != exceptDocumentId)
            .OrderBy(row => row.Number)
            .Select(row => new EarlierInvoice(row.Document!.Facts, row.Snapshot.Data))
            .ToArray();

    internal CaseRevision Append(InvoiceCase invoiceCase, CompanySnapshot? company, CompanyDataSnapshot? previousSnapshot,
        InvoiceReading? reading, InvoiceDocument? previousDocument, InvoiceResult? result, RevisionAction action)
    {
        int number = invoiceCase.CurrentRevision + 1;
        var snapshot = company is null ? database.CompanyDataSnapshots.Attach(previousSnapshot!).Entity
            : database.CompanyDataSnapshots.Add(new CompanyDataSnapshot
            {
                CaseId = invoiceCase.Id,
                ContextVersion = company.ContextVersion,
                Data = company
            }).Entity;

        var document = previousDocument;
        if (reading is not null)
        {
            document = database.Documents.Add(new InvoiceDocument
            {
                CaseId = invoiceCase.Id,
                ExternalDocumentId = reading.Facts.DocumentId,
                ImportOrder = number,
                ContentSha256 = reading.ContentSha256,
                Source = reading.Source,
                Facts = reading.Facts,
                ReadingOrigin = reading.Origin
            }).Entity;
        }
        else if (document is not null)
        {
            database.Documents.Attach(document);
        }

        var revision = new CaseRevision
        {
            CaseId = invoiceCase.Id,
            Number = number,
            Action = action,
            Snapshot = snapshot,
            Document = document,
            Result = result
        };
        database.Revisions.Add(revision);
        invoiceCase.CurrentRevision = number;
        database.SaveChanges();
        return revision;
    }

    private IQueryable<CaseRevision> RevisionQuery => database.Revisions.AsNoTracking()
        .Include(row => row.Document).Include(row => row.Snapshot);
}
