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
        InvoiceImportMatch? importMatch,
        ProposalMatches matching
    )
    {
        string documentId = inputs.Facts!.DocumentId;
        var disposition = importMatch is null ? ImportDisposition.New : ImportDisposition.Conflict;
        var result = ImportResult(
            caseId,
            step,
            inputs,
            documentId,
            disposition,
            importMatch?.Original
        );
        var context = BuildContext(inputs, parties, importMatch, matching);
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
            OrderMatchSource = context.OrderSource,
            Lines = context.Lines.Select(LineResult).ToArray(),
            Totals = context.Totals,
            Checks = checks,
            Decision = InvoiceDecision.From(checks),
            Questions = InvoiceQuestions.From(
                verdicts.Select(item => (item.ObjectId, item.Verdict))
            ),
        };
    }

    internal static InvoiceResult ImportResult(
        string caseId,
        int step,
        RevisionInputs inputs,
        string documentId,
        ImportDisposition disposition,
        OriginalInvoice? original
    ) =>
        new(
            "1.0",
            caseId,
            step,
            CaseMetadata.PolicyVersion,
            inputs.CompanyData.ContextVersion,
            inputs.Number,
            documentId,
            disposition,
            original?.DocumentId,
            disposition == ImportDisposition.Duplicate ? original?.AssessmentId : null,
            null,
            null,
            [],
            null,
            [],
            null,
            []
        );

    private static CheckContext BuildContext(
        RevisionInputs inputs,
        InvoiceParties parties,
        InvoiceImportMatch? importMatch,
        ProposalMatches matching
    )
    {
        var facts = inputs.Facts!;
        var companyData = inputs.CompanyData;
        var order = matching.Order;

        var lines = facts
            .Lines.Zip(matching.Lines, (invoice, match) =>
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
                    match?.Source,
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
            matching.OrderSource,
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
            line.Source,
            line.Amounts.Net,
            line.Amounts.Tax,
            line.Quantities?.Ordered,
            line.Quantities?.Received,
            line.Quantities?.PreviouslyInvoiced,
            line.Quantities?.AvailableToInvoice,
            line.Quantities?.RemainingOnOrder
        );
}
