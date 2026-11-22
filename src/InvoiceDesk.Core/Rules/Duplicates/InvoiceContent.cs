using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Rules.Normalization;

namespace InvoiceDesk.Core.Rules.Duplicates;

internal static class InvoiceContent
{
    public static NormalizedInvoiceFacts Normalize(InvoiceFacts facts) =>
        new(
            facts.IssueDate,
            facts.DueDate,
            facts.Currency,
            InvoiceText.Identifier(facts.PurchaseOrderReference),
            InvoiceText.Iban(facts.Bank.Iban),
            facts.DeclaredSubtotal,
            facts.DeclaredTaxTotal,
            facts.DeclaredTotal,
            facts
                .Lines.Select(line => new NormalizedInvoiceLine(
                    InvoiceText.Identifier(line.Sku),
                    line.Quantity,
                    line.Unit,
                    line.UnitNetPrice,
                    line.TaxRate,
                    line.DeclaredNetAmount,
                    line.DeclaredTaxAmount
                ))
                .ToArray()
        );

    public static NormalizedInvoiceFacts Normalize(NormalizedInvoiceFacts facts) =>
        facts with
        {
            PurchaseOrderReference = InvoiceText.Identifier(facts.PurchaseOrderReference),
            Iban = InvoiceText.Iban(facts.Iban),
            Lines = facts
                .Lines.Select(line => line with { Sku = InvoiceText.Identifier(line.Sku) })
                .ToArray(),
        };

    public static (
        NormalizedInvoiceLine[] Original,
        NormalizedInvoiceLine[] Current
    ) LineDifferences(NormalizedInvoiceFacts original, NormalizedInvoiceFacts current) =>
        (Subtract(original.Lines, current.Lines), Subtract(current.Lines, original.Lines));

    public static bool Equal(NormalizedInvoiceFacts original, NormalizedInvoiceFacts current) =>
        SameHeader(original, current)
        && original.Lines.Length == current.Lines.Length
        && Subtract(original.Lines, current.Lines).Length == 0;

    private static bool SameHeader(
        NormalizedInvoiceFacts original,
        NormalizedInvoiceFacts current
    ) =>
        original.IssueDate == current.IssueDate
        && original.DueDate == current.DueDate
        && original.Currency == current.Currency
        && original.PurchaseOrderReference == current.PurchaseOrderReference
        && original.Iban == current.Iban
        && original.DeclaredSubtotal == current.DeclaredSubtotal
        && original.DeclaredTaxTotal == current.DeclaredTaxTotal
        && original.DeclaredTotal == current.DeclaredTotal;

    private static bool SameLine(NormalizedInvoiceLine original, NormalizedInvoiceLine current) =>
        original.Sku == current.Sku
        && original.Quantity == current.Quantity
        && original.Unit == current.Unit
        && original.UnitNetPrice == current.UnitNetPrice
        && original.TaxRate == current.TaxRate
        && original.DeclaredNetAmount == current.DeclaredNetAmount
        && original.DeclaredTaxAmount == current.DeclaredTaxAmount;

    private static NormalizedInvoiceLine[] Subtract(
        NormalizedInvoiceLine[] lines,
        NormalizedInvoiceLine[] other
    )
    {
        var unmatched = other.ToList();
        var remaining = new List<NormalizedInvoiceLine>();
        foreach (var line in lines)
        {
            int match = unmatched.FindIndex(candidate => SameLine(line, candidate));
            if (match < 0)
                remaining.Add(line);
            else
                unmatched.RemoveAt(match);
        }

        return remaining.ToArray();
    }
}
