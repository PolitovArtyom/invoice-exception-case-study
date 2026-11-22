using System.Security.Cryptography;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Validation;

namespace InvoiceDesk.Web.CaseLoading;

public sealed class CaseFolders(IConfiguration configuration, IWebHostEnvironment environment)
{
    private readonly string _publicRoot = Path.GetFullPath(
        configuration["DatasetPath"] ?? Path.Combine(environment.ContentRootPath, "../../dataset")
    );

    private readonly string _ownRoot = Path.GetFullPath(
        configuration["ScenarioPath"]
            ?? Path.Combine(environment.ContentRootPath, "../../scenarios")
    );

    public IEnumerable<string> CaseDirectories(SourceRoot root)
    {
        string parent = Path.Combine(GetRoot(root), "cases");
        if (root == SourceRoot.Own && !Directory.Exists(parent))
            return [];

        return Directory.EnumerateDirectories(parent).Order(StringComparer.Ordinal);
    }

    public string? FindPdf(InvoiceSource source) =>
        FindFile(GetRoot(source.Root), source.RelativePath);

    public string? FindReferencePdf(
        CaseOrigin origin,
        IReadOnlyList<string> referencePdfPaths,
        string sha256
    )
    {
        var root = origin == CaseOrigin.Assignment ? SourceRoot.Public : SourceRoot.Own;
        foreach (string relativePath in referencePdfPaths)
        {
            if (FindFile(GetRoot(root), relativePath) is not { } path)
                continue;

            if (HashMatches(path, sha256))
                return path;
        }

        return null;
    }

    public static string HostPath(InvoiceSource source) =>
        (source.Root == SourceRoot.Public ? "dataset/" : "scenarios/") + source.RelativePath;

    public InvoiceSource ReadSource(
        CaseMetadata metadata,
        SourceRoot root,
        string relativePath,
        string documentId
    )
    {
        var source = metadata.Sources.SingleOrDefault(item =>
            item.Root == root && item.RelativePath == relativePath
        );
        if (source is null || FindPdf(source) is not string path)
            throw new InvoiceValidationException(
                "/source",
                "Select an available PDF from open_case."
            );

        if (source.DocumentId != documentId)
            throw new InvoiceValidationException(
                "/facts/document_id",
                "Must match the selected PDF's document ID."
            );

        using var pdf = File.OpenRead(path);
        return source with { ContentSha256 = Convert.ToHexStringLower(SHA256.HashData(pdf)) };
    }

    public static string? FindFile(string directory, string relativePath)
    {
        string path = Path.Combine(directory, relativePath);
        return File.Exists(path) ? path : null;
    }

    private static bool HashMatches(string path, string expected)
    {
        using var pdf = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(pdf)) == expected;
    }

    private string GetRoot(SourceRoot root) =>
        root switch
        {
            SourceRoot.Public => _publicRoot,
            SourceRoot.Own => _ownRoot,
            _ => throw new ArgumentOutOfRangeException(nameof(root)),
        };
}
