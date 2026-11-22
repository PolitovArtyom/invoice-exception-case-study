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
                    yield return new(prefix + pointer, message);
                }
            }
        }

        foreach (var child in result.Details ?? [])
        {
            foreach (var detail in Read(child, json, prefix))
                yield return detail;
        }
    }

    private static IEnumerable<string> RequiredNames(string message) =>
        message.Split('"').Where((_, index) => index % 2 == 1);
}
