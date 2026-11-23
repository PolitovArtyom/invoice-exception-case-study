using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Cases;

internal static class CaseIssues
{
    public static CaseIssue[] For(InvoiceResult? result, InvoiceFacts? facts) =>
        result is null || facts is null
            ? []
            : result
                .Checks.Where(IsIssue)
                .Select(check => new CaseIssue(
                    check,
                    Owner(check),
                    SourceFacts(check, result, facts)
                ))
                .ToArray();

    public static CaseIssue? MainIssue(InvoiceResult? result)
    {
        var check = result?.Decision switch
        {
            Decision.Blocked => result.Checks.First(check => check.Severity == Severity.Blocked),
            Decision.NeedsInformation => result.Checks.First(check =>
                check.Status == CheckStatus.Unknown && IsIssue(check)
            ),
            Decision.NeedsReview => result.Checks.First(check =>
                check.Severity == Severity.NeedsReview
            ),
            _ => null,
        };
        return check is null ? null : new(check, Owner(check), []);
    }

    private static bool IsIssue(CheckResult check) =>
        check is { Status: CheckStatus.Fail } or { Status: CheckStatus.Unknown, DependsOn: null };

    private static CaseIssueFact[] SourceFacts(
        CheckResult check,
        InvoiceResult result,
        InvoiceFacts facts
    )
    {
        if (check.Status != CheckStatus.Fail)
            return [];

        var sources = new List<CaseIssueFact>();
        if (check.Code == CheckCode.TotalsArithmetic && result.Totals is { } totals)
        {
            AddDifference(
                "/declared_subtotal",
                "Subtotal",
                facts.DeclaredSubtotal,
                totals.CalculatedSubtotal
            );
            AddDifference(
                "/declared_tax_total",
                "Tax total",
                facts.DeclaredTaxTotal,
                totals.CalculatedTaxTotal
            );
            AddDifference(
                "/declared_total",
                "Invoice total",
                facts.DeclaredTotal,
                totals.CalculatedTotal
            );
        }
        else if (check.Code is CheckCode.UnitPrice or CheckCode.LineArithmetic)
        {
            int index = Array.FindIndex(facts.Lines, line => line.LineId == check.ObjectId);
            var line = facts.Lines[index];
            string path = $"/lines/{index}";
            if (check.Code == CheckCode.LineArithmetic)
                Add($"{path}/quantity", "Quantity", line.Quantity);
            Add($"{path}/unit_net_price", "Unit net price", line.UnitNetPrice);
            if (check.Code == CheckCode.LineArithmetic)
            {
                var calculated = result.Lines.First(item => item.DocumentLineId == line.LineId);
                if (line.DeclaredTaxAmount is { } tax && tax != calculated.CalculatedTax)
                    Add($"{path}/tax_rate", "Tax rate", line.TaxRate);
                AddDifference(
                    $"{path}/declared_net_amount",
                    "Net",
                    line.DeclaredNetAmount,
                    calculated.CalculatedNet
                );
                AddDifference(
                    $"{path}/declared_tax_amount",
                    "Line tax",
                    line.DeclaredTaxAmount,
                    calculated.CalculatedTax
                );
            }
        }
        return sources.ToArray();

        void Add(string path, string label, decimal value, decimal? calculated = null) =>
            sources.Add(
                new(
                    path,
                    label,
                    value,
                    calculated,
                    facts.Evidence.Where(quote => quote.FieldPath == path).ToArray()
                )
            );

        void AddDifference(string path, string label, decimal? value, decimal calculated)
        {
            if (value is { } amount && amount != calculated)
                Add(path, label, amount, calculated);
        }
    }

    private static IssueOwner Owner(CheckResult check) =>
        check.Code switch
        {
            CheckCode.LineMatch when IsSameUnitProposal(check) => IssueOwner.AccountsPayable,
            CheckCode.LineMatch
            or CheckCode.QuantityOrderRemaining
            or CheckCode.UnitPrice
            or CheckCode.CurrencyMatch => IssueOwner.Procurement,
            CheckCode.QuantityReceived => IssueOwner.Receiving,
            _ => IssueOwner.AccountsPayable,
        };

    private static bool IsSameUnitProposal(CheckResult check) =>
        check.Status == CheckStatus.Fail
        && Unit(check.Actual) is string invoiceUnit
        && invoiceUnit == Unit(check.Expected);

    private static string? Unit(Operand[]? operands) =>
        operands?.FirstOrDefault(operand => operand.Name == "unit")?.Value as string;
}

public sealed record CaseIssue(CheckResult Check, IssueOwner Owner, CaseIssueFact[] Facts);

public sealed record CaseIssueFact(
    string Path,
    string Label,
    decimal Value,
    decimal? CalculatedValue,
    Evidence[] Quotes
);

public enum IssueOwner
{
    AccountsPayable,
    Procurement,
    Receiving,
}
