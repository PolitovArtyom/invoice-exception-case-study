using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Normalization;

namespace InvoiceDesk.Core.Rules.Bank;

internal static class BankAccountCheck
{
    private const string Code = "BANK_ACCOUNT";

    public static CheckOutcome Evaluate(InvoiceFacts facts, Supplier supplier)
    {
        var approved = supplier.ApprovedBankAccounts.Select(account => account.Iban).ToArray();
        CheckOutcome.Require(facts.Bank.Iban is not null &&
            approved.Any(iban => InvoiceText.Iban(iban) == InvoiceText.Iban(facts.Bank.Iban)),
            $"{Code} for an absent or unapproved account");

        return CheckOutcome.Pass(Code, facts.DocumentId, facts,
            new Dictionary<string, CheckOperand> { ["iban"] = CheckOperand.Invoice(facts.Bank.Iban) },
            new Dictionary<string, CheckOperand> { ["approved_accounts"] = CheckOperand.Company(approved) },
            "/bank/iban");
    }
}
