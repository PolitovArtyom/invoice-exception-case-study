using System.Globalization;

namespace InvoiceDesk.Core.Rules;

internal static class InvoiceDecimal
{
    public static decimal Parse(string value) => decimal.Parse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    public static decimal RoundMoney(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    public static string Money(decimal value) => value.ToString("F2", CultureInfo.InvariantCulture);

    public static string Quantity(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
}
