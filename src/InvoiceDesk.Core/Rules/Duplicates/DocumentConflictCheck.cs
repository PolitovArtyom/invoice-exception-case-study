using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Normalization;
using InvoiceDesk.Core.Rules.Parties;

namespace InvoiceDesk.Core.Rules.Duplicates;

internal static class DocumentConflictCheck
{
    public static InvoiceCheck Evaluate(InvoiceFacts facts, CompanySnapshot company, InvoiceBusinessKey key,
        IReadOnlyList<EarlierInvoice> earlierImports)
    {
        var historyMatches = company.InvoiceHistory.Documents.Where(key.Matches).Select(invoice => invoice.DocumentId).ToArray();
        var earlierMatches = earlierImports.Where(invoice => Matches(invoice, key)).Select(invoice => invoice.Facts.DocumentId).ToArray();
        Check.Require(historyMatches.Length == 0 && earlierMatches.Length == 0,
            "DOCUMENT_CONFLICT for a previously imported or counted invoice with the same business key");

        return Check.Pass("DOCUMENT_CONFLICT", facts.DocumentId, facts,
            new Dictionary<string, CheckOperand>
            {
                ["invoice_number"] = CheckOperand.Invoice(facts.Number),
                ["business_key"] = CheckOperand.Calculated(key),
                ["counted_matches"] = CheckOperand.Calculated(historyMatches),
                ["earlier_import_matches"] = CheckOperand.Calculated(earlierMatches)
            },
            new Dictionary<string, CheckOperand> { ["prior_matches"] = CheckOperand.Policy("none") },
            "/number", "/supplier/registration_id", "/buyer/registration_id", "/buyer/legal_name");
    }

    private static bool Matches(EarlierInvoice invoice, InvoiceBusinessKey key) =>
        InvoiceText.Identifier(invoice.Facts.Number) == key.Number &&
        SupplierIdentityCheck.Resolve(invoice.Facts, invoice.CompanyData)?.SupplierId == key.SupplierId &&
        BuyerIdentityCheck.Resolve(invoice.Facts, invoice.CompanyData)?.BuyerId == key.BuyerId;
}
