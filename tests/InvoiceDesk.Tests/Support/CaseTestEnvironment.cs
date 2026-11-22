using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace InvoiceDesk.Tests.Support;

internal sealed class CaseTestEnvironment : IDisposable
{
    public const string ReviewerCaseId = "case-911";

    private readonly TemporaryDirectory _directory = new();

    public CaseTestEnvironment() => Directory.CreateDirectory(Path.Combine(DatasetPath, "cases"));

    public string DatasetPath => Path.Combine(_directory.Path, "dataset");

    public string ScenarioPath => Path.Combine(_directory.Path, "scenarios");

    public string DatabasePath => Path.Combine(_directory.Path, "invoice.sqlite");

    public string CopyCase(string caseId) =>
        CopyCaseFolder(caseId, Path.Combine(DatasetPath, "cases", caseId));

    public string AddReviewerCase()
    {
        string folder = CopyCaseFolder(PublicCases.CleanCase, Path.Combine(ScenarioPath, "cases", ReviewerCaseId));
        string manifestPath = Path.Combine(folder, "case.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        manifest["case_id"] = ReviewerCaseId;
        File.WriteAllText(manifestPath, manifest.ToJsonString());

        var firstStep = PublicCases.ReadFirstStep();
        firstStep["case_id"] = ReviewerCaseId;
        firstStep["document"]!["facts"]!["number"] = "REVIEWER-FACTS-MUST-NOT-LOAD";
        File.WriteAllText(Path.Combine(folder, "steps", "step-001.json"), firstStep.ToJsonString());
        Directory.CreateDirectory(Path.Combine(folder, "expected"));
        File.WriteAllText(Path.Combine(folder, "expected", "step-001.json"), "EXPECTED DATA MUST NOT BE READ");
        return folder;
    }

    public WebApplicationFactory<Program> Start() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatasetPath"] = DatasetPath,
                ["ScenarioPath"] = ScenarioPath,
                ["DatabasePath"] = DatabasePath,
                ["Logging:LogLevel:Default"] = "Warning"
            })));

    private static string CopyCaseFolder(string caseId, string destination)
    {
        string original = PublicCases.Folder(caseId);
        foreach (string subdirectory in new[] { "steps", "documents" })
        {
            Directory.CreateDirectory(Path.Combine(destination, subdirectory));
            foreach (string path in Directory.EnumerateFiles(Path.Combine(original, subdirectory)))
                File.Copy(path, Path.Combine(destination, subdirectory, Path.GetFileName(path)));
        }

        File.Copy(Path.Combine(original, "case.json"), Path.Combine(destination, "case.json"));
        return destination;
    }

    public void Dispose() => _directory.Dispose();
}
