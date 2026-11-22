namespace InvoiceDesk.Core.Rules;

public sealed class UnsupportedInvoiceCaseException(string condition)
    : NotSupportedException($"Not implemented yet: {condition}.");
