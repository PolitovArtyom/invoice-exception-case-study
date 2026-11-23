using System.Text.Json.Serialization;
using InvoiceDesk.Core.Model.Json;

namespace InvoiceDesk.Core.Model.Results;

public sealed record InvoiceLineResult(
    string DocumentLineId,
    string? OrderLineId,
    MatchSource? MatchSource,
    [property: JsonConverter(typeof(MoneyStringConverter))] decimal CalculatedNet,
    [property: JsonConverter(typeof(MoneyStringConverter))] decimal CalculatedTax,
    decimal? Ordered,
    decimal? Received,
    decimal? PreviouslyInvoiced,
    decimal? AvailableToInvoice,
    decimal? RemainingOnOrder
);

public sealed record InvoiceTotals(
    [property: JsonConverter(typeof(MoneyStringConverter))] decimal CalculatedSubtotal,
    [property: JsonConverter(typeof(MoneyStringConverter))] decimal CalculatedTaxTotal,
    [property: JsonConverter(typeof(MoneyStringConverter))] decimal CalculatedTotal
);
