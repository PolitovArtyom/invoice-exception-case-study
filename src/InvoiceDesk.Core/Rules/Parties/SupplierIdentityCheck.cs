using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Parties;

internal sealed class SupplierIdentityCheck : IDocumentCheck
{
    public CheckCode Code => CheckCode.SupplierIdentity;

    public Verdict Evaluate(CheckContext context)
    {
        var supplier = context.Supplier;
        var printed = context.Facts.Supplier;
        if (supplier is null)
            return printed.RegistrationId is null
                ? Verdict.MissingFact(RequiredFact.SupplierRegistrationId)
                : Verdict.Fail(
                    Severity.Blocked,
                    new Comparison(
                        Actual: [Operand.Invoice("registration_id", printed.RegistrationId)],
                        Expected: [Operand.Directory("matching_registration_id", null)]
                    )
                );

        if (context.Order is not { } order)
            return Verdict.DependsOn(CheckCode.OrderMatch);

        var orderSupplier = context.CompanyData.Suppliers.Suppliers.FirstOrDefault(entry =>
            entry.SupplierId == order.SupplierId
        );
        var comparison = new Comparison(
            Actual:
            [
                Operand.Invoice("registration_id", context.Facts.Supplier.RegistrationId),
                Operand.Calculated("resolved_supplier_id", supplier.SupplierId),
            ],
            Expected:
            [
                Operand.Order("supplier_id", order.SupplierId),
                Operand.Directory("registration_id", orderSupplier?.RegistrationId),
            ]
        );
        return supplier.SupplierId == order.SupplierId
            ? Verdict.Pass(comparison)
            : Verdict.Fail(Severity.Blocked, comparison);
    }
}
