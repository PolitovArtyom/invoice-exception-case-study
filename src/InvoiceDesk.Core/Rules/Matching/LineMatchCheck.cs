using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Matching;

internal sealed class LineMatchCheck : ILineCheck
{
    public CheckCode Code => CheckCode.LineMatch;

    public Verdict Evaluate(CheckContext context, MatchedLine line)
    {
        var invoice = line.Invoice;
        var orderLine = line.OrderLine;
        if (context.Order is null)
            return Verdict.DependsOn(CheckCode.OrderMatch);
        if (orderLine is null)
            return Verdict.MissingFact(RequiredFact.LineMatch);

        var comparison = new Comparison(
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
        );
        return line.Source == MatchSource.AgentProposal || invoice.Unit != orderLine.Unit
            ? Verdict.Fail(Severity.NeedsReview, comparison)
            : Verdict.Pass(comparison);
    }
}
