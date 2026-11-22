namespace InvoiceDesk.Core.Validation;

public sealed class InvoiceValidationException(IReadOnlyList<ValidationDetail> details)
    : Exception("Invoice input validation failed.")
{
    public InvoiceValidationException(string path, string message) : this([new(path, message)]) { }

    public IReadOnlyList<ValidationDetail> Details { get; } = details;

    public ValidationResponse Response => new(new("VALIDATION_ERROR", Details));
}

public sealed record ValidationDetail(string Path, string Message);

public sealed record ValidationError(string Code, IReadOnlyList<ValidationDetail> Details);

public sealed record ValidationResponse(ValidationError Error);
