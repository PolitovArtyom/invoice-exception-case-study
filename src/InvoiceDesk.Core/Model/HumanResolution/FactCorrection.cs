namespace InvoiceDesk.Core.Model.HumanResolution;

public sealed record FactCorrection(
    string FieldPath,
    string? PreviousValue,
    string? Value,
    MissingReason? MissingReason,
    string Person,
    string Reason
);
