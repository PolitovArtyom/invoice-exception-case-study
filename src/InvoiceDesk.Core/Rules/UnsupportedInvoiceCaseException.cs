using System.Globalization;

namespace InvoiceDesk.Core.Rules;

public sealed class UnsupportedInvoiceCaseException : NotSupportedException
{
    public UnsupportedInvoiceCaseException(string condition) : this("Not implemented yet", condition)
    {
    }

    private UnsupportedInvoiceCaseException(string reason, string condition) : base($"{reason}: {condition}.")
    {
    }

    internal static UnsupportedInvoiceCaseException AmountAboveLimit(decimal limit, string path) =>
        new("Unsupported", $"amount or quantity above {limit.ToString("N0", CultureInfo.InvariantCulture)} at {path}");
}
