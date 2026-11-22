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
        InvoiceParties parties,
        InvoiceImportMatch? importMatch
    )
    {
        string documentId = inputs.Facts!.DocumentId;
        var result = new InvoiceResult(
            "1.0",
            caseId,
            step,
            CaseMetadata.PolicyVersion,
            inputs.CompanyData.ContextVersion,
            inputs.Number,
            documentId,
            importMatch is null ? ImportDisposition.New
                : importMatch.IsDuplicate ? ImportDisposition.Duplicate
                : ImportDisposition.Conflict,
            importMatch?.Original.DocumentId,
            importMatch is { IsDuplicate: true } ? importMatch.Original.AssessmentId : null,
            null,
            null,
            [],
            null,
            [],
            null,
            []
        );
        if (importMatch is { IsDuplicate: true })
            return result;

        var context = BuildContext(inputs, parties, importMatch);
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

        return result with
        {
            MatchedOrderId = context.Order?.OrderId,
            OrderMatchSource = context.Order is null ? null : MatchSource.Deterministic,
            Lines = context.Lines.Select(LineResult).ToArray(),
            Totals = context.Totals,
            Checks = checks,
            Decision = InvoiceDecision.From(checks),
            Questions = InvoiceQuestions.From(
                verdicts.Select(item => (item.ObjectId, item.Verdict))
            ),
        };
    }

    private static CheckContext BuildContext(
        RevisionInputs inputs,
        InvoiceParties parties,
        InvoiceImportMatch? importMatch
    )
    {
        var facts = inputs.Facts!;
        var companyData = inputs.CompanyData;
        var order = InvoiceMatches.FindOrder(facts, companyData);

        var lines = facts
            .Lines.Zip(InvoiceMatches.FindLines(facts, order), (invoice, match) =>
            {
                var quantities =
                    match is null || order is null
                        ? null
                        : LineQuantities.Calculate(
                            order,
                            match.OrderLine,
                            companyData,
                            parties.Key
                        );
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
            parties.Supplier,
            parties.Buyer,
            order,
            lines,
            parties.Key,
            importMatch,
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
