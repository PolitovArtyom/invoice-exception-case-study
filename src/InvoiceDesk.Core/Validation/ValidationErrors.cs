using System.Text.Json.Nodes;
using Json.Schema;

namespace InvoiceDesk.Core.Validation;

internal static class ValidationErrors
{
    public static ValidationDetail[] From(EvaluationResults result, JsonNode? json, string prefix)
    {
        var details = Read(result, json, prefix).Distinct().ToArray();
        return details.Length == 0
            ? [new(prefix, "Input does not match the assignment schema.")]
            : details;
    }

    private static IEnumerable<ValidationDetail> Read(
        EvaluationResults result,
        JsonNode? json,
        string prefix
    )
    {
        if (
            result.IsValid
            || result.EvaluationPath.ToString().EndsWith("/if", StringComparison.Ordinal)
        )
            yield break;

        string pointer = result.InstanceLocation.ToString();
        if (result.Errors is not null)
        {
            foreach (var (keyword, message) in result.Errors)
            {
                if (keyword is "anyOf" or "oneOf" or "allOf" && result.Details is { Count: > 0 })
                    continue;

                if (
                    keyword == "required"
                    && JsonFields.TryResolve(json, pointer, out var instance)
                    && instance is JsonObject obj
                )
                {
                    foreach (
                        string field in RequiredNames(message)
                            .Where(field => !obj.ContainsKey(field))
                    )
                        yield return new(
                            prefix + pointer + "/" + JsonFields.Escape(field),
                            "Required property is missing."
                        );
                }
                else
                {
                    yield return new(
                        prefix + pointer,
                        keyword == "pattern" ? PatternMessage(result, message) : message
                    );
                }
            }
        }

        var children = result.Details ?? [];
        string alternativesPath = $"{result.EvaluationPath}/anyOf/";
        bool hasValidAlternative = children.Any(child => child.IsValid && IsAlternative(child));
        foreach (var child in children)
        {
            if (hasValidAlternative && IsAlternative(child))
                continue;

            foreach (var detail in Read(child, json, prefix))
                yield return detail;
        }

        bool IsAlternative(EvaluationResults child) =>
            child.EvaluationPath.ToString().StartsWith(alternativesPath, StringComparison.Ordinal);
    }

    private static string PatternMessage(EvaluationResults result, string message) =>
        result.SchemaLocation.Fragment switch
        {
            "#/$defs/money" => "Use a number with two decimal places, e.g. 6.40.",
            "#/$defs/quantity" =>
                "Use a positive number with up to three decimal places, e.g. 3.125.",
            "#/$defs/tax_rate" =>
                "Use a rate from 0 to 1 with up to four decimal places, e.g. 0.20.",
            _ => message,
        };

    private static IEnumerable<string> RequiredNames(string message) =>
        message.Split('"').Where((_, index) => index % 2 == 1);
}
