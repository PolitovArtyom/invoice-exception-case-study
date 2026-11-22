using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceDesk.Core.Model.Json;

internal class DecimalStringConverter : JsonConverter<decimal>
{
    public override decimal Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) =>
        decimal.Parse(
            reader.GetString()!,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture
        );

    public override void Write(
        Utf8JsonWriter writer,
        decimal value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
}

internal sealed class InputDecimalStringConverter : DecimalStringConverter
{
    internal const decimal MaximumSupportedMagnitude = 1_000_000_000m;

    public override decimal Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        if (
            !decimal.TryParse(
                reader.GetString(),
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out decimal value
            )
            || value > MaximumSupportedMagnitude
        )
            throw new AmountLimitException();

        return value;
    }
}

internal sealed class AmountLimitException : JsonException;

internal sealed class MoneyStringConverter : DecimalStringConverter
{
    public override void Write(
        Utf8JsonWriter writer,
        decimal value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(value.ToString("F2", CultureInfo.InvariantCulture));
}
