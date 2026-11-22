using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules.Parties;

internal static class CurrencyMatchCheck
{
    public static InvoiceCheck Evaluate(InvoiceFacts facts, PurchaseOrder order)
    {
        Check.Require(facts.Currency == order.Currency, "CURRENCY_MATCH for an absent or different currency");

        return Check.Pass("CURRENCY_MATCH", facts.DocumentId, facts,
            new Dictionary<string, CheckOperand> { ["currency"] = CheckOperand.Invoice(facts.Currency) },
            new Dictionary<string, CheckOperand> { ["currency"] = CheckOperand.Company(order.Currency) },
            "/currency");
    }
}
