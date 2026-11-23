using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Rules.Duplicates;
using InvoiceDesk.Core.Rules.Matching;

namespace InvoiceDesk.Core.Rules.Parties;

internal sealed record InvoiceParties(Supplier? Supplier, Buyer? Buyer, InvoiceBusinessKey? Key)
{
    public static InvoiceParties Resolve(InvoiceFacts facts, CompanyData companyData)
    {
        var supplier = InvoiceMatches.FindSupplier(facts, companyData);
        var buyer = InvoiceMatches.FindBuyer(facts, companyData);
        var key =
            supplier is null || buyer is null
                ? null
                : InvoiceBusinessKey.From(facts, supplier, buyer);
        return new(supplier, buyer, key);
    }
}
