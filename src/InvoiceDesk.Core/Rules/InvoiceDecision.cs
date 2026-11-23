using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules;

internal static class InvoiceDecision
{
    public static Decision From(IReadOnlyList<CheckResult> checks)
    {
        if (checks.Any(check => check is { Status: CheckStatus.Fail, Severity: Severity.Blocked }))
            return Decision.Blocked;
        if (checks.Any(check => check.Status == CheckStatus.Unknown))
            return Decision.NeedsInformation;
        if (
            checks.Any(check =>
                check is { Status: CheckStatus.Fail, Severity: Severity.NeedsReview }
            )
        )
            return Decision.NeedsReview;

        return Decision.Clear;
    }
}
