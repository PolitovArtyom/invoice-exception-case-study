using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Normalization;

namespace InvoiceDesk.Core.Rules.Parties;

internal static class SupplierIdentityCheck
{
    private const string Code = "SUPPLIER_IDENTITY";

    public static Supplier? Resolve(InvoiceFacts facts, CompanyData companyData)
    {
        var matches = companyData.Suppliers.Suppliers
            .Where(supplier => InvoiceText.Identifier(supplier.RegistrationId) == InvoiceText.Identifier(facts.Supplier.RegistrationId))
            .Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    public static CheckOutcome Evaluate(InvoiceFacts facts, Supplier? supplier, PurchaseOrder order)
    {
        if (supplier is null || supplier.SupplierId != order.SupplierId)
            throw new UnsupportedInvoiceCaseException($"{Code} for a missing, ambiguous or different supplier");

        return CheckOutcome.Pass(Code, facts.DocumentId, facts,
            new Dictionary<string, CheckOperand>
            {
                ["registration_id"] = CheckOperand.Invoice(facts.Supplier.RegistrationId),
                ["resolved_supplier_id"] = CheckOperand.Calculated(supplier.SupplierId)
            },
            new Dictionary<string, CheckOperand>
            {
                ["supplier_id"] = CheckOperand.Order(order.SupplierId),
                ["registration_id"] = CheckOperand.Company(supplier.RegistrationId)
            }, "/supplier/registration_id");
    }
}
