using System.Text.Json;
using System.Text.Json.Nodes;
using InvoiceDesk.Core.Cases;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Validation;

namespace InvoiceDesk.Web.CaseLoading;

public sealed class CaseLoader(CaseFolders folders, CaseService cases, ILogger<CaseLoader> logger)
{
    private static readonly HashSet<string> FixtureFacts = ["case-001"];

    public void Load()
    {
        foreach (string root in new[] { "public", "own" })
        {
            foreach (string directory in folders.CaseDirectories(root))
            {
                try
                {
                    LoadCase(root, directory);
                }
                catch (Exception error)
                    when (error
                            is IOException
                                or UnauthorizedAccessException
                                or InvalidDataException
                                or JsonException
                                or InvoiceValidationException
                                or UnsupportedInvoiceCaseException
                                or ArgumentException
                    )
                {
                    logger.LogError(error, "Skipped case folder {CaseFolder}", directory);
                }
            }
        }
    }

    private void LoadCase(string root, string directory)
    {
        string folderName = Path.GetFileName(directory);
        if (cases.Exists(folderName))
            return;

        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "case.json")));
        var input = IntakeValidation.Read<CaseManifest>("case", manifest);
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
            root == "public" ? "assignment" : "own",
            input.AsOf,
            sources
        );
        string stepPath = CaseFolders.FindFile(directory, input.Steps[0].Input)
            ?? throw new FileNotFoundException("First step file is missing.", input.Steps[0].Input);
        var step =
            JsonNode.Parse(File.ReadAllText(stepPath)) as JsonObject
            ?? throw new InvalidDataException("First step must be a JSON object.");

        var companyData = IntakeValidation.ReadCompanyData(step["context"], "/context");
        var reading = FixtureFacts.Contains(metadata.CaseId)
            ? ReadFixture(
                metadata,
                step["document"] as JsonObject
                    ?? throw new InvalidDataException("First step document is missing.")
            )
            : null;
        if (cases.LoadCase(metadata, companyData, reading))
            logger.LogInformation(
                "Loaded case {CaseId} with snapshot {ContextVersion} and fixture facts {HasFacts}",
                metadata.CaseId,
                companyData.ContextVersion,
                reading is not null
            );
    }

    private static InvoiceReading ReadFixture(CaseMetadata metadata, JsonObject document)
    {
        var facts = IntakeValidation.ReadFacts(document["facts"], "/document/facts");
        if (
            document["content_sha256"] is not JsonValue value
            || !value.TryGetValue<string>(out string? hash)
        )
            throw new InvalidDataException("First step document must include its hash.");

        var source = metadata.Sources.FirstOrDefault(item => item.DocumentId == facts.DocumentId);
        return new(facts, hash, source, InvoiceReading.FixtureReadingOrigin);
    }

    private sealed record CaseManifest(
        string CaseId,
        DateOnly AsOf,
        ManifestDocument[] InputDocuments,
        ManifestStep[] Steps
    );

    private sealed record ManifestDocument(string DocumentId, string Path, string ContentSha256);

    private sealed record ManifestStep(string Input);
}
