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

    public string CopyCase(string caseId, bool own = false) =>
        CopyCaseFolder(
            PublicCases.Folder(caseId),
            Path.Combine(own ? ScenarioPath : DatasetPath, "cases", caseId)
        );

    public string CopyOwnCaseFixture(string caseId)
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", caseId);
        CopyCaseFolder(fixture, Path.Combine(ScenarioPath, "cases", caseId));
        return fixture;
    }

    public void CopyScenarioCase(string caseId) =>
        CopyCaseFolder(
            Path.Combine(RepositoryScenarios, "cases", caseId),
            Path.Combine(ScenarioPath, "cases", caseId)
        );

    private static string RepositoryScenarios =>
        Path.Combine(Path.GetDirectoryName(PublicCases.Root)!, "scenarios");

    public void CopyLateReceiptCase()
    {
        const string caseId = "case-101";
        CopyScenarioCase(caseId);
        WriteCompanyDataUpdate(
            caseId,
            File.ReadAllText(Path.Combine(RepositoryScenarios, "updates", caseId, "context.json"))
        );
    }

    public void WriteCompanyDataUpdate(string caseId, string json)
    {
        string folder = Path.Combine(ScenarioPath, "updates", caseId);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "context.json"), json);
    }

    public string AddReviewerCase()
    {
        string folder = CopyCaseFolder(
            PublicCases.Folder(PublicCases.CleanCase),
            Path.Combine(ScenarioPath, "cases", ReviewerCaseId)
        );
        string manifestPath = Path.Combine(folder, "case.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        manifest["case_id"] = ReviewerCaseId;
        File.WriteAllText(manifestPath, manifest.ToJsonString());

        var firstStep = PublicCases.ReadFirstStep();
        firstStep["case_id"] = ReviewerCaseId;
        firstStep["document"]!["facts"]!["number"] = "REVIEWER-FACTS-MUST-NOT-LOAD";
        File.WriteAllText(Path.Combine(folder, "steps", "step-001.json"), firstStep.ToJsonString());
        Directory.CreateDirectory(Path.Combine(folder, "expected"));
        File.WriteAllText(
            Path.Combine(folder, "expected", "step-001.json"),
            "EXPECTED DATA MUST NOT BE READ"
        );
        return folder;
    }

    public WebApplicationFactory<Program> Start(string? databasePath = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(
                (_, config) =>
                    config.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["DatasetPath"] = DatasetPath,
                            ["ScenarioPath"] = ScenarioPath,
                            ["DatabasePath"] = databasePath ?? DatabasePath,
                            ["Logging:LogLevel:Default"] = "Warning",
                        }
                    )
            )
        );

    private static string CopyCaseFolder(string original, string destination)
    {
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
