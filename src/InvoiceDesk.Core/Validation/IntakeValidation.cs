using System.Text.Json;
using System.Text.Json.Nodes;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Json;
using InvoiceDesk.Core.Rules;

namespace InvoiceDesk.Core.Validation;

public static class IntakeValidation
{
    public static StepInput ReadStep(string input, bool includeDocument = true)
    {
        JsonNode? json;
        try
        {
            json = JsonNode.Parse(input);
        }
        catch (JsonException)
        {
            throw new InvoiceValidationException("", "Expected one JSON step input.");
        }

        if (!includeDocument && json is JsonObject envelope)
            envelope.Remove("document");

        var step = Read<StepInput>("step-input", json);
        if (step.Context is { } context)
            ValidateCompanyData(context, "/context");
        if (step.Document is { } document)
            ValidateFacts(document.Facts, json!["document"]!["facts"]!, "/document/facts");
        return step;
    }

    public static InvoiceFacts ReadAgentFacts(JsonNode? json, string prefix) =>
        ReadFacts(json, prefix, requireCompleteReading: true);

    internal static InvoiceFacts ReadFacts(
        JsonNode? json,
        string prefix,
        bool requireCompleteReading = false
    )
    {
        var facts = Read<InvoiceFacts>("extraction", json, prefix);
        ValidateFacts(facts, json!, prefix, requireCompleteReading);
        return facts;
    }

    private static void ValidateFacts(
        InvoiceFacts facts,
        JsonNode json,
        string prefix,
        bool requireCompleteReading = false
    )
    {
        RequireUnique(
            facts.Lines.Select(line => line.LineId),
            prefix + "/lines",
            "invoice line ID"
        );

        ValidateCode(
            facts.Currency,
            3,
            prefix + "/currency",
            "Use a three-letter currency code, e.g. EUR."
        );
        ValidateCode(
            facts.Supplier.Country,
            2,
            prefix + "/supplier/country",
            "Use a two-letter country code, e.g. DE."
        );
        ValidateCode(
            facts.Buyer.Country,
            2,
            prefix + "/buyer/country",
            "Use a two-letter country code, e.g. DE."
        );

        var missingPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (
            var (missing, index) in facts.MissingFields.Select((field, index) => (field, index))
        )
        {
            if (
                !JsonFields.TryResolve(json, missing.FieldPath, out var value)
                || value is not null
                || !missingPaths.Add(missing.FieldPath)
            )
                throw new InvoiceValidationException(
                    $"{prefix}/missing_fields/{index}/field_path",
                    "Expected a unique reference to a null invoice field."
                );
        }

        var printedFields = requireCompleteReading
            ? JsonFields.Leaves(json, "").Where(field => IsPrintedFact(field.Path)).ToArray()
            : [];
        if (
            printedFields
                .FirstOrDefault(field => field.Value is null && !missingPaths.Contains(field.Path))
                .Path is
            { } fieldWithoutReason
        )
            throw new InvoiceValidationException(
                prefix + fieldWithoutReason,
                "A null invoice field requires a missing reason."
            );

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
                || !JsonFields.TryResolve(json, fieldPath, out var value)
                || (value is not JsonValue && (value is not null || requireCompleteReading))
            )
                throw new InvoiceValidationException(
                    $"{prefix}/evidence/{index}/field_path",
                    "Must reference a printed invoice field."
                );
        }

        if (requireCompleteReading)
        {
            var citedPaths = facts
                .Evidence.Select(evidence => evidence.FieldPath)
                .ToHashSet(StringComparer.Ordinal);
            var uncitedFacts = printedFields
                .Where(field => field.Value is not null && !citedPaths.Contains(field.Path))
                .Select(field => new ValidationDetail(
                    prefix + field.Path,
                    "A printed invoice field requires a page and excerpt."
                ))
                .ToArray();
            if (uncitedFacts.Length > 0)
                throw new InvoiceValidationException(uncitedFacts);
        }
    }

    private static bool IsPrintedFact(string path) =>
        path is not ("/schema_version" or "/document_id")
        && !path.EndsWith("/line_id", StringComparison.Ordinal)
        && !path.StartsWith("/evidence/", StringComparison.Ordinal)
        && !path.StartsWith("/missing_fields/", StringComparison.Ordinal);

    private static void ValidateCode(string? value, int length, string path, string message)
    {
        if (value is not null && value.Length != length)
            throw new InvoiceValidationException(path, message);
    }

    public static CompanyData ReadCompanyData(JsonNode? json, string prefix)
    {
        var snapshot = Read<CompanyData>("context", json, prefix);
        ValidateCompanyData(snapshot, prefix);
        return snapshot;
    }

    internal static void ValidateCompanyData(CompanyData snapshot, string prefix) =>
        RequireUnique(
            snapshot
                .PurchaseOrders.Orders.SelectMany(order => order.Lines)
                .Select(line => line.LineId),
            prefix + "/purchase_orders/orders",
            "order line ID"
        );

    public static T Read<T>(string schema, JsonNode? json, string prefix = "")
    {
        AssignmentSchemas.Instance.Validate(schema, json, prefix);
        return Read<T>(json, prefix);
    }

    public static T Read<T>(JsonNode? json, string prefix)
    {
        if (json is null)
            throw new InvoiceValidationException(prefix, "A JSON value is required.");

        try
        {
            return InvoiceJson.ReadInput<T>(json);
        }
        catch (AmountLimitException error)
        {
            throw UnsupportedInvoiceCaseException.AmountAboveLimit(
                error.Maximum,
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

    private static void RequireUnique(IEnumerable<string> values, string path, string label)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (values.Any(value => !seen.Add(value)))
            throw new InvoiceValidationException(path, $"Each {label} must be unique.");
    }
}
