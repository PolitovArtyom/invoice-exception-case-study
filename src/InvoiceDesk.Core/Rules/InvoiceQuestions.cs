using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules;

internal static class InvoiceQuestions
{
    public static InvoiceQuestion[] From(
        IEnumerable<(string ObjectId, Verdict Verdict)> verdicts
    ) =>
        verdicts
            .Where(item => item.Verdict.RequiredFact is not null)
            .Select(item => (Fact: item.Verdict.RequiredFact!.Value, item.ObjectId))
            .Select(item => new InvoiceQuestion(
                item.Fact,
                [item.ObjectId],
                QuestionText(item.Fact)
            ))
            .ToArray();

    private static string QuestionText(RequiredFact fact) =>
        fact switch
        {
            RequiredFact.SupplierRegistrationId => "What is the supplier's registration ID?",
            RequiredFact.BuyerRegistrationId => "What is the buyer's registration ID?",
            RequiredFact.DocumentCurrency => "What currency does the invoice use?",
            RequiredFact.OrderReference => "Which purchase order does this invoice refer to?",
            RequiredFact.LineMatch => "Which order lines match the listed invoice lines?",
            RequiredFact.GoodsReceipts => "Which goods or services were received for these lines?",
            RequiredFact.BankAccount => "What bank account is printed on the invoice?",
            _ => throw new ArgumentOutOfRangeException(nameof(fact)),
        };
}
