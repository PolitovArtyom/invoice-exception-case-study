using System.Text.Json;
using System.Text.Json.Serialization;
using InvoiceDesk.Core.Model.HumanResolution;

namespace InvoiceDesk.Core.Cases.HumanResolution;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AcceptanceForm(
    int ExpectedRevision,
    string AssessmentId,
    string Person,
    string Reason
);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FactCorrectionForm(
    int ExpectedRevision,
    string FieldPath,
    JsonElement Value,
    MissingReason? MissingReason,
    string Person,
    string Reason
);
