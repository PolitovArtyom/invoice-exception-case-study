using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Normalization;

namespace InvoiceDesk.Core.Rules.Parties;

internal static class BuyerIdentityCheck
{
    private const string Code = "BUYER_IDENTITY";

    public static Buyer? Resolve(InvoiceFacts facts, CompanyData companyData)
    {
        var matches = companyData.Buyers.Buyers.Where(buyer => Matches(facts.Buyer, buyer)).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    public static CheckOutcome Evaluate(InvoiceFacts facts, Buyer? buyer, PurchaseOrder order)
    {
        if (buyer is null || buyer.BuyerId != order.BuyerId)
            throw new UnsupportedInvoiceCaseException($"{Code} for a missing, ambiguous or different buyer");

        bool byRegistration = facts.Buyer.RegistrationId is not null;
        return CheckOutcome.Pass(Code, facts.DocumentId, facts,
            new Dictionary<string, CheckOperand>
            {
                ["registration_id"] = CheckOperand.Invoice(facts.Buyer.RegistrationId),
                ["legal_name"] = CheckOperand.Invoice(facts.Buyer.LegalName),
                ["resolved_buyer_id"] = CheckOperand.Calculated(buyer.BuyerId)
            },
            new Dictionary<string, CheckOperand>
            {
                ["buyer_id"] = CheckOperand.Order(order.BuyerId),
                ["registration_id"] = CheckOperand.Company(buyer.RegistrationId),
                ["legal_name"] = CheckOperand.Company(buyer.LegalName),
                ["explicit_aliases"] = CheckOperand.Company(buyer.ExplicitAliases)
            }, byRegistration ? "/buyer/registration_id" : "/buyer/legal_name");
    }

    private static bool Matches(InvoiceParty party, Buyer buyer) => party.RegistrationId is not null
        ? InvoiceText.Identifier(party.RegistrationId) == InvoiceText.Identifier(buyer.RegistrationId)
        : InvoiceText.SameName(party.LegalName, buyer.LegalName) ||
            buyer.ExplicitAliases.Any(alias => InvoiceText.SameName(party.LegalName, alias));
}
