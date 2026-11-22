using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Normalization;

namespace InvoiceDesk.Core.Rules.Bank;

internal sealed class BankAccountCheck : IDocumentCheck
{
    public CheckCode Code => CheckCode.BankAccount;

    public Verdict Evaluate(CheckContext context)
    {
        string? iban = context.Facts.Bank.Iban;
        if (iban is null)
            return Verdict.MissingFact(RequiredFact.BankAccount);

        if (context.Supplier is not { } supplier)
            return Verdict.DependsOn(CheckCode.SupplierIdentity);

        string? normalized = InvoiceText.Iban(iban);
        var approved = supplier.ApprovedBankAccounts.Select(account => account.Iban).ToArray();

        var comparison = new Comparison(
            Actual: [Operand.Invoice("iban", iban)],
            Expected: [Operand.Directory("approved_accounts", approved)]
        );
        return approved.Any(account => InvoiceText.Iban(account) == normalized)
            ? Verdict.Pass(comparison)
            : Verdict.Fail(Severity.Blocked, comparison);
    }
}
