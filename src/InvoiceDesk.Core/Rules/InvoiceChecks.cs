using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules;

internal interface IDocumentCheck
{
    CheckCode Code { get; }

    Verdict Evaluate(CheckContext context);
}

internal interface ILineCheck
{
    CheckCode Code { get; }

    Verdict Evaluate(CheckContext context, MatchedLine line);
}
