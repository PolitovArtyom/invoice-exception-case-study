using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Cases;

internal static class CaseIssues
{
    public static CaseIssue[] For(InvoiceResult? result) =>
        result?.Checks.Where(IsIssue).Select(Resolve).ToArray() ?? [];

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
        return check is null ? null : Resolve(check);
    }

    private static bool IsIssue(CheckResult check) =>
        check is { Status: CheckStatus.Fail } or { Status: CheckStatus.Unknown, DependsOn: null };

    private static CaseIssue Resolve(CheckResult check) => new(check, Owner(check));

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

public sealed record CaseIssue(CheckResult Check, IssueOwner Owner);

public enum IssueOwner
{
    AccountsPayable,
    Procurement,
    Receiving,
}
