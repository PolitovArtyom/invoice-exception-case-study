using InvoiceDesk.Core.Model.HumanResolution;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Storage;

namespace InvoiceDesk.Core.Cases.HumanResolution;

internal static class ResolutionEligibility
{
    public static bool CanAccept(
        CaseRevision assessed,
        DiscrepancyAcceptance? acceptance,
        bool inputsChanged
    ) =>
        acceptance is null
        && !inputsChanged
        && assessed.Result is { Decision: Decision.NeedsReview } result
        && result.OrderMatchSource != MatchSource.AgentProposal
        && result.Lines.All(line => line.MatchSource != MatchSource.AgentProposal);
}
