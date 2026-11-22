using System.Text.Json.Nodes;

using InvoiceDesk.Web.CaseLoading;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceDesk.Tests.Support;

internal sealed class CaseTestEnvironment : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public string DatasetPath => Path.Combine(_directory.Path, "dataset");

    public string ScenarioPath => Path.Combine(_directory.Path, "scenarios");

    public string DatabasePath => Path.Combine(_directory.Path, "invoice.sqlite");

    public string CopyCase(string caseId)
    {
        string original = PublicCases.Folder(caseId);
        string copied = Path.Combine(DatasetPath, "cases", caseId);
        foreach (string subdirectory in new[] { "steps", "documents" })
        {
            Directory.CreateDirectory(Path.Combine(copied, subdirectory));
            foreach (string path in Directory.EnumerateFiles(Path.Combine(original, subdirectory)))
                File.Copy(path, Path.Combine(copied, subdirectory, Path.GetFileName(path)));
        }

        File.Copy(Path.Combine(original, "case.json"), Path.Combine(copied, "case.json"));
        return copied;
    }

    public void AddReviewerCase()
    {
        const string reviewerCaseId = "case-911";
        string folder = Path.Combine(ScenarioPath, "cases", reviewerCaseId);
        Directory.CreateDirectory(Path.Combine(folder, "steps"));
        Directory.CreateDirectory(Path.Combine(folder, "expected"));
        string original = Path.Combine(PublicCases.Folder(PublicCases.CleanCase), "case.json");
        var manifest = JsonNode.Parse(File.ReadAllText(original))!;
        manifest["case_id"] = reviewerCaseId;
        File.WriteAllText(Path.Combine(folder, "case.json"), manifest.ToJsonString());
        var firstStep = PublicCases.ReadFirstStep();
        firstStep["case_id"] = reviewerCaseId;
        firstStep["document"]!["facts"]!["number"] = "REVIEWER-FACTS-MUST-NOT-LOAD";
        File.WriteAllText(Path.Combine(folder, "steps", "step-001.json"), firstStep.ToJsonString());
        File.WriteAllText(Path.Combine(folder, "expected", "step-001.json"), "EXPECTED DATA MUST NOT BE READ");
    }

    public WebApplicationFactory<Program> Start(bool loadCasesOnStartup = false) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatasetPath"] = DatasetPath,
                ["ScenarioPath"] = ScenarioPath,
                ["DatabasePath"] = DatabasePath,
                ["LoadCasesOnStartup"] = loadCasesOnStartup.ToString(),
                ["Logging:LogLevel:Default"] = "Warning"
            })));

    public void PrepareCleanCase(WebApplicationFactory<Program> application)
    {
        using var scope = application.Services.CreateScope();
        Assert.True(File.Exists(DatabasePath));
        var loader = scope.ServiceProvider.GetRequiredService<CaseLoader>();
        loader.LoadCase("public", Path.Combine(DatasetPath, "cases", PublicCases.CleanCase), loadFixtureFacts: false);
    }

    public void Dispose() => _directory.Dispose();
}
