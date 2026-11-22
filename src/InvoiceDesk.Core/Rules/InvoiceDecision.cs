using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules;

internal static class InvoiceDecision
{
    internal const string Clear = "clear";

    public static string From(IReadOnlyList<InvoiceCheck> checks)
    {
        if (checks.Any(check => check is { Status: "fail", Severity: "blocked" }))
            return "blocked";
        if (checks.Any(check => check.Status == "unknown"))
            return "needs_information";
        if (checks.Any(check => check is { Status: "fail", Severity: "needs_review" }))
            return "needs_review";

        return Clear;
    }
}
