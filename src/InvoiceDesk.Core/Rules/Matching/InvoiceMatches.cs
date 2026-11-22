using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Rules.Normalization;

namespace InvoiceDesk.Core.Rules.Matching;

internal static class InvoiceMatches
{
    public static Supplier? FindSupplier(InvoiceFacts facts, CompanyData companyData) =>
        Unique(
            companyData.Suppliers.Suppliers.Where(supplier =>
                InvoiceText.Identifier(supplier.RegistrationId)
                == InvoiceText.Identifier(facts.Supplier.RegistrationId)
            )
        );

    public static Buyer? FindBuyer(InvoiceFacts facts, CompanyData companyData) =>
        Unique(companyData.Buyers.Buyers.Where(buyer => MatchesBuyer(facts.Buyer, buyer)));

    public static PurchaseOrder? FindOrder(InvoiceFacts facts, CompanyData companyData) =>
        Unique(
            companyData.PurchaseOrders.Orders.Where(order =>
                InvoiceText.Identifier(order.Reference)
                == InvoiceText.Identifier(facts.PurchaseOrderReference)
            )
        );

    public static OrderLineMatch?[] FindLines(InvoiceFacts facts, PurchaseOrder? order)
    {
        if (order is null)
            return new OrderLineMatch?[facts.Lines.Length];

        var matches = facts.Lines.Select(line => FindLine(line, order)).ToArray();
        var contested = matches
            .OfType<OrderLineMatch>()
            .GroupBy(match => match.OrderLine.LineId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        return matches
            .Select(match =>
                match is not null && contested.Contains(match.OrderLine.LineId) ? null : match
            )
            .ToArray();
    }

    private static OrderLineMatch? FindLine(InvoiceLine line, PurchaseOrder order) =>
        Unique(
            order
                .Lines.Select(candidate =>
                    MatchReason(line, candidate) is { } reason
                        ? new OrderLineMatch(candidate, reason)
                        : null
                )
                .OfType<OrderLineMatch>()
        );

    private static bool MatchesBuyer(InvoiceParty party, Buyer buyer) =>
        party.RegistrationId is not null
            ? InvoiceText.Identifier(party.RegistrationId)
                == InvoiceText.Identifier(buyer.RegistrationId)
            : party.LegalName is not null
                && (
                    InvoiceText.SameName(party.LegalName, buyer.LegalName)
                    || buyer.ExplicitAliases.Any(alias =>
                        InvoiceText.SameName(party.LegalName, alias)
                    )
                );

    private static LineMatchReason? MatchReason(InvoiceLine line, PurchaseOrderLine candidate)
    {
        if (
            line.Sku is not null
            && InvoiceText.Identifier(line.Sku) == InvoiceText.Identifier(candidate.Sku)
        )
            return LineMatchReason.Sku;
        if (candidate.ExplicitAliases.Any(alias => InvoiceText.SameName(line.Description, alias)))
            return LineMatchReason.ExplicitAlias;
        return InvoiceText.SameName(line.Description, candidate.Description)
            ? LineMatchReason.Description
            : null;
    }

    private static T? Unique<T>(IEnumerable<T> matches)
        where T : class
    {
        var firstTwo = matches.Take(2).ToArray();
        return firstTwo.Length == 1 ? firstTwo[0] : null;
    }

    internal sealed record OrderLineMatch(PurchaseOrderLine OrderLine, LineMatchReason Reason);
}

internal enum LineMatchReason
{
    Sku,
    ExplicitAlias,
    Description,
}
