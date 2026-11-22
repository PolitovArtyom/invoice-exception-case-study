using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Proposals;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules.Parties;

namespace InvoiceDesk.Core.Rules.Matching;

internal sealed record ProposalMatches(
    PurchaseOrder? Order,
    MatchSource? OrderSource,
    OrderLineMatch?[] Lines
);

internal sealed record ProposalOption(
    RegisteredProposal Proposal,
    PurchaseOrder? Order,
    bool Applicable
);

internal sealed class ProposalMatching(
    InvoiceFacts facts,
    int importRevision,
    CompanyData companyData,
    InvoiceParties parties
)
{
    private readonly PurchaseOrder? _deterministic = InvoiceMatches.FindOrder(
        facts,
        companyData,
        parties
    );

    public PurchaseOrder? FindOrder(string orderId) =>
        companyData.PurchaseOrders.Orders.FirstOrDefault(order => order.OrderId == orderId);

    public bool Applies(RegisteredProposal proposal, PurchaseOrder? order) =>
        proposal.ImportRevision == importRevision
        && order is not null
        && (_deterministic is null || _deterministic.OrderId == order.OrderId)
        && proposal.Proposal.LineMatches.All(link =>
            order.Lines.Any(line => line.LineId == link.OrderLineId)
        );

    public ProposalOption Option(RegisteredProposal proposal)
    {
        var order = FindOrder(proposal.Proposal.OrderId);
        return new(proposal, order, Applies(proposal, order));
    }

    public ProposalOption[] Options(RegisteredProposal[] proposals) =>
        proposals
            .Reverse()
            .Where(proposal => proposal.ImportRevision == importRevision)
            .Select(Option)
            .ToArray();

    public static ProposalOption? Effective(ProposalOption[] options) =>
        options
            .Where(option => option.Applicable)
            .OrderByDescending(option => option.Proposal.Confirmation?.Sequence)
            .FirstOrDefault();

    public ProposalMatches Resolve(RegisteredProposal[] proposals)
    {
        var selection = Effective(Options(proposals));
        var order = _deterministic ?? selection?.Order;
        if (order is null)
            return new(null, null, new OrderLineMatch?[facts.Lines.Length]);

        var proposalSource = selection?.Proposal.Confirmation is null
            ? MatchSource.AgentProposal
            : MatchSource.HumanConfirmation;
        var lines = InvoiceMatches.FindLines(facts, order);
        if (selection?.Proposal is { } selected)
        {
            var occupied = lines
                .OfType<OrderLineMatch>()
                .Select(match => match.OrderLine.LineId)
                .ToHashSet(StringComparer.Ordinal);
            var proposedLines = selected.Proposal.LineMatches.ToDictionary(
                link => link.DocumentLineId,
                link => link.OrderLineId,
                StringComparer.Ordinal
            );
            for (int index = 0; index < lines.Length; index++)
            {
                if (
                    lines[index] is null
                    && proposedLines.TryGetValue(facts.Lines[index].LineId, out string? target)
                    && occupied.Add(target)
                )
                    lines[index] = new(
                        order.Lines.First(line => line.LineId == target),
                        proposalSource,
                        null
                    );
            }
        }

        return new(
            order,
            _deterministic is null ? proposalSource : MatchSource.Deterministic,
            lines
        );
    }
}
