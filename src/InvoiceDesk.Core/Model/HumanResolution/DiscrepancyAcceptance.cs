namespace InvoiceDesk.Core.Model.HumanResolution;

public sealed record DiscrepancyAcceptance(
    string AssessmentId,
    string Person,
    string Reason,
    DateTimeOffset RecordedAt
);
