namespace InvoiceDesk.Core.Rules.Normalization;

internal static class InvoiceText
{
    public static string? Identifier(string? value) => value?.Trim().ToUpperInvariant();

    public static bool SameName(string? left, string? right) => Name(left) == Name(right);

    private static string? Name(string? value) =>
        value is null
            ? null
            : UnicodeCaseFolding.Fold(
                string.Join(
                    ' ',
                    value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                )
            );

    public static string? Iban(string? value) =>
        value is null
            ? null
            : string.Concat(value.Where(character => !char.IsWhiteSpace(character)))
                .ToUpperInvariant();
}
