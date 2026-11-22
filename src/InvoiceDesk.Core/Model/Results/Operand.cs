namespace InvoiceDesk.Core.Model.Results;

public sealed record Operand(string Name, object? Value, OperandSource Source)
{
    public static Operand Invoice(string name, object? value) =>
        new(name, value, OperandSource.Invoice);

    public static Operand Order(string name, object? value) =>
        new(name, value, OperandSource.Order);

    public static Operand Receipts(string name, object? value) =>
        new(name, value, OperandSource.Receipts);

    public static Operand History(string name, object? value) =>
        new(name, value, OperandSource.History);

    public static Operand Directory(string name, object? value) =>
        new(name, value, OperandSource.Directory);

    public static Operand Calculated(string name, object? value) =>
        new(name, value, OperandSource.Calculated);

    public static Operand Policy(string name, object? value) =>
        new(name, value, OperandSource.Policy);
}

public enum OperandSource
{
    Invoice,
    Order,
    Receipts,
    History,

    Directory,
    Calculated,
    Policy,
}
