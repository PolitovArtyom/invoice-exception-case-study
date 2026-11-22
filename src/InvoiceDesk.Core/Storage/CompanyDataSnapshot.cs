using InvoiceDesk.Core.Model.CompanyData;

namespace InvoiceDesk.Core.Storage;

internal sealed class CompanyDataSnapshot
{
    public long Id { get; init; }

    public required string CaseId { get; init; }

    public required CompanyData Data { get; init; }
}
