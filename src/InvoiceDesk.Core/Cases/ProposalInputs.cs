using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.Proposals;
using InvoiceDesk.Core.Rules.Matching;
using InvoiceDesk.Core.Validation;

namespace InvoiceDesk.Core.Cases;

internal static class ProposalInputs
{
    public static RegisteredProposal[] Register(
        RevisionInputs inputs,
        ProposalMatching? matching,
        StepProposal proposal,
        string path
    )
    {
        if (inputs.Facts is not { } facts || proposal.DocumentId != facts.DocumentId)
            throw new InvoiceValidationException(
                path + "/document_id",
                "Expected the active invoice document."
            );

        var order = matching!.FindOrder(proposal.OrderId)
            ?? throw new InvoiceValidationException(path + "/order_id", "Order does not exist.");
        var registration = new RegisteredProposal(proposal, inputs.Document!.ImportRevision, null);
        var existing = inputs.Proposals.FirstOrDefault(item =>
            item.Proposal.ProposalId == proposal.ProposalId
        );
        if (existing is not null && !SameMapping(existing.Proposal, proposal))
            throw new InvoiceValidationException(
                path + "/proposal_id",
                "This proposal ID already identifies a different proposal."
            );

        var automatic = facts
            .Lines.Zip(InvoiceMatches.FindLines(facts, order))
            .Where(pair => pair.Second is not null)
            .ToDictionary(
                pair => pair.First.LineId,
                pair => pair.Second!.OrderLine.LineId,
                StringComparer.Ordinal
            );
        var occupied = automatic.Values.ToHashSet(StringComparer.Ordinal);
        var invoiceLines = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (link, index) in proposal.LineMatches.Select((link, index) => (link, index)))
        {
            string linkPath = $"{path}/line_matches/{index}";
            if (!facts.Lines.Any(line => line.LineId == link.DocumentLineId))
                throw new InvoiceValidationException(
                    linkPath + "/document_line_id",
                    "Invoice line does not exist."
                );
            if (!invoiceLines.Add(link.DocumentLineId))
                throw new InvoiceValidationException(
                    linkPath + "/document_line_id",
                    "Each invoice line can appear only once."
                );
            if (!order.Lines.Any(line => line.LineId == link.OrderLineId))
                throw new InvoiceValidationException(
                    linkPath + "/order_line_id",
                    "Line does not belong to the proposed order."
                );

            if (automatic.TryGetValue(link.DocumentLineId, out string? target))
            {
                if (target != link.OrderLineId)
                    throw new InvoiceValidationException(
                        linkPath + "/order_line_id",
                        "Contradicts the deterministic line match."
                    );
            }
            else if (!occupied.Add(link.OrderLineId))
                throw new InvoiceValidationException(
                    linkPath + "/order_line_id",
                    "Each order line can be matched only once."
                );
        }

        if (!matching.Applies(registration, order))
            throw new InvoiceValidationException(
                path + "/order_id",
                "Contradicts the deterministic order match."
            );

        if (existing is null)
            return [.. inputs.Proposals, registration];
        return existing.ImportRevision == registration.ImportRevision
            ? inputs.Proposals
            : inputs.Proposals.Select(item => item == existing ? registration : item).ToArray();
    }

    public static RegisteredProposal[] Confirm(
        RevisionInputs inputs,
        ProposalMatching? matching,
        StepConfirmation confirmation,
        string path,
        bool requireApplicable
    )
    {
        var proposal = inputs.Proposals.FirstOrDefault(item =>
            item.Proposal.ProposalId == confirmation.ProposalId
        )
            ?? throw new InvoiceValidationException(
                path + "/proposal_id",
                "Proposal does not exist."
            );
        if (requireApplicable && matching?.Option(proposal).Applicable != true)
            throw new InvoiceValidationException(
                path + "/proposal_id",
                "Proposal no longer applies to the current invoice and company data."
            );
        if (proposal.Confirmation is not null)
            return inputs.Proposals;

        int sequence = inputs.Proposals.Max(item => item.Confirmation?.Sequence ?? 0) + 1;
        return inputs
            .Proposals.Select(item =>
                item == proposal
                    ? item with
                    {
                        Confirmation = new(confirmation.ConfirmedBy, sequence),
                    }
                    : item
            )
            .ToArray();
    }

    private static bool SameMapping(StepProposal left, StepProposal right) =>
        left.DocumentId == right.DocumentId
        && left.OrderId == right.OrderId
        && left.LineMatches.Length == right.LineMatches.Length
        && left.LineMatches.ToHashSet().SetEquals(right.LineMatches);
}
