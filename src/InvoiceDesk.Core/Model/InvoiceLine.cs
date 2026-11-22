namespace InvoiceDesk.Core.Model;

public sealed record InvoiceLine(
    string LineId,
    string Description,
    string? Sku,
    decimal Quantity,
    string Unit,
    decimal UnitNetPrice,
    decimal DeclaredNetAmount,
    decimal TaxRate,
    decimal? DeclaredTaxAmount
);
