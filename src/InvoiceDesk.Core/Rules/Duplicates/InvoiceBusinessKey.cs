using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Rules.Normalization;

namespace InvoiceDesk.Core.Rules.Duplicates;

internal sealed record InvoiceBusinessKey(string SupplierId, string BuyerId, string Number)
{
    public static InvoiceBusinessKey From(InvoiceFacts facts, Supplier supplier, Buyer buyer) =>
        new(supplier.SupplierId, buyer.BuyerId, InvoiceText.Identifier(facts.Number)!);

    public bool Matches(CountedInvoice invoice) =>
        SupplierId == invoice.SupplierId && BuyerId == invoice.BuyerId && Number == InvoiceText.Identifier(invoice.DocumentNumber);
}
