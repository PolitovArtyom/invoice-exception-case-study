using System.Text.Json.Serialization;

namespace InvoiceDesk.Core.Model.Results;

public enum RequiredFact
{
    [JsonStringEnumMemberName("supplier.registration_id")]
    SupplierRegistrationId,

    [JsonStringEnumMemberName("buyer.registration_id")]
    BuyerRegistrationId,

    [JsonStringEnumMemberName("document.currency")]
    DocumentCurrency,
    OrderReference,
    LineMatch,
    GoodsReceipts,
    BankAccount,
}
