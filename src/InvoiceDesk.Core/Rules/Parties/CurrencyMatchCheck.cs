using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Parties;

internal sealed class CurrencyMatchCheck : IDocumentCheck
{
    public CheckCode Code => CheckCode.CurrencyMatch;

    public Verdict Evaluate(CheckContext context)
    {
        if (context.Facts.Currency is not { } currency)
            return Verdict.MissingFact(RequiredFact.DocumentCurrency);
        if (context.Order is not { } order)
            return Verdict.DependsOn(CheckCode.OrderMatch);

        var comparison = new Comparison(
            Actual: [Operand.Invoice("currency", currency)],
            Expected: [Operand.Order("currency", order.Currency)]
        );
        return currency == order.Currency
            ? Verdict.Pass(comparison)
            : Verdict.Fail(Severity.Blocked, comparison);
    }
}
