using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules;

internal sealed record CheckOutcome(InvoiceCheck Check, InvoiceQuestion? Question = null)
{
    public static CheckOutcome Pass(string code, string objectId, InvoiceFacts facts,
        IReadOnlyDictionary<string, CheckOperand> actual, IReadOnlyDictionary<string, CheckOperand> expected,
        params string[] fields)
    {
        var evidenceRefs = facts.Evidence.Select((evidence, index) => (evidence, index))
            .Where(item => fields.Contains(item.evidence.FieldPath, StringComparer.Ordinal))
            .Select(item => $"/evidence/{item.index}")
            .ToArray();

        return new(new InvoiceCheck(code, objectId, "pass", null, null, actual, expected, evidenceRefs));
    }

    public static void Require(bool passes, string condition)
    {
        if (!passes)
            throw new UnsupportedInvoiceCaseException(condition);
    }
}
