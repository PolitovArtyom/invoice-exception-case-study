using System.Text.Json.Nodes;
using InvoiceDesk.Core.Validation;

namespace InvoiceDesk.Tests.Support;

internal static class AssessmentAssertions
{
    private static readonly string[] DocumentCodes =
    [
        "DOCUMENT_CONFLICT",
        "SUPPLIER_IDENTITY",
        "BUYER_IDENTITY",
        "ORDER_MATCH",
        "CURRENCY_MATCH",
        "TOTALS_ARITHMETIC",
        "BANK_ACCOUNT",
    ];
    private static readonly string[] LineCodes =
    [
        "LINE_MATCH",
        "QTY_RECEIVED",
        "QTY_ORDER_REMAINING",
        "UNIT_PRICE",
        "LINE_ARITHMETIC",
    ];

    public static void AssertOutcome(
        JsonNode result,
        string decision,
        string documentId,
        string[] lineIds,
        ExpectedCheck[] exceptions,
        (string Fact, string ObjectId)[]? questions = null,
        string? matchedOrderId = "O",
        string? orderMatchSource = "deterministic",
        string disposition = "new",
        string? originalDocumentId = null,
        string?[]? lineMatchSources = null
    )
    {
        AssignmentSchemas.Instance.Validate("step-output", result);
        Assert.Equal(disposition, Text(result, "import_disposition"));
        Assert.Equal(originalDocumentId, Text(result, "original_document_id"));
        Assert.Null(result["existing_assessment_ref"]);
        Assert.Equal(documentId, Text(result, "document_id"));
        Assert.Equal(decision, Text(result, "decision"));
        Assert.Equal(matchedOrderId, Text(result, "matched_order_id"));
        Assert.Equal(orderMatchSource, Text(result, "order_match_source"));

        Assert.Equal(
            lineIds.Order(StringComparer.Ordinal),
            result["lines"]!
                .AsArray()
                .Select(line => Text(line!, "document_line_id"))
                .Order(StringComparer.Ordinal)
        );

        var lines = result["lines"]!
            .AsArray()
            .ToDictionary(line => Text(line!, "document_line_id")!);
        var sources = lineMatchSources
            ?? Enumerable.Repeat<string?>("deterministic", lineIds.Length).ToArray();
        Assert.Equal(sources, lineIds.Select(id => Text(lines[id]!, "match_source")));

        var checks = result["checks"]!.AsArray();
        Assert.Equal(7 + 5 * lineIds.Length, checks.Count);
        var expectedKeys = DocumentCodes
            .Select(code => (Code: code, ObjectId: documentId))
            .Concat(lineIds.SelectMany(id => LineCodes.Select(code => (Code: code, ObjectId: id))))
            .ToHashSet();
        var overrides = exceptions.ToDictionary(check => (check.Code, check.ObjectId));
        Assert.All(overrides.Keys, key => Assert.Contains(key, expectedKeys));
        foreach (var check in checks)
        {
            var key = (Text(check!, "code")!, Text(check!, "object_id")!);
            Assert.True(expectedKeys.Remove(key), $"Unexpected or repeated check {key}.");
            var expected = overrides.GetValueOrDefault(key);
            Assert.Equal(expected?.Status ?? "pass", Text(check!, "status"));
            Assert.Equal(expected?.Severity, Text(check!, "severity"));
            Assert.Equal(expected?.Dependency, Text(check!, "depends_on"));
        }

        Assert.Empty(expectedKeys);
        var actualQuestions = result["questions"]!
            .AsArray()
            .Select(question =>
                (
                    Fact: Text(question!, "required_fact")!,
                    ObjectId: Assert
                        .Single(question!["target_object_ids"]!.AsArray())!
                        .GetValue<string>()
                )
            );
        Assert.Equal(
            (questions ?? [])
                .OrderBy(question => question.Fact, StringComparer.Ordinal)
                .ThenBy(question => question.ObjectId, StringComparer.Ordinal),
            actualQuestions
                .OrderBy(question => question.Fact, StringComparer.Ordinal)
                .ThenBy(question => question.ObjectId, StringComparer.Ordinal)
        );
    }

    public static void AssertTotals(JsonNode result, string subtotal, string tax, string total)
    {
        var totals = result["totals"]!;
        Assert.Equal(subtotal, Text(totals, "calculated_subtotal"));
        Assert.Equal(tax, Text(totals, "calculated_tax_total"));
        Assert.Equal(total, Text(totals, "calculated_total"));
    }

    public static void AssertLine(
        JsonNode result,
        string lineId,
        string? orderLineId,
        string? matchSource,
        string net,
        string tax,
        string? ordered = "10",
        string? received = "10",
        string? previous = "0",
        string? available = "10",
        string? remaining = "10"
    )
    {
        var line = Assert.Single(result["lines"]!.AsArray(), item =>
            Text(item!, "document_line_id") == lineId
        )!;
        Assert.Equal(orderLineId, Text(line, "order_line_id"));
        Assert.Equal(matchSource, Text(line, "match_source"));
        Assert.Equal(net, Text(line, "calculated_net"));
        Assert.Equal(tax, Text(line, "calculated_tax"));
        Assert.Equal(ordered, Text(line, "ordered"));
        Assert.Equal(received, Text(line, "received"));
        Assert.Equal(previous, Text(line, "previously_invoiced"));
        Assert.Equal(available, Text(line, "available_to_invoice"));
        Assert.Equal(remaining, Text(line, "remaining_on_order"));
    }

    public static void AssertDuplicate(
        JsonNode response,
        string documentId,
        string originalId,
        string? assessment,
        int revision
    )
    {
        Assert.Null(response["assessment_id"]);
        Assert.False(response["inputs_unchanged"]!.GetValue<bool>());
        var result = response["result"]!;
        AssignmentSchemas.Instance.Validate("step-output", result);
        Assert.Equal("duplicate", Text(result, "import_disposition"));
        Assert.Equal(documentId, Text(result, "document_id"));
        Assert.Equal(originalId, Text(result, "original_document_id"));
        Assert.Equal(assessment, Text(result, "existing_assessment_ref"));
        Assert.Equal(revision, result["input_revision"]!.GetValue<int>());
        foreach (
            string field in new[] { "decision", "totals", "matched_order_id", "order_match_source" }
        )
        {
            Assert.True(result.AsObject().ContainsKey(field));
            Assert.Null(result[field]);
        }
        foreach (string field in new[] { "lines", "checks", "questions" })
            Assert.Empty(result[field]!.AsArray());
    }

    public static string? Text(JsonNode node, string field) => node[field]?.GetValue<string>();

    public sealed record ExpectedCheck(
        string Code,
        string ObjectId,
        string Status,
        string? Severity = null,
        string? Dependency = null
    );
}
