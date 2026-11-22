using InvoiceDesk.Core.Model;

namespace InvoiceDesk.Core.Storage;

internal sealed class InvoiceCase
{
    public required string Id { get; init; }

    public required string Origin { get; init; }

    public required string AsOf { get; init; }

    public string PolicyVersion { get; init; } = CaseMetadata.PolicyVersion;

    public required CaseSources Sources { get; init; }

    public int CurrentRevision { get; set; }

    public CaseMetadata Metadata => new(Id, Origin, AsOf, Sources.Items);
}

internal sealed record CaseSources(IReadOnlyList<InvoiceSource> Items);
