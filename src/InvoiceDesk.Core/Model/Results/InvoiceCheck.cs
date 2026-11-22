using System.Text.Json;

namespace InvoiceDesk.Core.Model.Results;

public sealed record InvoiceCheck(
    string Code, string ObjectId, string Status, string? Severity, string? DependsOn,
    IReadOnlyDictionary<string, CheckOperand> Actual, IReadOnlyDictionary<string, CheckOperand> Expected,
    string[] EvidenceRefs);

public sealed record CheckOperand(JsonElement Value, string Source)
{
    public static CheckOperand Invoice<T>(T value) => Create(value, "invoice");

    public static CheckOperand Company<T>(T value) => Create(value, "company_data");

    public static CheckOperand Order<T>(T value) => Create(value, "order");

    public static CheckOperand Receipts<T>(T value) => Create(value, "receipts");

    public static CheckOperand History<T>(T value) => Create(value, "history");

    public static CheckOperand Calculated<T>(T value) => Create(value, "calculated");

    public static CheckOperand Policy<T>(T value) => Create(value, "policy");

    private static CheckOperand Create<T>(T value, string source) =>
        new(JsonSerializer.SerializeToElement(value, InvoiceJson.Options), source);
}
