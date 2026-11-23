using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using InvoiceDesk.Core.Model.Json;
using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Model;

public static class InvoiceJson
{
    public static JsonSerializerOptions Options { get; } =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Converters =
            {
                new DecimalStringConverter(),
                new CalendarDateConverter(),
                new JsonStringEnumConverter<CheckCode>(
                    JsonNamingPolicy.SnakeCaseUpper,
                    allowIntegerValues: false
                ),
                new JsonStringEnumConverter(
                    JsonNamingPolicy.SnakeCaseLower,
                    allowIntegerValues: false
                ),
            },
        };

    private static readonly JsonSerializerOptions InputOptions = WithDecimalConverter(
        new InputDecimalStringConverter()
    );

    private static readonly JsonSerializerOptions ComparisonOptions = WithDecimalConverter(
        new ComparableDecimalStringConverter()
    );

    internal static T ReadInput<T>(JsonNode json) =>
        json.Deserialize<T>(InputOptions) ?? throw new JsonException("A JSON value is required.");

    public static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new JsonException("A JSON value is required.");

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    internal static bool SameValues<T>(T left, T right) =>
        JsonSerializer.Serialize(left, ComparisonOptions)
        == JsonSerializer.Serialize(right, ComparisonOptions);

    private static JsonSerializerOptions WithDecimalConverter(DecimalStringConverter converter)
    {
        var options = new JsonSerializerOptions(Options);
        options.Converters.Insert(0, converter);
        return options;
    }

    private sealed class ComparableDecimalStringConverter : DecimalStringConverter
    {
        public override void Write(
            Utf8JsonWriter writer,
            decimal value,
            JsonSerializerOptions options
        ) => writer.WriteStringValue(value.ToString("G29", CultureInfo.InvariantCulture));
    }
}
