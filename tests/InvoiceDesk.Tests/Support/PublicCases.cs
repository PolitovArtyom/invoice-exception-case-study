using System.Text.Json.Nodes;

namespace InvoiceDesk.Tests.Support;

internal static class PublicCases
{
    public const string CleanCase = "case-001";

    private static string Root { get; } = FindDataRoot();

    public static string Folder(string caseId) => Path.Combine(Root, "cases", caseId);

    public static IEnumerable<string> Steps(string caseId)
    {
        string folder = Folder(caseId);
        var definition = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "case.json")))!;
        return definition["steps"]!.AsArray().Select(step => Path.Combine(folder, step!["input"]!.GetValue<string>()));
    }

    public static JsonObject ReadFirstStep(string caseId = CleanCase) =>
        JsonNode.Parse(File.ReadAllText(Steps(caseId).First()))!.AsObject();

    public static JsonObject CleanFacts() => ReadFirstStep()["document"]!["facts"]!.AsObject();

    private static string FindDataRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string dataset = Path.Combine(directory.FullName, "dataset");
            if (Directory.Exists(dataset))
                return dataset;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Test dataset could not be found.");
    }
}
