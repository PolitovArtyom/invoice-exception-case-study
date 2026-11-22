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
        IReadOnlyList<EarlierImport> earlierImports
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
            context.Totals,
            checks,
            InvoiceDecision.From(checks),
            InvoiceQuestions.From(verdicts.Select(item => (item.ObjectId, item.Verdict)))
        );
    }

    private static CheckContext BuildContext(
        RevisionInputs inputs,
        IReadOnlyList<EarlierImport> earlierImports
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
                    match is null || order is null
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
        var (subtotal, tax) = LineAmounts.Sum(lines.Select(line => line.Amounts));
        var totals = new InvoiceTotals(subtotal, tax, subtotal + tax);

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
}
