using System.Text.Json.Nodes;

using InvoiceDesk.Core.Validation;

namespace InvoiceDesk.Tests.Support;

internal static class CleanAssessment
{
    public static void AssertResult(JsonNode result, int revision)
    {
        AssignmentSchemas.Instance.Validate("step-output", result);
        Assert.Equal("case-001", Text(result, "case_id"));
        Assert.Equal("core-v1", Text(result, "policy_version"));
        Assert.Equal("CTX-001-v1", Text(result, "context_version"));
        Assert.Equal(revision, result["input_revision"]!.GetValue<int>());
        Assert.Equal("DOC-001", Text(result, "document_id"));
        Assert.Equal("new", Text(result, "import_disposition"));
        Assert.Null(result["original_document_id"]);
        Assert.Null(result["existing_assessment_ref"]);
        Assert.Equal("clear", Text(result, "decision"));
        Assert.Equal("ORD-001", Text(result, "matched_order_id"));
        Assert.Equal("deterministic", Text(result, "order_match_source"));
        Assert.Empty(result["questions"]!.AsArray());

        var totals = result["totals"]!;
        Assert.Equal("1845.20", Text(totals, "calculated_subtotal"));
        Assert.Equal("369.04", Text(totals, "calculated_tax_total"));
        Assert.Equal("2214.24", Text(totals, "calculated_total"));

        Assert.Collection(result["lines"]!.AsArray(),
            line => AssertLine(line!, "L01", "OL-001-01", "120", "1050.00", "210.00"),
            line => AssertLine(line!, "L02", "OL-001-02", "80", "512.00", "102.40"),
            line => AssertLine(line!, "L03", "OL-001-03", "6", "195.00", "39.00"),
            line => AssertLine(line!, "L04", "OL-001-04", "12", "88.20", "17.64"));

        AssertChecks(result["checks"]!.AsArray());
    }

    private static void AssertChecks(JsonArray checks)
    {
        string[] documentCodes =
        [
            "DOCUMENT_CONFLICT", "SUPPLIER_IDENTITY", "BUYER_IDENTITY", "ORDER_MATCH",
            "CURRENCY_MATCH", "TOTALS_ARITHMETIC", "BANK_ACCOUNT"
        ];
        string[] lineCodes = ["LINE_MATCH", "QTY_RECEIVED", "QTY_ORDER_REMAINING", "UNIT_PRICE", "LINE_ARITHMETIC"];
        string[] documentLineIds = ["L01", "L02", "L03", "L04"];
        string[] expected =
        [
            .. documentCodes.Select(code => $"{code}/DOC-001"),
            .. documentLineIds.SelectMany(id => lineCodes.Select(code => $"{code}/{id}"))
        ];
        Assert.Equal(27, checks.Count);
        Assert.Equal(expected.Order(StringComparer.Ordinal),
            checks.Select(check => $"{Text(check!, "code")}/{Text(check!, "object_id")}").Order(StringComparer.Ordinal));
        Assert.All(checks, check =>
        {
            Assert.Equal("pass", Text(check!, "status"));
            Assert.Null(check!["severity"]);
            Assert.Null(check["depends_on"]);
            Assert.NotEmpty(check["actual"]!.AsObject());
            Assert.NotEmpty(check["expected"]!.AsObject());
            Assert.NotEmpty(check["evidence_refs"]!.AsArray());
        });

        var price = checks.Single(check => Text(check!, "code") == "UNIT_PRICE" && Text(check!, "object_id") == "L01")!;
        Assert.Equal("8.75", Text(price["actual"]!["unit_net_price"]!, "value"));
        Assert.Equal("invoice", Text(price["actual"]!["unit_net_price"]!, "source"));
        Assert.Equal("0.01", Text(price["expected"]!["inclusive_tolerance"]!, "value"));
        Assert.Equal("order", Text(price["expected"]!["unit_net_price"]!, "source"));
        Assert.Equal("/evidence/17", Assert.Single(price["evidence_refs"]!.AsArray())!.GetValue<string>());
    }

    private static void AssertLine(JsonNode line, string documentLine, string orderLine, string quantity, string net, string tax)
    {
        Assert.Equal(documentLine, Text(line, "document_line_id"));
        Assert.Equal(orderLine, Text(line, "order_line_id"));
        Assert.Equal("deterministic", Text(line, "match_source"));
        Assert.Equal(net, Text(line, "calculated_net"));
        Assert.Equal(tax, Text(line, "calculated_tax"));
        Assert.Equal("0", Text(line, "previously_invoiced"));
        foreach (string field in new[] { "ordered", "received", "available_to_invoice", "remaining_on_order" })
            Assert.Equal(quantity, Text(line, field));
    }

    private static string? Text(JsonNode node, string field) => node[field]?.GetValue<string>();
}
