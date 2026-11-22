using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules;

public sealed class UnsupportedInvoiceCaseException : NotSupportedException
{
    public UnsupportedInvoiceCaseException(string condition)
        : this("Not implemented yet", condition) { }

    private UnsupportedInvoiceCaseException(string reason, string condition)
        : base($"{reason}: {condition}.") { }

    internal static void Require(
        [DoesNotReturnIf(false)] bool passes,
        CheckCode code,
        string condition
    )
    {
        if (!passes)
            throw new UnsupportedInvoiceCaseException(
                $"{InvoiceJson.Write(code).Trim('"')} {condition}"
            );
    }

    internal static UnsupportedInvoiceCaseException AmountAboveLimit(decimal limit, string path) =>
        new(
            "Unsupported",
            $"amount or quantity above {limit.ToString("N0", CultureInfo.InvariantCulture)} at {path}"
        );
}
