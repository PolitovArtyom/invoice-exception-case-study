using System.Text.Json.Serialization;
using InvoiceDesk.Core.Model.Json;

namespace InvoiceDesk.Core.Model.Results;

public sealed record CheckResult(
    CheckCode Code,
    string ObjectId,
    CheckStatus Status,
    Severity? Severity,
    CheckCode? DependsOn,
    [property: JsonConverter(typeof(OperandListConverter))] Operand[]? Actual,
    [property: JsonConverter(typeof(OperandListConverter))] Operand[]? Expected
);
