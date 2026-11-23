using System.Text.Json;
using System.Text.Json.Nodes;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.HumanResolution;
using InvoiceDesk.Core.Validation;

namespace InvoiceDesk.Core.Cases.HumanResolution;

internal static class FactCorrections
{
    private static readonly CorrectableFact[] DocumentFields =
    [
        new("/number", false),
        new("/issue_date", false),
        new("/due_date", true),
        new("/currency", true),
        new("/purchase_order_reference", true),
        new("/payment_terms", true),
        new("/supplier/legal_name", true),
        new("/supplier/registration_id", true),
        new("/supplier/country", true),
        new("/buyer/legal_name", true),
        new("/buyer/registration_id", true),
        new("/buyer/country", true),
        new("/bank/iban", true),
        new("/bank/bic", true),
        new("/declared_subtotal", false),
        new("/declared_tax_total", false),
        new("/declared_total", false),
    ];

    private static readonly CorrectableFact[] LineFields =
    [
        new("/description", false),
        new("/sku", true),
        new("/quantity", false),
        new("/unit", false),
        new("/unit_net_price", false),
        new("/declared_net_amount", false),
        new("/tax_rate", false),
        new("/declared_tax_amount", true),
    ];

    public static CorrectableFact[] Fields(InvoiceFacts facts) =>
        [
            .. DocumentFields,
            .. facts.Lines.SelectMany(
                (_, index) =>
                    LineFields.Select(field => field with { Path = $"/lines/{index}{field.Path}" })
            ),
        ];

    public static (InvoiceFacts Facts, FactCorrection Correction) Correct(
        InvoiceFacts facts,
        FactCorrectionForm form,
        string person,
        string reason
    )
    {
        var field = Fields(facts).FirstOrDefault(field => field.Path == form.FieldPath)
            ?? throw new InvoiceValidationException(
                "/field_path",
                "Select a parsed invoice field."
            );
        string path = "/facts" + field.Path;
        string? value = form.Value.ValueKind switch
        {
            JsonValueKind.String => form.Value.GetString(),
            JsonValueKind.Null when field.CanBeMissing => null,
            JsonValueKind.Undefined => throw new InvoiceValidationException(
                "/value",
                "Supply a corrected value."
            ),
            _ => throw new InvoiceValidationException(
                path,
                "Enter a valid value for this invoice field."
            ),
        };
        if (value is null && form.MissingReason is null)
            throw new InvoiceValidationException(
                "/missing_reason",
                "Choose why the value is missing."
            );

        if (value is not null && form.MissingReason is not null)
            throw new InvoiceValidationException(
                "/missing_reason",
                "Remove the missing reason or clear the value."
            );

        var json = (JsonObject)JsonSerializer.SerializeToNode(facts, InvoiceJson.Options)!;
        JsonFields.TryResolve(json, field.Path, out var previous);
        var correction = new FactCorrection(
            field.Path,
            previous?.GetValue<string>(),
            value,
            form.MissingReason,
            person,
            reason
        );
        Apply(json, correction);
        var corrected = IntakeValidation.ReadFacts(json, "/facts");
        if (InvoiceJson.SameValues(facts, corrected))
            throw new InvoiceValidationException("/value", "The value is unchanged.");
        return (corrected, correction);
    }

    private static void Apply(JsonObject facts, FactCorrection correction)
    {
        string path = correction.FieldPath;
        int fieldStart = path.LastIndexOf('/');
        JsonFields.TryResolve(facts, path[..fieldStart], out var parent);
        parent![path[(fieldStart + 1)..]] = correction.Value;

        var missing = facts["missing_fields"]!.AsArray();
        var entry = missing.FirstOrDefault(item => item!["field_path"]!.GetValue<string>() == path);
        if (correction.MissingReason is { } reason)
        {
            var serializedReason = JsonSerializer.SerializeToNode(reason, InvoiceJson.Options);
            if (entry is null)
                missing.Add(
                    new JsonObject { ["field_path"] = path, ["reason"] = serializedReason }
                );
            else
                entry["reason"] = serializedReason;
        }
        else if (entry is not null)
            missing.Remove(entry);
    }
}
