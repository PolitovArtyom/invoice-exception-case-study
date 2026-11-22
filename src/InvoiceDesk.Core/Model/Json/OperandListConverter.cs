using System.Text.Json;
using System.Text.Json.Serialization;
using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Model.Json;

internal sealed class OperandListConverter : JsonConverter<Operand[]>
{
    public override Operand[] Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var saved = JsonSerializer.Deserialize<Dictionary<string, SavedOperand>>(
            ref reader,
            options
        )!;
        return saved
            .Select(item => new Operand(
                item.Key,
                ReadValue(item.Value.Value, options),
                item.Value.Source
            ))
            .ToArray();
    }

    public override void Write(
        Utf8JsonWriter writer,
        Operand[] operands,
        JsonSerializerOptions options
    )
    {
        writer.WriteStartObject();
        foreach (var operand in operands)
        {
            writer.WritePropertyName(operand.Name);
            JsonSerializer.Serialize(writer, new { operand.Value, operand.Source }, options);
        }
        writer.WriteEndObject();
    }

    private static object? ReadValue(JsonElement value, JsonSerializerOptions options) =>
        value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Array => value.Deserialize<string[]>(options),
            JsonValueKind.Object => value.Deserialize<Dictionary<string, string>>(options),
            _ => throw new JsonException("Unexpected comparison operand value."),
        };

    private sealed record SavedOperand(JsonElement Value, OperandSource Source);
}
