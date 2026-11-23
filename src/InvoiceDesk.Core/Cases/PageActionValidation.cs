using InvoiceDesk.Core.Storage;
using InvoiceDesk.Core.Validation;

namespace InvoiceDesk.Core.Cases;

internal static class PageActionValidation
{
    public static string RequirePerson(string? person, string path)
    {
        if (string.IsNullOrWhiteSpace(person) || person.Length > 128)
            throw new InvoiceValidationException(path, "Enter a name of up to 128 characters.");
        return person.Trim();
    }

    public static string RequireReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvoiceValidationException("/reason", "Enter a reason.");
        return reason.Trim();
    }

    public static void RequireLatestRevision(int expected, CaseRevision current)
    {
        if (expected != current.Number)
            throw new InvoiceValidationException(
                "/expected_revision",
                "The case changed. Open the latest revision and review the action again."
            );
    }
}
