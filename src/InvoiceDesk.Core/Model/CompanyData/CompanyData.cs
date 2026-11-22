namespace InvoiceDesk.Core.Model.CompanyData;

public sealed record CompanyData(
    string ContextVersion,
    PurchaseOrderDirectory PurchaseOrders,
    ReceiptDirectory GoodsReceipts,
    SupplierDirectory Suppliers,
    BuyerDirectory Buyers,
    InvoiceHistory InvoiceHistory
);
