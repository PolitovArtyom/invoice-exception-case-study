using System.Text.Json;
using System.Text.Json.Nodes;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Json;
using InvoiceDesk.Core.Rules;

namespace InvoiceDesk.Core.Validation;

public static class IntakeValidation
{
    public static InvoiceFacts ReadFacts(JsonNode? json, string prefix)
    {
        var facts = Read<InvoiceFacts>("extraction", json, prefix);
        RequireUnique(
            facts.Lines.Select(line => line.LineId),
            prefix + "/lines",
            "invoice line ID"
        );

        var missingPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (
            var (missing, index) in facts.MissingFields.Select((field, index) => (field, index))
        )
        {
            if (
                !JsonFields.TryResolve(json!, missing.FieldPath, out var value)
                || value is not null
                || !missingPaths.Add(missing.FieldPath)
            )
                throw new InvoiceValidationException(
                    $"{prefix}/missing_fields/{index}/field_path",
                    "Expected a unique reference to a null invoice field."
                );
        }

        foreach (string path in NullFields(json, ""))
        {
            if (!missingPaths.Contains(path))
                throw new InvoiceValidationException(
                    prefix + path,
                    "A null invoice field requires a missing reason."
                );
        }

        foreach (var (evidence, index) in facts.Evidence.Select((item, index) => (item, index)))
        {
            if (evidence.DocumentId != facts.DocumentId)
                throw new InvoiceValidationException(
                    $"{prefix}/evidence/{index}/document_id",
                    "Must reference this invoice document."
                );
            string fieldPath = evidence.FieldPath;
            if (
                fieldPath.StartsWith("/evidence/", StringComparison.Ordinal)
                || fieldPath.StartsWith("/missing_fields/", StringComparison.Ordinal)
                || !JsonFields.TryResolve(json!, fieldPath, out var value)
                || value is not JsonValue
            )
                throw new InvoiceValidationException(
                    $"{prefix}/evidence/{index}/field_path",
                    "Must reference a printed invoice field."
                );
        }

        return facts;
    }

    public static CompanyData ReadCompanyData(JsonNode? json, string prefix)
    {
        var snapshot = Read<CompanyData>("context", json, prefix);
        RequireUnique(
            snapshot
                .PurchaseOrders.Orders.SelectMany(order => order.Lines)
                .Select(line => line.LineId),
            prefix + "/purchase_orders/orders",
            "order line ID"
        );
        return snapshot;
    }

    public static DateOnly ReadDate(string? value, string path) =>
        CalendarDateConverter.TryRead(value, out var date)
            ? date
            : throw new InvoiceValidationException(path, CalendarDateConverter.InvalidDateMessage);

    public static T Read<T>(string schema, JsonNode? json, string prefix = "")
    {
        AssignmentSchemas.Instance.Validate(schema, json, prefix);
        try
        {
            return InvoiceJson.ReadInput<T>(json!);
        }
        catch (AmountLimitException error)
        {
            throw UnsupportedInvoiceCaseException.AmountAboveLimit(
                InputDecimalStringConverter.MaximumSupportedMagnitude,
                Pointer(error, prefix)
            );
        }
        catch (JsonException error)
        {
            throw new InvoiceValidationException(Pointer(error, prefix), error.Message);
        }
    }

    private static string Pointer(JsonException error, string prefix)
    {
        string[] fields = (error.Path ?? "$").Split(
            ['$', '.', '[', ']'],
            StringSplitOptions.RemoveEmptyEntries
        );
        return prefix + string.Concat(fields.Select(field => "/" + JsonFields.Escape(field)));
    }

    private static IEnumerable<string> NullFields(JsonNode? node, string path) =>
        node switch
        {
            JsonObject obj => obj.SelectMany(field =>
                NullFields(field.Value, path + "/" + JsonFields.Escape(field.Key))
            ),
            JsonArray array => array.SelectMany(
                (value, index) => NullFields(value, $"{path}/{index}")
            ),
            null => [path],
            _ => [],
        };

    private static void RequireUnique(IEnumerable<string> values, string path, string label)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (values.Any(value => !seen.Add(value)))
            throw new InvoiceValidationException(path, $"Each {label} must be unique.");
    }
}
