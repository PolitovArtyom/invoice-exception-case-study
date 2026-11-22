using System.Text.Json.Serialization;

namespace InvoiceDesk.Core.Model.Results;

public enum CheckCode
{
    SupplierIdentity,
    BuyerIdentity,
    CurrencyMatch,
    OrderMatch,
    LineMatch,

    [JsonStringEnumMemberName("QTY_RECEIVED")]
    QuantityReceived,

    [JsonStringEnumMemberName("QTY_ORDER_REMAINING")]
    QuantityOrderRemaining,
    UnitPrice,
    LineArithmetic,
    TotalsArithmetic,
    BankAccount,
    DocumentConflict,
}
