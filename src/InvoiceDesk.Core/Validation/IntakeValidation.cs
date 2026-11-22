using System.Globalization;
using System.Text.Json.Nodes;

using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Rules;

namespace InvoiceDesk.Core.Validation;

public static class IntakeValidation
{
    private const decimal MaximumSupportedMagnitude = 1_000_000_000m;

    private static readonly HashSet<string> DecimalFields =
    [
        "quantity", "accepted_quantity", "unit_net_price", "tax_rate", "declared_net_amount",
        "declared_tax_amount", "declared_subtotal", "declared_tax_total", "declared_total"
    ];

    public static InvoiceFacts ReadFacts(JsonNode? json, string prefix)
    {
        AssignmentSchemas.Instance.Validate("extraction", json, prefix);
        ValidateValues(json, prefix);
        var evidenceEntries = json!["evidence"]!.AsArray();
        for (int index = 0; index < evidenceEntries.Count; index++)
        {
            if (!evidenceEntries[index]!["page"]!.AsValue().TryGetValue<int>(out _))
                throw new InvoiceValidationException($"{prefix}/evidence/{index}/page", "Page number is outside the supported integer range.");
        }

        var facts = InvoiceJson.Read<InvoiceFacts>(json.ToJsonString());
        RequireUnique(facts.Lines.Select(line => line.LineId), prefix + "/lines", "invoice line ID");

        var missingPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (missing, index) in facts.MissingFields.Select((field, index) => (field, index)))
        {
            if (!JsonFields.TryResolve(json, missing.FieldPath, out var value) || value is not null ||
                !missingPaths.Add(missing.FieldPath))
                throw new InvoiceValidationException($"{prefix}/missing_fields/{index}/field_path", "Expected a unique reference to a null invoice field.");
        }

        foreach (string path in NullFields(json, ""))
        {
            if (!missingPaths.Contains(path))
                throw new InvoiceValidationException(prefix + path, "A null invoice field requires a missing reason.");
        }

        foreach (var (evidence, index) in facts.Evidence.Select((item, index) => (item, index)))
        {
            if (evidence.DocumentId != facts.DocumentId)
                throw new InvoiceValidationException($"{prefix}/evidence/{index}/document_id", "Must reference this invoice document.");
            string fieldPath = evidence.FieldPath;
            if (fieldPath.StartsWith("/evidence/", StringComparison.Ordinal) ||
                fieldPath.StartsWith("/missing_fields/", StringComparison.Ordinal) ||
                !JsonFields.TryResolve(json, fieldPath, out var value) || value is not JsonValue)
                throw new InvoiceValidationException($"{prefix}/evidence/{index}/field_path", "Must reference a printed invoice field.");
        }

        return facts;
    }

    public static CompanySnapshot ReadCompanyData(JsonNode? json, string prefix)
    {
        AssignmentSchemas.Instance.Validate("context", json, prefix);
        ValidateValues(json, prefix);
        var snapshot = InvoiceJson.Read<CompanySnapshot>(json!.ToJsonString());
        RequireUnique(snapshot.PurchaseOrders.Orders.Select(order => order.OrderId), prefix + "/purchase_orders/orders", "order ID");
        RequireUnique(snapshot.PurchaseOrders.Orders.SelectMany(order => order.Lines).Select(line => line.LineId),
            prefix + "/purchase_orders/orders", "order line ID");
        RequireUnique(snapshot.Suppliers.Suppliers.Select(supplier => supplier.SupplierId), prefix + "/suppliers/suppliers", "supplier ID");
        RequireUnique(snapshot.Buyers.Buyers.Select(buyer => buyer.BuyerId), prefix + "/buyers/buyers", "buyer ID");
        return snapshot;
    }

    public static void ValidateDate(string value, string path)
    {
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new InvoiceValidationException(path, "Expected a calendar date in YYYY-MM-DD format.");
    }

    private static void ValidateValues(JsonNode? node, string path)
    {
        if (node is JsonArray array)
        {
            for (int index = 0; index < array.Count; index++)
                ValidateValues(array[index], $"{path}/{index}");
        }
        else if (node is JsonObject obj)
        {
            foreach (var (field, value) in obj)
            {
                string fieldPath = path + "/" + field;
                if (value is JsonValue text && field is "issue_date" or "due_date" or "accepted_at")
                    ValidateDate(text.GetValue<string>(), fieldPath);
                else if (value is JsonValue number && DecimalFields.Contains(field))
                    ValidateDecimal(number.GetValue<string>(), fieldPath);
                else
                    ValidateValues(value, fieldPath);
            }
        }
    }

    private static void ValidateDecimal(string text, string path)
    {
        var parts = text.Split('.');
        string whole = parts[0].TrimStart('0');
        string fraction = parts.Length == 2 ? parts[1].TrimEnd('0') : "";
        string canonical = (whole.Length == 0 ? "0" : whole) + (fraction.Length == 0 ? "" : "." + fraction);
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value) ||
            value > MaximumSupportedMagnitude)
            throw new UnsupportedInvoiceCaseException($"amount or quantity above {MaximumSupportedMagnitude.ToString(CultureInfo.InvariantCulture)} at {path}");

        if (value.ToString("0.############################", CultureInfo.InvariantCulture) != canonical)
            throw new InvoiceValidationException(path, "Decimal value is outside the supported exact representation.");
    }

    private static IEnumerable<string> NullFields(JsonNode? node, string path) => node switch
    {
        JsonObject obj => obj.SelectMany(field => NullFields(field.Value, path + "/" + JsonFields.Escape(field.Key))),
        JsonArray array => array.SelectMany((value, index) => NullFields(value, $"{path}/{index}")),
        null => [path],
        _ => []
    };

    private static void RequireUnique(IEnumerable<string> values, string path, string label)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (values.Any(value => !seen.Add(value)))
            throw new InvoiceValidationException(path, $"Each {label} must be unique.");
    }
}
