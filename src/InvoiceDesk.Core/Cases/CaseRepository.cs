using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.HumanResolution;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Duplicates;
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

    internal (
        InvoiceCase Case,
        CaseRevision Revision,
        DiscrepancyAcceptance? Acceptance,
        bool InputsChanged
    )[] List() =>
        database
            .Cases.AsNoTracking()
            .OrderBy(invoiceCase => invoiceCase.Id)
            .ToArray()
            .Select(invoiceCase =>
            {
                var revision = ReadAssessmentOrLatestRevision(invoiceCase);
                return (
                    invoiceCase,
                    revision,
                    ReadCurrentAcceptance(invoiceCase, revision),
                    InputsChanged(invoiceCase, revision)
                );
            })
            .ToArray();

    internal CompanyDataSnapshot ResolveSnapshot(string caseId, CompanyData companyData) =>
        database.CompanyDataSnapshots.FirstOrDefault(row =>
            row.CaseId == caseId && row.Data == companyData
        ) ?? new CompanyDataSnapshot { CaseId = caseId, Data = companyData };

    internal bool InputsChanged(InvoiceCase invoiceCase, CaseRevision assessed) =>
        assessed.Result is { } result && result.PolicyVersion != CaseMetadata.PolicyVersion
        || (
            assessed.Number != invoiceCase.CurrentRevision
            && database.Revisions.Any(row =>
                row.CaseId == invoiceCase.Id
                && row.Number == invoiceCase.CurrentRevision
                && (
                    row.SnapshotId != assessed.SnapshotId
                    || row.DocumentId != assessed.DocumentId
                    || row.Proposals != assessed.Proposals
                    || row.CorrectedFacts != assessed.CorrectedFacts
                )
            )
        );

    internal DiscrepancyAcceptance? ReadCurrentAcceptance(
        InvoiceCase invoiceCase,
        CaseRevision assessed
    ) =>
        assessed.Number == invoiceCase.CurrentRevision
            ? assessed.Acceptance
            : database
                .Revisions.AsNoTracking()
                .Where(row =>
                    row.CaseId == invoiceCase.Id && row.Number == invoiceCase.CurrentRevision
                )
                .Select(row => row.Acceptance)
                .Single();

    internal void AcceptDiscrepancy(CaseRevision revision, DiscrepancyAcceptance acceptance)
    {
        revision.Acceptance = acceptance;
        database.SaveChanges();
    }

    internal CaseRevision ReadAssessmentOrLatestRevision(InvoiceCase invoiceCase) =>
        RevisionQuery
            .Where(row => row.CaseId == invoiceCase.Id && row.Number <= invoiceCase.CurrentRevision)
            .OrderByDescending(row => row.Result != null)
            .ThenByDescending(row => row.Number)
            .First();

    internal CaseRevision? ReadRevision(string caseId, int number) =>
        RevisionQuery.SingleOrDefault(row => row.CaseId == caseId && row.Number == number);

    internal CompanyData? ReadCompanyData(string caseId, int number) =>
        database
            .Revisions.AsNoTracking()
            .Where(row => row.CaseId == caseId && row.Number == number)
            .Select(row => row.Snapshot.Data)
            .SingleOrDefault();

    internal int? ReadLatestDuplicateRevision(
        string caseId,
        long documentId,
        int currentRevision
    ) =>
        database
            .Revisions.AsNoTracking()
            .Where(row =>
                row.CaseId == caseId
                && row.DocumentId == documentId
                && row.Number <= currentRevision
                && row.DuplicateAttempt != null
            )
            .OrderByDescending(row => row.Number)
            .Select(row => new { row.Number, row.OriginalInvoice })
            .AsEnumerable()
            .FirstOrDefault(row => row.OriginalInvoice?.DocumentReference == documentId)
            ?.Number;

    internal CaseRevision[] ReadHistory(string caseId) =>
        RevisionQuery
            .Where(row => row.CaseId == caseId)
            .OrderByDescending(row => row.Number)
            .ToArray();

    internal InvoiceSource? ReadDocumentSource(string caseId, long reference) =>
        database
            .Documents.AsNoTracking()
            .Where(row => row.CaseId == caseId && row.Id == reference)
            .Select(row => row.Source)
            .SingleOrDefault();

    internal bool HasAccountingAssessment(InvoiceDocument document) =>
        database
            .Revisions.AsNoTracking()
            .Where(row =>
                row.CaseId == document.CaseId
                && row.Document!.ContentSha256 == document.ContentSha256
                && (row.Result != null || row.Acceptance != null)
            )
            .Select(row => new
            {
                row.Document!.Facts,
                row.Result,
                row.Acceptance,
            })
            .AsEnumerable()
            .Any(row =>
                row.Facts.DocumentId == document.Facts.DocumentId
                && (row.Result?.Decision == Decision.Clear || row.Acceptance is not null)
            );

    internal EarlierImport[] ReadEarlierImports(string caseId) =>
        (
            from document in database.Documents.AsNoTracking()
            where document.CaseId == caseId
            orderby document.ImportRevision
            let latestAssessment = database
                .Revisions.Where(revision =>
                    revision.DocumentId == document.Id && revision.Result != null
                )
                .OrderByDescending(revision => revision.Number)
                .FirstOrDefault()
            select new
            {
                document.Id,
                document.Facts,
                document.ContentSha256,
                LatestAssessment = (int?)latestAssessment!.Number,
                latestAssessment.CorrectedFacts,
            }
        )
            .AsEnumerable()
            .Select(document => new EarlierImport(
                new OriginalInvoice(
                    document.Facts.DocumentId,
                    document.ContentSha256,
                    document.Id,
                    document.LatestAssessment?.ToString(
                        System.Globalization.CultureInfo.InvariantCulture
                    )
                ),
                document.CorrectedFacts ?? document.Facts
            ))
            .ToArray();

    internal void Append(
        InvoiceCase invoiceCase,
        RevisionInputs inputs,
        InvoiceResult? result,
        RevisionAction action,
        OriginalInvoice? original,
        DuplicateAttempt? duplicate,
        FactCorrection? correction,
        DiscrepancyAcceptance? acceptance
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
            Proposals = inputs.Proposals.Length == 0 ? null : new(inputs.Proposals),
            OriginalInvoice = original,
            DuplicateAttempt = duplicate,
            CorrectedFacts = inputs.CorrectedFacts,
            Correction = correction,
            Acceptance = acceptance,
        };
        database.Revisions.Add(revision);
        invoiceCase.CurrentRevision = inputs.Number;
        database.SaveChanges();
    }

    private IQueryable<CaseRevision> RevisionQuery =>
        database.Revisions.AsNoTracking().Include(row => row.Document).Include(row => row.Snapshot);
}
