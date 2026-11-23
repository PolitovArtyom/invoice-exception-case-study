using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Normalization;
using InvoiceDesk.Core.Rules.Parties;

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

    public static PurchaseOrder? FindOrder(
        InvoiceFacts facts,
        CompanyData companyData,
        InvoiceParties parties
    )
    {
        if (facts.PurchaseOrderReference is { } reference)
            return Unique(
                companyData.PurchaseOrders.Orders.Where(order =>
                    InvoiceText.Identifier(order.Reference) == InvoiceText.Identifier(reference)
                )
            );

        if (parties.Supplier is not { } supplier)
            return null;

        return Unique(
            companyData.PurchaseOrders.Orders.Where(order =>
                order.Status == OrderStatus.Open
                && order.SupplierId == supplier.SupplierId
                && (parties.Buyer is null || order.BuyerId == parties.Buyer.BuyerId)
                && FindLines(facts, order).All(match => match is not null)
            )
        );
    }

    public static OrderLineMatch?[] FindLines(InvoiceFacts facts, PurchaseOrder order)
    {
        var candidates = facts
            .Lines.Select(line =>
                order
                    .Lines.Select(candidate =>
                        MatchReason(line, candidate) is { } reason
                            ? new OrderLineMatch(candidate, MatchSource.Deterministic, reason)
                            : null
                    )
                    .OfType<OrderLineMatch>()
                    .ToArray()
            )
            .ToArray();
        var contested = candidates
            .SelectMany(matches => matches)
            .GroupBy(match => match.OrderLine.LineId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        return candidates
            .Select(matches =>
                matches is [var match] && !contested.Contains(match.OrderLine.LineId) ? match : null
            )
            .ToArray();
    }

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
}

internal sealed record OrderLineMatch(
    PurchaseOrderLine OrderLine,
    MatchSource Source,
    LineMatchReason? Reason
);

internal enum LineMatchReason
{
    Sku,
    ExplicitAlias,
    Description,
}
