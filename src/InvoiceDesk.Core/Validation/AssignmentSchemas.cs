using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace InvoiceDesk.Core.Validation;

public sealed class AssignmentSchemas
{
    private readonly Dictionary<string, JsonSchema> _schemas = [];
    private readonly EvaluationOptions _options = new()
    {
        OutputFormat = OutputFormat.Hierarchical,
    };

    public static AssignmentSchemas Instance { get; } = new();

    private AssignmentSchemas()
    {
        var build = new BuildOptions();
        var assembly = typeof(AssignmentSchemas).Assembly;
        foreach (
            string resource in assembly
                .GetManifestResourceNames()
                .Where(name => name.StartsWith("Schemas.", StringComparison.Ordinal))
        )
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            string name = resource["Schemas.".Length..^".schema.json".Length];
            string json = reader.ReadToEnd();
            var schema = JsonSchema.FromText(json, build);
            build.SchemaRegistry.Register(schema);
            _schemas.Add(name, schema);

            if (name == "extraction")
                ExtractionContract = JsonNode.Parse(json)!.AsObject();
            if (name == "common")
                CommonDefinitions = JsonNode.Parse(json)!["$defs"]!.AsObject();
        }

        const string proposalItem =
            "https://invoice-exception-case-study.example/schemas/step-input.schema.json#/properties/proposals/items";
        _schemas.Add("proposal", new JsonSchemaBuilder().Ref(proposalItem).Build(build));

        foreach (var definition in CommonDefinitions)
            ExtractionContract["$defs"]![definition.Key] = definition.Value?.DeepClone();
        InlineCommonReferences(ExtractionContract);
    }

    private JsonObject CommonDefinitions { get; } = new();

    private JsonObject ExtractionContract { get; } = new();

    public JsonObject GetExtractionContract() => (JsonObject)ExtractionContract.DeepClone();

    public void Validate(string schema, JsonNode? json, string prefix = "")
    {
        var result = _schemas[schema].Evaluate(JsonSerializer.SerializeToElement(json), _options);
        if (!result.IsValid)
            throw new InvoiceValidationException(ValidationErrors.From(result, json, prefix));
    }

    private static void InlineCommonReferences(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            if (
                obj["$ref"]?.GetValue<string>() is string reference
                && reference.StartsWith("common.schema.json#", StringComparison.Ordinal)
            )
                obj["$ref"] = reference["common.schema.json".Length..];

            foreach (var child in obj.Select(property => property.Value).OfType<JsonNode>())
                InlineCommonReferences(child);
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array.OfType<JsonNode>())
                InlineCommonReferences(child);
        }
    }
}
