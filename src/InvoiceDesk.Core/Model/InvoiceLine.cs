namespace InvoiceDesk.Core.Model;

public sealed record InvoiceLine(
    string LineId, string Description, string? Sku, string Quantity, string Unit,
    string UnitNetPrice, string DeclaredNetAmount, string TaxRate, string? DeclaredTaxAmount);
