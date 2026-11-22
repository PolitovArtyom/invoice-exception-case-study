using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Pricing;

internal static class UnitPriceCheck
{
    private const string Code = "UNIT_PRICE";

    private const decimal Tolerance = 0.01m;

    public static CheckOutcome Evaluate(InvoiceFacts facts, InvoiceLine line, int index, PurchaseOrderLine orderLine)
    {
        decimal difference = Math.Abs(InvoiceDecimal.Parse(line.UnitNetPrice) - InvoiceDecimal.Parse(orderLine.UnitNetPrice));
        CheckOutcome.Require(difference <= Tolerance, $"{Code} outside tolerance for {line.LineId}");

        return CheckOutcome.Pass(Code, line.LineId, facts,
            new Dictionary<string, CheckOperand>
            {
                ["unit_net_price"] = CheckOperand.Invoice(line.UnitNetPrice),
                ["absolute_difference"] = CheckOperand.Calculated(InvoiceDecimal.Money(difference))
            },
            new Dictionary<string, CheckOperand>
            {
                ["unit_net_price"] = CheckOperand.Order(orderLine.UnitNetPrice),
                ["inclusive_tolerance"] = CheckOperand.Policy(InvoiceDecimal.Money(Tolerance))
            }, $"/lines/{index}/unit_net_price");
    }
}
