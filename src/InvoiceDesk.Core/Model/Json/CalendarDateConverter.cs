using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceDesk.Core.Model.Json;

internal sealed class CalendarDateConverter : JsonConverter<DateOnly>
{
    public const string InvalidDateMessage = "Expected a calendar date in YYYY-MM-DD format.";

    private const string Format = "yyyy-MM-dd";

    public static bool TryRead(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(
            value,
            Format,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date
        );

    public override DateOnly Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) =>
        TryRead(reader.GetString(), out var date)
            ? date
            : throw new JsonException(InvalidDateMessage);

    public override void Write(
        Utf8JsonWriter writer,
        DateOnly value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
}
