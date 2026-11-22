using System.Text.Json.Nodes;
using InvoiceDesk.Core.Cases;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Validation;

namespace InvoiceDesk.Web.CaseLoading;

public sealed class CaseLoader(
    CaseFolders folders,
    CaseService cases,
    CaseReader reader,
    ILogger<CaseLoader> logger
)
{
    private static readonly HashSet<string> FixtureFacts =
    [
        "case-001",
        "case-002",
        "case-003",
        "case-005",
        "case-006",
        "case-007",
        "case-008",
        "case-009",
    ];

    public void Load()
    {
        var foldersByCaseId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var root in new[] { SourceRoot.Public, SourceRoot.Own })
        {
            foreach (string directory in folders.CaseDirectories(root))
            {
                try
                {
                    LoadCase(root, directory, foldersByCaseId);
                }
                catch (Exception error)
                {
                    string reason = error is InvoiceValidationException validation
                        ? string.Join(
                            "; ",
                            validation.Details.Select(detail => $"{detail.Path}: {detail.Message}")
                        )
                        : error.Message;
                    throw new InvalidDataException(
                        $"Could not load case folder '{directory}': {reason}",
                        error
                    );
                }
            }
        }
    }

    private void LoadCase(
        SourceRoot root,
        string directory,
        Dictionary<string, string> foldersByCaseId
    )
    {
        string folderName = Path.GetFileName(directory);
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "case.json")));
        var input = IntakeValidation.Read<CaseManifest>("case", manifest);
        RequireMatchingCaseId(folderName, input.CaseId, "case.json");
        if (!foldersByCaseId.TryAdd(input.CaseId, directory))
            throw new InvalidDataException(
                $"Case ID '{input.CaseId}' already appears in '{foldersByCaseId[input.CaseId]}'."
            );

        var sources = input
            .InputDocuments.Select(document => new InvoiceSource(
                root,
                $"cases/{folderName}/{document.Path}",
                document.ContentSha256,
                document.DocumentId
            ))
            .ToArray();
        var metadata = new CaseMetadata(
            input.CaseId,
            root == SourceRoot.Public ? CaseOrigin.Assignment : CaseOrigin.Own,
            input.AsOf,
            sources
        );
        string stepPath = CaseFolders.FindFile(directory, input.Steps[0].Input)
            ?? throw new FileNotFoundException("First step file is missing.", input.Steps[0].Input);
        bool includeDocument = FixtureFacts.Contains(metadata.CaseId);
        var step = IntakeValidation.ReadStep(File.ReadAllText(stepPath), includeDocument);
        RequireMatchingCaseId(folderName, step.CaseId, input.Steps[0].Input);
        var companyData = step.Context
            ?? throw new InvalidDataException("First step company data is missing.");
        var reading = includeDocument
            ? ReadFixture(
                metadata,
                step.Document ?? throw new InvalidDataException("First step document is missing.")
            )
            : null;

        if (reader.Exists(folderName))
            return;

        var referencePdfPaths = (input.Fixtures ?? [])
            .Where(fixture =>
                string.Equals(
                    Path.GetExtension(fixture.Path),
                    ".pdf",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .Select(fixture => $"cases/{folderName}/{fixture.Path}")
            .ToArray();
        if (cases.LoadCase(metadata, companyData, reading, referencePdfPaths))
            logger.LogInformation(
                "Loaded case {CaseId} with snapshot {ContextVersion} and fixture facts {HasFacts}",
                metadata.CaseId,
                companyData.ContextVersion,
                reading is not null
            );
    }

    private static void RequireMatchingCaseId(string folderName, string caseId, string fileName)
    {
        if (caseId != folderName)
            throw new InvalidDataException(
                $"{fileName}: case_id '{caseId}' must match folder name '{folderName}'."
            );
    }

    private static InvoiceReading ReadFixture(CaseMetadata metadata, StepDocument document)
    {
        var matches = metadata
            .Sources.Where(source =>
                source.DocumentId == document.Facts.DocumentId
                && source.ContentSha256 == document.ContentSha256
            )
            .ToArray();
        if (matches is not [var source])
            throw new InvalidDataException(
                $"First step document '{document.Facts.DocumentId}' must match exactly one manifest source by document ID and SHA-256."
            );

        return new(document.Facts, document.ContentSha256, source, ReadingOrigin.Fixture);
    }

    private sealed record CaseManifest(
        string CaseId,
        DateOnly AsOf,
        ManifestDocument[] InputDocuments,
        ManifestStep[] Steps,
        ManifestFixture[]? Fixtures
    );

    private sealed record ManifestDocument(string DocumentId, string Path, string ContentSha256);

    private sealed record ManifestStep(string Input);

    private sealed record ManifestFixture(string Path);
}
