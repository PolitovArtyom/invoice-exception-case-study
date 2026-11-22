namespace InvoiceDesk.Core.Model.Results;

public sealed record InvoiceLineResult(
    string DocumentLineId, string? OrderLineId, string? MatchSource, string CalculatedNet, string CalculatedTax,
    string? Ordered, string? Received, string? PreviouslyInvoiced, string? AvailableToInvoice, string? RemainingOnOrder);

public sealed record InvoiceTotals(string CalculatedSubtotal, string CalculatedTaxTotal, string CalculatedTotal);
