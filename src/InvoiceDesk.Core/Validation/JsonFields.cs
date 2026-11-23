using System.Globalization;
using System.Text.Json.Nodes;

namespace InvoiceDesk.Core.Validation;

internal static class JsonFields
{
    public static bool TryResolve(JsonNode? node, string path, out JsonNode? found)
    {
        foreach (string part in path.Split('/').Skip(1))
        {
            string key = part.Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
            switch (node)
            {
                case JsonObject obj when obj.TryGetPropertyValue(key, out var value):
                    node = value;
                    break;
                case JsonArray array
                    when int.TryParse(
                        key,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out int index
                    )
                        && key == index.ToString(CultureInfo.InvariantCulture)
                        && index < array.Count:
                    node = array[index];
                    break;
                default:
                    found = null;
                    return false;
            }
        }

        found = node;
        return true;
    }

    public static IEnumerable<(string Path, JsonNode? Value)> Leaves(JsonNode? node, string path) =>
        node switch
        {
            JsonObject obj => obj.SelectMany(field =>
                Leaves(field.Value, path + "/" + Escape(field.Key))
            ),
            JsonArray array => array.SelectMany((value, index) => Leaves(value, $"{path}/{index}")),
            _ => [(path, node)],
        };

    public static string Escape(string key) =>
        key.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);
}
