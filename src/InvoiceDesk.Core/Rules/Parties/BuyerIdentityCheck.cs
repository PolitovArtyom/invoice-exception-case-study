using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Parties;

internal sealed class BuyerIdentityCheck : IDocumentCheck
{
    public CheckCode Code => CheckCode.BuyerIdentity;

    public Verdict Evaluate(CheckContext context)
    {
        var buyer = context.Buyer;
        var order = context.Order;
        UnsupportedInvoiceCaseException.Require(
            buyer is not null && order is not null && buyer.BuyerId == order.BuyerId,
            Code,
            "for an unresolved buyer, missing order or different buyer"
        );

        return Verdict.Pass(
            new Comparison(
                Actual:
                [
                    Operand.Invoice("registration_id", context.Facts.Buyer.RegistrationId),
                    Operand.Invoice("legal_name", context.Facts.Buyer.LegalName),
                    Operand.Calculated("resolved_buyer_id", buyer.BuyerId),
                ],
                Expected:
                [
                    Operand.Order("buyer_id", order.BuyerId),
                    Operand.Directory("registration_id", buyer.RegistrationId),
                    Operand.Directory("legal_name", buyer.LegalName),
                    Operand.Directory("explicit_aliases", buyer.ExplicitAliases),
                ]
            )
        );
    }
}
