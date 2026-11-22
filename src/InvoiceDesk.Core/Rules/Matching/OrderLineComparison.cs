using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Matching;

internal static class OrderLineComparison
{
    public static Verdict? NotComparable(CheckContext context, MatchedLine line)
    {
        if (context.Order is null)
            return Verdict.DependsOn(CheckCode.OrderMatch);

        if (line.OrderLine is null)
            return Verdict.DependsOn(CheckCode.LineMatch);

        return line.Invoice.Unit == line.OrderLine.Unit ? null : Verdict.NotApplicable();
    }
}
