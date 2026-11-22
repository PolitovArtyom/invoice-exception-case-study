using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Bank;
using InvoiceDesk.Core.Rules.Duplicates;
using InvoiceDesk.Core.Rules.Matching;
using InvoiceDesk.Core.Rules.Parties;
using InvoiceDesk.Core.Rules.Pricing;
using InvoiceDesk.Core.Rules.Quantities;

using InvoiceDesk.Core.UseCases;

namespace InvoiceDesk.Core.Rules;

public sealed class InvoiceEvaluator
{
    internal InvoiceResult Evaluate(string caseId, int step, RevisionInputs inputs, IReadOnlyList<EarlierInvoice> earlierImports)
    {
        var facts = inputs.Facts!;
        var companyData = inputs.CompanyData;
        var (order, orderMatchSource, orderCheck) = OrderMatchCheck.Evaluate(facts, companyData);
        var supplier = SupplierIdentityCheck.Resolve(facts, companyData);
        var buyer = BuyerIdentityCheck.Resolve(facts, companyData);
        var supplierCheck = SupplierIdentityCheck.Evaluate(facts, supplier, order);
        var buyerCheck = BuyerIdentityCheck.Evaluate(facts, buyer, order);
        var key = InvoiceBusinessKey.From(facts, supplier!, buyer!);
        var (importDisposition, conflictCheck) = DocumentConflictCheck.Evaluate(facts, companyData, key, earlierImports);
        var currencyCheck = CurrencyMatchCheck.Evaluate(facts, order);
        var matches = LineMatchCheck.Evaluate(facts, order);
        var lines = matches.Select((match, index) => EvaluateLine(inputs, order, match.OrderLine, match.MatchSource, index, key)).ToArray();
        var (totals, totalsCheck) = TotalsArithmeticCheck.Evaluate(facts, lines.Select(line => line.Amounts).ToArray());
        var bankCheck = BankAccountCheck.Evaluate(facts, supplier!);

        CheckOutcome[] outcomes =
        [
            conflictCheck, supplierCheck, buyerCheck, orderCheck, currencyCheck,
            .. matches.Select(match => match.Outcome),
            .. lines.SelectMany(line => line.Outcomes),
            totalsCheck, bankCheck
        ];
        var checks = outcomes.Select(outcome => outcome.Check).ToArray();
        var questions = outcomes.Select(outcome => outcome.Question).OfType<InvoiceQuestion>().ToArray();
        return new("1.0", caseId, step, CaseMetadata.PolicyVersion, companyData.ContextVersion, inputs.Number,
            facts.DocumentId, importDisposition, null, null, order.OrderId, orderMatchSource,
            lines.Select(line => line.Result).ToArray(), totals, checks, InvoiceDecision.From(checks), questions);
    }

    private static (InvoiceLineResult Result, LineAmounts Amounts, CheckOutcome[] Outcomes) EvaluateLine(
        RevisionInputs inputs, PurchaseOrder order, PurchaseOrderLine orderLine, string matchSource, int index, InvoiceBusinessKey key)
    {
        var facts = inputs.Facts!;
        var line = facts.Lines[index];
        var quantities = LineQuantities.Calculate(order, orderLine, inputs.CompanyData, key);
        var receiptCheck = QuantityReceivedCheck.Evaluate(facts, line, index, quantities);
        var remainingCheck = QuantityOrderRemainingCheck.Evaluate(facts, line, index, quantities);
        var priceCheck = UnitPriceCheck.Evaluate(facts, line, index, orderLine);
        var (amounts, arithmeticCheck) = LineArithmeticCheck.Evaluate(facts, line, index);
        var result = new InvoiceLineResult(line.LineId, orderLine.LineId, matchSource,
            InvoiceDecimal.Money(amounts.Net), InvoiceDecimal.Money(amounts.Tax),
            InvoiceDecimal.Quantity(quantities.Ordered), FormatQuantity(quantities.Received),
            InvoiceDecimal.Quantity(quantities.PreviouslyInvoiced), FormatQuantity(quantities.AvailableToInvoice),
            InvoiceDecimal.Quantity(quantities.RemainingOnOrder));

        return (result, amounts, [receiptCheck, remainingCheck, priceCheck, arithmeticCheck]);
    }

    private static string? FormatQuantity(decimal? quantity) =>
        quantity is decimal value ? InvoiceDecimal.Quantity(value) : null;
}
