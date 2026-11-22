using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Normalization;

namespace InvoiceDesk.Core.Rules.Bank;

internal static class BankAccountCheck
{
    public static InvoiceCheck Evaluate(InvoiceFacts facts, Supplier supplier)
    {
        var approved = supplier.ApprovedBankAccounts.Select(account => account.Iban).ToArray();
        Check.Require(facts.Bank.Iban is not null &&
            approved.Any(iban => InvoiceText.Iban(iban) == InvoiceText.Iban(facts.Bank.Iban)),
            "BANK_ACCOUNT for an absent or unapproved account");

        return Check.Pass("BANK_ACCOUNT", facts.DocumentId, facts,
            new Dictionary<string, CheckOperand> { ["iban"] = CheckOperand.Invoice(facts.Bank.Iban) },
            new Dictionary<string, CheckOperand> { ["approved_accounts"] = CheckOperand.Company(approved) },
            "/bank/iban");
    }
}
