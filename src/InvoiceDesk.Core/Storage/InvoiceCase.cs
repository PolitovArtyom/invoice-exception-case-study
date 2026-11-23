using InvoiceDesk.Core.Model;

namespace InvoiceDesk.Core.Storage;

internal sealed class InvoiceCase
{
    public required string Id { get; init; }

    public required CaseOrigin Origin { get; init; }

    public required DateOnly AsOf { get; init; }

    public required CaseSources Sources { get; init; }

    public int CurrentRevision { get; set; }

    public CaseMetadata Metadata => new(Id, Origin, AsOf, Sources.Items);

    public static InvoiceCase FromMetadata(
        CaseMetadata metadata,
        IReadOnlyList<string>? referencePdfPaths = null
    ) =>
        new()
        {
            Id = metadata.CaseId,
            Origin = metadata.Origin,
            AsOf = metadata.AsOf,
            Sources = new(metadata.Sources, referencePdfPaths),
        };
}

internal sealed record CaseSources(
    IReadOnlyList<InvoiceSource> Items,
    IReadOnlyList<string>? ReferencePdfPaths
);
