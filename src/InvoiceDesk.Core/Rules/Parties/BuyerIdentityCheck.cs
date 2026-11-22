using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Parties;

internal sealed class BuyerIdentityCheck : IDocumentCheck
{
    public CheckCode Code => CheckCode.BuyerIdentity;

    public Verdict Evaluate(CheckContext context)
    {
        var buyer = context.Buyer;
        var printed = context.Facts.Buyer;
        if (buyer is null)
            return printed.RegistrationId is null
                ? Verdict.MissingFact(RequiredFact.BuyerRegistrationId)
                : Verdict.Fail(
                    Severity.Blocked,
                    new Comparison(
                        Actual: [Operand.Invoice("registration_id", printed.RegistrationId)],
                        Expected: [Operand.Directory("matching_registration_id", null)]
                    )
                );

        if (context.Order is not { } order)
            return Verdict.DependsOn(CheckCode.OrderMatch);

        var orderBuyer = context.CompanyData.Buyers.Buyers.FirstOrDefault(entry =>
            entry.BuyerId == order.BuyerId
        );
        var comparison = new Comparison(
            Actual:
            [
                Operand.Invoice("registration_id", context.Facts.Buyer.RegistrationId),
                Operand.Invoice("legal_name", context.Facts.Buyer.LegalName),
                Operand.Calculated("resolved_buyer_id", buyer.BuyerId),
            ],
            Expected:
            [
                Operand.Order("buyer_id", order.BuyerId),
                Operand.Directory("registration_id", orderBuyer?.RegistrationId),
                Operand.Directory("legal_name", orderBuyer?.LegalName),
                Operand.Directory("explicit_aliases", orderBuyer?.ExplicitAliases),
            ]
        );
        return buyer.BuyerId == order.BuyerId
            ? Verdict.Pass(comparison)
            : Verdict.Fail(Severity.Blocked, comparison);
    }
}
