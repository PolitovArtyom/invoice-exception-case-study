using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Pricing;

internal sealed class UnitPriceCheck : ILineCheck
{
    private const decimal Tolerance = 0.01m;

    public CheckCode Code => CheckCode.UnitPrice;

    public Verdict Evaluate(CheckContext context, MatchedLine line)
    {
        var orderLine = line.OrderLine;
        UnsupportedInvoiceCaseException.Require(
            orderLine is not null,
            Code,
            $"without a matched order line for {line.Invoice.LineId}"
        );

        decimal difference = Math.Abs(line.Invoice.UnitNetPrice - orderLine.UnitNetPrice);
        UnsupportedInvoiceCaseException.Require(
            difference <= Tolerance,
            Code,
            $"outside tolerance for {line.Invoice.LineId}"
        );

        return Verdict.Pass(
            new Comparison(
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
            )
        );
    }
}
