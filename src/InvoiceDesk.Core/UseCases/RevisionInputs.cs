using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Storage;

namespace InvoiceDesk.Core.UseCases;

internal sealed record RevisionInputs(int Number, CompanyDataSnapshot Snapshot, InvoiceDocument? Document)
{
    public CompanyData CompanyData => Snapshot.Data;

    public InvoiceFacts? Facts => Document?.Facts;
}
