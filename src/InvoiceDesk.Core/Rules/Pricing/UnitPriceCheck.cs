using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Matching;

namespace InvoiceDesk.Core.Rules.Pricing;

internal sealed class UnitPriceCheck : ILineCheck
{
    private const decimal Tolerance = 0.01m;

    public CheckCode Code => CheckCode.UnitPrice;

    public Verdict Evaluate(CheckContext context, MatchedLine line)
    {
        if (OrderLineComparison.NotComparable(context, line) is { } notComparable)
            return notComparable;

        var orderLine = line.OrderLine!;
        decimal difference = Math.Abs(line.Invoice.UnitNetPrice - orderLine.UnitNetPrice);

        var comparison = new Comparison(
            Actual:
            [
                Operand.Invoice("unit_net_price", line.Invoice.UnitNetPrice),
                Operand.Calculated("absolute_difference", difference),
            ],
            Expected:
            [
                Operand.Order("unit_net_price", orderLine.UnitNetPrice),
                Operand.Policy("inclusive_tolerance", Tolerance),
            ]
        );
        return difference <= Tolerance
            ? Verdict.Pass(comparison)
            : Verdict.Fail(Severity.NeedsReview, comparison);
    }
}
