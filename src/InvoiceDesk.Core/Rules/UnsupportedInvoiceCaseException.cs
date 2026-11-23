using System.Globalization;

namespace InvoiceDesk.Core.Rules;

public sealed class UnsupportedInvoiceCaseException : NotSupportedException
{
    private UnsupportedInvoiceCaseException(string path, string fieldMessage)
        : base($"{fieldMessage} at {path}.")
    {
        Path = path;
        FieldMessage = fieldMessage + ".";
    }

    public string Path { get; }

    public string FieldMessage { get; }

    internal static UnsupportedInvoiceCaseException AmountAboveLimit(decimal limit, string path) =>
        new(
            path,
            $"Unsupported: amount or quantity above {limit.ToString("N0", CultureInfo.InvariantCulture)}"
        );
}
