using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Parties;

internal sealed class CurrencyMatchCheck : IDocumentCheck
{
    public CheckCode Code => CheckCode.CurrencyMatch;

    public Verdict Evaluate(CheckContext context)
    {
        var order = context.Order;
        string? currency = context.Facts.Currency;
        UnsupportedInvoiceCaseException.Require(
            order is not null && currency == order.Currency,
            Code,
            "for a missing order or an absent or different currency"
        );

        return Verdict.Pass(
            new Comparison(
                Actual: [Operand.Invoice("currency", currency)],
                Expected: [Operand.Order("currency", order.Currency)]
            )
        );
    }
}
