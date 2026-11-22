using InvoiceDesk.Core.Cases;
using InvoiceDesk.Core.Model;
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
    private static readonly IDocumentCheck[] DocumentChecks =
    [
        new OrderMatchCheck(),
        new SupplierIdentityCheck(),
        new BuyerIdentityCheck(),
        new DocumentConflictCheck(),
        new CurrencyMatchCheck(),
        new TotalsArithmeticCheck(),
        new BankAccountCheck(),
    ];

    private static readonly ILineCheck[] LineChecks =
    [
        new LineMatchCheck(),
        new QuantityReceivedCheck(),
        new QuantityOrderRemainingCheck(),
        new UnitPriceCheck(),
        new LineArithmeticCheck(),
    ];

    internal InvoiceResult Evaluate(
        string caseId,
        int step,
        RevisionInputs inputs,
        IReadOnlyList<EarlierInvoice> earlierImports
    )
    {
        var context = BuildContext(inputs, earlierImports);
        string documentId = context.Facts.DocumentId;
        var verdicts = DocumentChecks
            .Select(check => (check.Code, ObjectId: documentId, Verdict: check.Evaluate(context)))
            .Concat(
                context.Lines.SelectMany(line =>
                    LineChecks.Select(check =>
                        (
                            check.Code,
                            ObjectId: line.Invoice.LineId,
                            Verdict: check.Evaluate(context, line)
                        )
                    )
                )
            )
            .ToArray();
        var checks = verdicts
            .Select(item => new CheckResult(
                item.Code,
                item.ObjectId,
                item.Verdict.Status,
                item.Verdict.Severity,
                item.Verdict.Dependency,
                item.Verdict.Comparison?.Actual,
                item.Verdict.Comparison?.Expected
            ))
            .ToArray();
        var questions = verdicts
            .Where(item => item.Verdict.RequiredFact is not null)
            .Select(item => (Fact: item.Verdict.RequiredFact!.Value, item.ObjectId))
            .GroupBy(item => item.Fact)
            .Select(group => new InvoiceQuestion(
                group.Key,
                group.Select(item => item.ObjectId).Distinct(StringComparer.Ordinal).ToArray(),
                QuestionText(group.Key)
            ))
            .ToArray();
        var (subtotal, tax) = context.Totals;

        return new(
            "1.0",
            caseId,
            step,
            CaseMetadata.PolicyVersion,
            context.CompanyData.ContextVersion,
            inputs.Number,
            documentId,
            ImportDisposition.New,
            null,
            null,
            context.Order?.OrderId,
            context.Order is null ? null : MatchSource.Deterministic,
            context.Lines.Select(LineResult).ToArray(),
            new InvoiceTotals(subtotal, tax, subtotal + tax),
            checks,
            InvoiceDecision.From(checks),
            questions
        );
    }

    private static CheckContext BuildContext(
        RevisionInputs inputs,
        IReadOnlyList<EarlierInvoice> earlierImports
    )
    {
        var facts = inputs.Facts!;
        var companyData = inputs.CompanyData;
        var order = InvoiceMatches.FindOrder(facts, companyData);
        var supplier = InvoiceMatches.FindSupplier(facts, companyData);
        var buyer = InvoiceMatches.FindBuyer(facts, companyData);
        var key =
            supplier is null || buyer is null
                ? null
                : InvoiceBusinessKey.From(facts, supplier, buyer);

        var lines = facts
            .Lines.Zip(InvoiceMatches.FindLines(facts, order), (invoice, match) =>
            {
                var quantities =
                    match is null || order is null || key is null
                        ? null
                        : LineQuantities.Calculate(order, match.OrderLine, companyData, key);
                return new MatchedLine(
                    invoice,
                    match?.OrderLine,
                    match?.Reason,
                    LineAmounts.Calculate(invoice),
                    quantities
                );
            })
            .ToArray();
        var totals = new LineAmounts(
            lines.Sum(line => line.Amounts.Net),
            lines.Sum(line => line.Amounts.Tax)
        );

        return new(
            facts,
            companyData,
            supplier,
            buyer,
            order,
            lines,
            key,
            companyData.InvoiceHistory.Documents,
            earlierImports,
            totals
        );
    }

    private static InvoiceLineResult LineResult(MatchedLine line) =>
        new(
            line.Invoice.LineId,
            line.OrderLine?.LineId,
            line.OrderLine is null ? null : MatchSource.Deterministic,
            line.Amounts.Net,
            line.Amounts.Tax,
            line.Quantities?.Ordered,
            line.Quantities?.Received,
            line.Quantities?.PreviouslyInvoiced,
            line.Quantities?.AvailableToInvoice,
            line.Quantities?.RemainingOnOrder
        );

    private static string QuestionText(RequiredFact fact) =>
        fact switch
        {
            RequiredFact.SupplierRegistrationId => "What is the supplier's registration ID?",
            RequiredFact.BuyerRegistrationId => "What is the buyer's registration ID?",
            RequiredFact.DocumentCurrency => "What currency does the invoice use?",
            RequiredFact.OrderReference => "Which purchase order does this invoice refer to?",
            RequiredFact.LineMatch => "Which order lines match the listed invoice lines?",
            RequiredFact.GoodsReceipts => "Which goods or services were received for these lines?",
            RequiredFact.BankAccount => "What bank account is printed on the invoice?",
            _ => throw new ArgumentOutOfRangeException(nameof(fact)),
        };
}
