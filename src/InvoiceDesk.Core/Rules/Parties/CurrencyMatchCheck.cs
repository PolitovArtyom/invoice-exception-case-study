using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Parties;

internal static class CurrencyMatchCheck
{
    private const string Code = "CURRENCY_MATCH";

    public static CheckOutcome Evaluate(InvoiceFacts facts, PurchaseOrder order)
    {
        CheckOutcome.Require(facts.Currency == order.Currency, $"{Code} for an absent or different currency");

        return CheckOutcome.Pass(Code, facts.DocumentId, facts,
            new Dictionary<string, CheckOperand> { ["currency"] = CheckOperand.Invoice(facts.Currency) },
            new Dictionary<string, CheckOperand> { ["currency"] = CheckOperand.Order(order.Currency) },
            "/currency");
    }
}
