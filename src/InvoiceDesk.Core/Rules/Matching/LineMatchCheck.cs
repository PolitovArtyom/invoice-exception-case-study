using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Matching;

internal sealed class LineMatchCheck : ILineCheck
{
    public CheckCode Code => CheckCode.LineMatch;

    public Verdict Evaluate(CheckContext context, MatchedLine line)
    {
        var invoice = line.Invoice;
        var orderLine = line.OrderLine;
        UnsupportedInvoiceCaseException.Require(
            orderLine is not null,
            Code,
            $"without a unique one-to-one match for {invoice.LineId}"
        );
        UnsupportedInvoiceCaseException.Require(
            invoice.Unit == orderLine.Unit,
            Code,
            $"with different units for {invoice.LineId}"
        );

        return Verdict.Pass(
            new Comparison(
                Actual:
                [
                    Operand.Invoice("sku", invoice.Sku),
                    Operand.Invoice("description", invoice.Description),
                    Operand.Invoice("unit", invoice.Unit),
                    Operand.Calculated("match_reason", line.MatchReason),
                ],
                Expected:
                [
                    Operand.Order("order_line_id", orderLine.LineId),
                    Operand.Order("sku", orderLine.Sku),
                    Operand.Order("description", orderLine.Description),
                    Operand.Order("explicit_aliases", orderLine.ExplicitAliases),
                    Operand.Order("unit", orderLine.Unit),
                ]
            )
        );
    }
}
