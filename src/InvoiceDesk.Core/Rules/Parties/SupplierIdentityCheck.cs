using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Parties;

internal sealed class SupplierIdentityCheck : IDocumentCheck
{
    public CheckCode Code => CheckCode.SupplierIdentity;

    public Verdict Evaluate(CheckContext context)
    {
        var supplier = context.Supplier;
        var order = context.Order;
        UnsupportedInvoiceCaseException.Require(
            supplier is not null && order is not null && supplier.SupplierId == order.SupplierId,
            Code,
            "for an unresolved supplier, missing order or different supplier"
        );

        return Verdict.Pass(
            new Comparison(
                Actual:
                [
                    Operand.Invoice("registration_id", context.Facts.Supplier.RegistrationId),
                    Operand.Calculated("resolved_supplier_id", supplier.SupplierId),
                ],
                Expected:
                [
                    Operand.Order("supplier_id", order.SupplierId),
                    Operand.Directory("registration_id", supplier.RegistrationId),
                ]
            )
        );
    }
}
