using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Bank;
using InvoiceDesk.Core.Rules.Duplicates;
using InvoiceDesk.Core.Rules.Matching;
using InvoiceDesk.Core.Rules.Parties;
using InvoiceDesk.Core.Rules.Pricing;
using InvoiceDesk.Core.Rules.Quantities;

namespace InvoiceDesk.Core.Rules;

public sealed class InvoiceEvaluator
{
    public InvoiceResult Evaluate(AssessmentInput input)
    {
        var facts = input.Facts;
        var company = input.CompanyData;
        var (order, orderCheck) = OrderMatchCheck.Evaluate(facts, company);
        var supplier = SupplierIdentityCheck.Resolve(facts, company);
        var buyer = BuyerIdentityCheck.Resolve(facts, company);
        var supplierCheck = SupplierIdentityCheck.Evaluate(facts, supplier, order);
        var buyerCheck = BuyerIdentityCheck.Evaluate(facts, buyer, order);
        var key = InvoiceBusinessKey.From(facts, supplier!, buyer!);
        var conflictCheck = DocumentConflictCheck.Evaluate(facts, company, key, input.EarlierImports);
        var currencyCheck = CurrencyMatchCheck.Evaluate(facts, order);
        var matches = LineMatchCheck.Evaluate(facts, order);
        var lines = matches.Select((match, index) => EvaluateLine(input, order, match.OrderLine, index, key)).ToArray();
        var (totals, totalsCheck) = TotalsArithmeticCheck.Evaluate(facts, lines.Select(line => line.Amounts).ToArray());
        var bankCheck = BankAccountCheck.Evaluate(facts, supplier!);

        InvoiceCheck[] checks =
        [
            conflictCheck, supplierCheck, buyerCheck, orderCheck, currencyCheck,
            .. matches.Select(match => match.Check),
            .. lines.SelectMany(line => line.Checks),
            totalsCheck, bankCheck
        ];
        return new("1.0", input.CaseId, input.Step, CaseMetadata.PolicyVersion, company.ContextVersion, input.Revision,
            facts.DocumentId, "new", null, null, order.OrderId, "deterministic",
            lines.Select(line => line.Result).ToArray(), totals, checks, "clear", []);
    }

    private static (InvoiceLineResult Result, LineAmounts Amounts, InvoiceCheck[] Checks) EvaluateLine(
        AssessmentInput input, PurchaseOrder order, PurchaseOrderLine orderLine, int index, InvoiceBusinessKey key)
    {
        var facts = input.Facts;
        var line = facts.Lines[index];
        var quantities = LineQuantities.Calculate(order, orderLine, input.CompanyData, key);
        var receiptCheck = QuantityReceivedCheck.Evaluate(facts, line, index, quantities);
        var remainingCheck = QuantityOrderRemainingCheck.Evaluate(facts, line, index, quantities);
        var priceCheck = UnitPriceCheck.Evaluate(facts, line, index, orderLine);
        var (amounts, arithmeticCheck) = LineArithmeticCheck.Evaluate(facts, line, index);
        var result = new InvoiceLineResult(line.LineId, orderLine.LineId, "deterministic",
            InvoiceDecimal.Money(amounts.Net), InvoiceDecimal.Money(amounts.Tax),
            InvoiceDecimal.Quantity(quantities.Ordered), FormatQuantity(quantities.Received),
            InvoiceDecimal.Quantity(quantities.PreviouslyInvoiced), FormatQuantity(quantities.AvailableToInvoice),
            InvoiceDecimal.Quantity(quantities.RemainingOnOrder));

        return (result, amounts, [receiptCheck, remainingCheck, priceCheck, arithmeticCheck]);
    }

    private static string? FormatQuantity(decimal? quantity) =>
        quantity is decimal value ? InvoiceDecimal.Quantity(value) : null;
}
