using System.Globalization;
using System.Text;

namespace InvoiceDesk.Core.Rules.Normalization;

internal static class UnicodeCaseFolding
{
    private static readonly Dictionary<int, string> Mappings = LoadMappings();

    public static string Fold(string text)
    {
        var folded = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
            folded.Append(
                Mappings.TryGetValue(rune.Value, out string? mapping) ? mapping : rune.ToString()
            );
        return folded.ToString();
    }

    private static Dictionary<int, string> LoadMappings()
    {
        using var stream = typeof(UnicodeCaseFolding).Assembly.GetManifestResourceStream(
            "CaseFolding.txt"
        )!;
        using var reader = new StreamReader(stream);
        var mappings = new Dictionary<int, string>();
        while (reader.ReadLine() is { } line)
        {
            var fields = line.Split(';', StringSplitOptions.TrimEntries);
            if (fields.Length < 3 || fields[1] is not ("C" or "F"))
                continue;
            mappings.Add(
                CodePoint(fields[0]),
                string.Concat(
                    fields[2].Split(' ').Select(hex => char.ConvertFromUtf32(CodePoint(hex)))
                )
            );
        }

        return mappings;
    }

    private static int CodePoint(string hex) =>
        int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
