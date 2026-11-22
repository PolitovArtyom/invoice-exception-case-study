namespace InvoiceDesk.Core.Model.CompanyData;

public sealed record CompanySnapshot(
    string ContextVersion, PurchaseOrderDirectory PurchaseOrders, ReceiptDirectory GoodsReceipts,
    SupplierDirectory Suppliers, BuyerDirectory Buyers, InvoiceHistory InvoiceHistory);
