using System.Text.Json.Nodes;

using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.UseCases;
using InvoiceDesk.Core.Validation;

namespace InvoiceDesk.Web.CaseLoading;

public sealed class CaseLoader(CaseFolders folders, CaseQueries queries, CaseUpdates updates, ILogger<CaseLoader> logger)
{
    private static readonly HashSet<string> FixtureFacts = ["case-001"];

    public void Load()
    {
        foreach (string root in new[] { "public", "own" })
        {
            foreach (string directory in folders.CaseDirectories(root))
                LoadCase(root, directory, loadFixtureFacts: true);
        }
    }

    public void LoadCase(string root, string directory, bool loadFixtureFacts)
    {
        string caseId = Path.GetFileName(directory);
        if (queries.Exists(caseId) || !File.Exists(Path.Combine(directory, "case.json")))
            return;

        string manifestPath = CaseFolders.FindFile(directory, "case.json") ??
            throw new InvalidDataException($"Invalid case metadata path for {caseId}.");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath));
        AssignmentSchemas.Instance.Validate("case", manifest);
        var input = InvoiceJson.Read<CaseManifest>(manifest!.ToJsonString());
        if (input.CaseId != caseId)
            throw new InvalidDataException($"Case ID in {caseId} must match its directory.");

        IntakeValidation.ValidateDate(input.AsOf, "/as_of");
        var sources = input.InputDocuments.Select(document =>
            new InvoiceSource(root, $"cases/{caseId}/{document.Path}", document.ContentSha256, document.DocumentId)).ToArray();
        var metadata = new CaseMetadata(caseId, root == "public" ? "assignment" : "own", input.AsOf, sources);
        string firstInput = input.Steps[0].Input;
        if (!firstInput.StartsWith("steps/", StringComparison.Ordinal) ||
            CaseFolders.FindFile(directory, firstInput) is not string stepPath)
            throw new InvalidDataException($"Invalid first step input path for {caseId}.");

        var step = JsonNode.Parse(File.ReadAllText(stepPath));
        if (step?["case_id"]?.GetValue<string>() != caseId ||
            step["as_of"]?.GetValue<string>() != metadata.AsOf ||
            step["policy_ref"]?.GetValue<string>() != CaseMetadata.PolicyVersion)
            throw new InvalidDataException($"First step metadata does not match {caseId}.");

        var company = IntakeValidation.ReadCompanyData(step["context"], "/context");
        var reading = loadFixtureFacts && FixtureFacts.Contains(caseId)
            ? ReadFixture(metadata, step["document"] as JsonObject ??
                throw new InvalidDataException($"First step document is missing for {caseId}."))
            : null;
        if (updates.LoadCase(metadata, company, reading))
            logger.LogInformation("Loaded case {CaseId} with snapshot {ContextVersion} and fixture facts {HasFacts}",
                caseId, company.ContextVersion, reading is not null);
    }

    private static InvoiceReading ReadFixture(CaseMetadata metadata, JsonObject document)
    {
        var facts = IntakeValidation.ReadFacts(document["facts"], "/document/facts");
        string? hash = document["content_sha256"]?.GetValue<string>();
        var source = metadata.Sources.SingleOrDefault(item => item.DocumentId == facts.DocumentId && item.ContentSha256 == hash) ??
            throw new InvalidDataException($"First step document does not match source metadata for {metadata.CaseId}.");
        return new(facts, source.ContentSha256, source, InvoiceReading.FixtureOrigin);
    }

    private sealed record CaseManifest(string CaseId, string AsOf, ManifestDocument[] InputDocuments, ManifestStep[] Steps);

    private sealed record ManifestDocument(string DocumentId, string Path, string ContentSha256);

    private sealed record ManifestStep(string Input);
}
