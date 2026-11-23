using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Proposals;
using InvoiceDesk.Core.Storage;

namespace InvoiceDesk.Core.Cases;

internal sealed record RevisionInputs(
    int Number,
    CompanyDataSnapshot Snapshot,
    InvoiceDocument? Document,
    RegisteredProposal[] Proposals,
    InvoiceFacts? CorrectedFacts
)
{
    public CompanyData CompanyData => Snapshot.Data;

    public InvoiceFacts? Facts => CorrectedFacts ?? Document?.Facts;
}
