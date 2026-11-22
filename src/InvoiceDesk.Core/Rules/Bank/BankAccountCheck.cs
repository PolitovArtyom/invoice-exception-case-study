using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Normalization;

namespace InvoiceDesk.Core.Rules.Bank;

internal sealed class BankAccountCheck : IDocumentCheck
{
    public CheckCode Code => CheckCode.BankAccount;

    public Verdict Evaluate(CheckContext context)
    {
        var supplier = context.Supplier;
        UnsupportedInvoiceCaseException.Require(
            supplier is not null,
            Code,
            "without a resolved supplier"
        );

        string? iban = context.Facts.Bank.Iban;
        string? normalized = InvoiceText.Iban(iban);
        var approved = supplier.ApprovedBankAccounts.Select(account => account.Iban).ToArray();
        UnsupportedInvoiceCaseException.Require(
            normalized is not null
                && approved.Any(account => InvoiceText.Iban(account) == normalized),
            Code,
            "for an absent or unapproved account"
        );

        return Verdict.Pass(
            new Comparison(
                Actual: [Operand.Invoice("iban", iban)],
                Expected: [Operand.Directory("approved_accounts", approved)]
            )
        );
    }
}
