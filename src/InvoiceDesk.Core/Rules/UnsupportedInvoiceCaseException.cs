using System.Globalization;

namespace InvoiceDesk.Core.Rules;

public sealed class UnsupportedInvoiceCaseException : NotSupportedException
{
    private UnsupportedInvoiceCaseException(string message)
        : base(message) { }

    internal static UnsupportedInvoiceCaseException AmountAboveLimit(decimal limit, string path) =>
        new(
            $"Unsupported: amount or quantity above {limit.ToString("N0", CultureInfo.InvariantCulture)} at {path}."
        );
}
