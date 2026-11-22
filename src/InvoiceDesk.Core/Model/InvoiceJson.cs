using System.Text.Json;

namespace InvoiceDesk.Core.Model;

public static class InvoiceJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException("A JSON value is required.");

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
}
