using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Normalization;

namespace InvoiceDesk.Core.Rules.Matching;

internal static class LineMatchCheck
{
    private const string Code = "LINE_MATCH";

    public static (PurchaseOrderLine OrderLine, string MatchSource, CheckOutcome Outcome)[] Evaluate(InvoiceFacts facts, PurchaseOrder order)
    {
        var matches = facts.Lines.Select((line, index) => Match(facts, line, index, order)).ToArray();
        CheckOutcome.Require(matches.Select(match => match.OrderLine.LineId).Distinct().Count() == matches.Length,
            $"{Code} when multiple invoice lines claim one order line");
        return matches;
    }

    private static (PurchaseOrderLine OrderLine, string MatchSource, CheckOutcome Outcome) Match(
        InvoiceFacts facts, InvoiceLine line, int index, PurchaseOrder order)
    {
        var matches = order.Lines.Select(candidate => (OrderLine: candidate, Reason: MatchReason(line, candidate)))
            .Where(match => match.Reason is not null).Take(2).ToArray();
        if (matches.Length != 1)
            throw new UnsupportedInvoiceCaseException($"{Code} without a unique exact match for {line.LineId}");

        var (orderLine, reason) = matches[0];
        CheckOutcome.Require(line.Unit == orderLine.Unit, $"{Code} with different units for {line.LineId}");
        return (orderLine, "deterministic", CheckOutcome.Pass(Code, line.LineId, facts,
            new Dictionary<string, CheckOperand>
            {
                ["sku"] = CheckOperand.Invoice(line.Sku),
                ["description"] = CheckOperand.Invoice(line.Description),
                ["unit"] = CheckOperand.Invoice(line.Unit),
                ["match_reason"] = CheckOperand.Calculated(reason)
            },
            new Dictionary<string, CheckOperand>
            {
                ["order_line_id"] = CheckOperand.Order(orderLine.LineId),
                ["sku"] = CheckOperand.Order(orderLine.Sku),
                ["description"] = CheckOperand.Order(orderLine.Description),
                ["explicit_aliases"] = CheckOperand.Order(orderLine.ExplicitAliases),
                ["unit"] = CheckOperand.Order(orderLine.Unit)
            }, $"/lines/{index}/sku", $"/lines/{index}/description", $"/lines/{index}/unit"));
    }

    private static string? MatchReason(InvoiceLine line, PurchaseOrderLine candidate)
    {
        if (line.Sku is not null && InvoiceText.Identifier(line.Sku) == InvoiceText.Identifier(candidate.Sku))
            return "sku";
        if (candidate.ExplicitAliases.Any(alias => InvoiceText.SameName(line.Description, alias)))
            return "explicit_alias";
        return InvoiceText.SameName(line.Description, candidate.Description) ? "description" : null;
    }
}
