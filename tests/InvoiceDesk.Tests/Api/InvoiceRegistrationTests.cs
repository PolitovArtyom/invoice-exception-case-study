using System.Net;
using System.Text.Json.Nodes;
using InvoiceDesk.Tests.Support;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;

namespace InvoiceDesk.Tests.Api;

public sealed class InvoiceRegistrationTests : IDisposable
{
    private const string CaseUrl = "/api/cases/" + CaseTestEnvironment.ReviewerCaseId;
    private readonly CaseTestEnvironment _environment = new();
    private readonly string _caseFolder;

    public InvoiceRegistrationTests() => _caseFolder = _environment.AddReviewerCase();

    [Fact]
    public async Task Should_return_source_and_extraction_contract_when_open_case_is_called_over_mcp()
    {
        await using var app = _environment.Start();
        await using var mcp = await ConnectMcp(app);
        var tools = await mcp.ListToolsAsync();
        Assert.Equal(
            new[]
            {
                "assess_invoice",
                "get_assessment",
                "get_case_history",
                "get_company_data",
                "open_case",
                "register_invoice",
            },
            tools.Select(tool => tool.Name).Order(StringComparer.Ordinal)
        );

        var opened = Payload(await Call(mcp, "open_case"));
        Assert.Equal(CaseTestEnvironment.ReviewerCaseId, opened["case_id"]!.GetValue<string>());
        Assert.Equal("core-v1", opened["policy_version"]!.GetValue<string>());
        Assert.Equal(1, opened["current_revision"]!.GetValue<int>());
        Assert.Null(opened["current_document_id"]);
        var source = Assert.Single(opened["sources"]!.AsArray())!;
        Assert.Equal("DOC-001", source["document_id"]!.GetValue<string>());
        Assert.Equal("own", source["root"]!.GetValue<string>());
        Assert.Equal(
            "scenarios/cases/case-911/documents/document-001.pdf",
            source["host_path"]!.GetValue<string>()
        );
        Assert.True(source["available"]!.GetValue<bool>());
        Assert.True(File.Exists(source["container_path"]!.GetValue<string>()));

        var schema = opened["extraction_schema"]!;
        Assert.Equal(
            "#/$defs/money",
            schema["properties"]!["declared_total"]!["$ref"]!.GetValue<string>()
        );
        Assert.NotNull(schema["$defs"]!["money"]!["pattern"]);
        Assert.Contains("verbatim excerpt", opened["extraction_instructions"]!.GetValue<string>());
        Assert.Contains("missing_fields", opened["extraction_instructions"]!.GetValue<string>());
        var company = Payload(await Call(mcp, "get_company_data"));
        Assert.Equal("CTX-001-v1", company["context_version"]!.GetValue<string>());
        Assert.Equal(
            "ORD-001",
            company["purchase_orders"]!["orders"]![0]!["order_id"]!.GetValue<string>()
        );
    }

    [Fact]
    public async Task Should_save_a_clear_assessment_when_clean_invoice_facts_are_registered_over_mcp()
    {
        string manifestPath = Path.Combine(_caseFolder, "case.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        manifest["input_documents"]![0]!["content_sha256"] = new string('0', 64);
        File.WriteAllText(manifestPath, manifest.ToJsonString());

        await using var app = _environment.Start();
        await using var mcp = await ConnectMcp(app);
        using var page = app.CreateClient();

        var company = Payload(await Call(mcp, "get_company_data"));
        var facts = PublicCases.CleanFacts();
        var registered = Payload(await Register(mcp, facts));
        CleanAssessment.AssertResult(registered["result"]!, 2, CaseTestEnvironment.ReviewerCaseId);
        Assert.Equal("2", registered["assessment_id"]!.GetValue<string>());
        Assert.False(registered["inputs_unchanged"]!.GetValue<bool>());

        var detail = await Read(page, CaseUrl);
        var revision = detail["revision"]!;
        Assert.Equal(2, detail["current_revision"]!.GetValue<int>());
        Assert.Equal(2, revision["number"]!.GetValue<int>());
        Assert.Equal("2", revision["assessment_id"]!.GetValue<string>());
        Assert.Equal("CTX-001-v1", revision["context_version"]!.GetValue<string>());
        Assert.Equal("CTX-001-v1", company["context_version"]!.GetValue<string>());
        AssertSameJson(company, Payload(await Call(mcp, "get_company_data")));

        var history = Payload(await Call(mcp, "get_case_history"));
        Assert.Collection(
            history.AsArray(),
            saved =>
            {
                AssertSameJson(revision, saved);
                Assert.Equal("register_invoice", saved!["action"]!["kind"]!.GetValue<string>());
                Assert.Equal("agent", saved["action"]!["actor"]!.GetValue<string>());
            },
            initial =>
            {
                Assert.Equal(1, initial!["number"]!.GetValue<int>());
                Assert.Null(initial["assessment_id"]);
            }
        );

        var document = revision["document"]!;
        Assert.Equal("agent", document["reading_origin"]!.GetValue<string>());
        Assert.Equal(
            "09e11592605b5a234e9e2e3df698972f3fd3e8569c6175701676d832b6f83837",
            document["content_sha256"]!.GetValue<string>()
        );
        AssertSameJson(facts, document["facts"]);
        AssertSameJson(registered["result"], revision["result"]);

        var list = await Read(page, "/api/cases");
        var row = Assert.Single(list.AsArray())!;
        Assert.Equal("2214.24", row["declared_total"]!.GetValue<string>());
        Assert.Equal("EUR", row["currency"]!.GetValue<string>());
        Assert.Equal("ready_for_accounting", row["stage"]!.GetValue<string>());
        AssertSameJson(row["stage"], detail["stage"]);
        Assert.Equal("clear", row["decision"]!.GetValue<string>());
        using var pdf = await page.GetAsync(
            $"{CaseUrl}/documents/{document["reference"]!.GetValue<long>()}/pdf"
        );
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Should_preserve_facts_and_assessment_when_the_app_restarts()
    {
        var facts = PublicCases.CleanFacts();
        facts["payment_terms"] = null;
        facts["missing_fields"]!
            .AsArray()
            .Add(new JsonObject { ["field_path"] = "/payment_terms", ["reason"] = "illegible" });
        var evidence = facts["evidence"]!.AsArray();
        evidence.Remove(
            evidence.Single(item => item!["field_path"]!.GetValue<string>() == "/payment_terms")
        );

        JsonNode saved;
        JsonNode history;
        await using (var app = _environment.Start())
        {
            await using var mcp = await ConnectMcp(app);
            var registered = Payload(await Register(mcp, facts));
            CleanAssessment.AssertResult(
                registered["result"]!,
                2,
                CaseTestEnvironment.ReviewerCaseId
            );
            saved = Payload(await Call(mcp, "get_assessment"));
            history = Payload(await Call(mcp, "get_case_history"));
        }

        await using var restarted = _environment.Start();
        await using var restartedMcp = await ConnectMcp(restarted);
        var assessment = Payload(await Call(restartedMcp, "get_assessment"));
        AssertSameJson(saved, assessment);
        var assessmentById = await restartedMcp.CallToolAsync(
            "get_assessment",
            new Dictionary<string, object?>
            {
                ["case_id"] = CaseTestEnvironment.ReviewerCaseId,
                ["assessment_id"] = "2",
            }
        );
        AssertSameJson(saved, Payload(assessmentById));
        AssertSameJson(history, Payload(await Call(restartedMcp, "get_case_history")));
        var savedFacts = assessment["revision"]!["document"]!["facts"]!;
        AssertSameJson(facts, savedFacts);
        Assert.Equal("illegible", savedFacts["missing_fields"]![0]!["reason"]!.GetValue<string>());
        Assert.Collection(
            history.AsArray(),
            revision =>
                Assert.Equal("register_invoice", revision!["action"]!["kind"]!.GetValue<string>()),
            revision => Assert.Null(revision!["result"])
        );
    }

    [Fact]
    public async Task Should_reject_without_writes_when_required_fact_structure_is_invalid()
    {
        await using var app = _environment.Start();
        await using var mcp = await ConnectMcp(app);
        using var page = app.CreateClient();
        var initial = await Read(page, CaseUrl);
        var history = Payload(await Call(mcp, "get_case_history"));
        var company = Payload(await Call(mcp, "get_company_data"));

        var facts = PublicCases.CleanFacts();
        facts["lines"]![0]!.AsObject().Remove("quantity");
        var error = Payload(await Register(mcp, facts), isError: true);
        Assert.Equal("VALIDATION_ERROR", error["error"]!["code"]!.GetValue<string>());
        Assert.Contains(error["error"]!["details"]!.AsArray(), detail =>
            detail!["path"]!.GetValue<string>() == "/facts/lines/0/quantity"
            && detail["message"]!
                .GetValue<string>()
                .Contains("required", StringComparison.OrdinalIgnoreCase)
        );

        Assert.Null(initial["revision"]!["document"]);
        Assert.Null(initial["revision"]!["result"]);
        AssertSameJson(initial, await Read(page, CaseUrl));
        AssertSameJson(history, Payload(await Call(mcp, "get_case_history")));
        AssertSameJson(company, Payload(await Call(mcp, "get_company_data")));
    }

    [Theory]
    [InlineData("2026-02-30", "/facts/issue_date")]
    public async Task Should_reject_without_writes_when_a_fact_date_does_not_exist(
        string issueDate,
        string fieldPath
    )
    {
        await using var app = _environment.Start();
        await using var mcp = await ConnectMcp(app);
        var history = Payload(await Call(mcp, "get_case_history"));
        var facts = PublicCases.CleanFacts();
        facts["issue_date"] = issueDate;

        var error = Payload(await Register(mcp, facts), isError: true)["error"]!;

        Assert.Equal("VALIDATION_ERROR", error["code"]!.GetValue<string>());
        var detail = Assert.Single(error["details"]!.AsArray())!;
        Assert.Equal(fieldPath, detail["path"]!.GetValue<string>());
        Assert.Contains("calendar date", detail["message"]!.GetValue<string>());
        AssertSameJson(history, Payload(await Call(mcp, "get_case_history")));
    }

    [Fact]
    public async Task Should_require_a_missing_reason_and_reject_a_quote_when_an_agent_leaves_iban_null()
    {
        await using var app = _environment.Start();
        await using var mcp = await ConnectMcp(app);
        var history = Payload(await Call(mcp, "get_case_history"));
        var facts = PublicCases.CleanFacts();
        facts["bank"]!["iban"] = null;

        var missingReason = Payload(await Register(mcp, facts), isError: true)["error"]!;
        Assert.Equal("VALIDATION_ERROR", missingReason["code"]!.GetValue<string>());
        Assert.Equal(
            "/facts/bank/iban",
            Assert.Single(missingReason["details"]!.AsArray())!["path"]!.GetValue<string>()
        );

        facts["missing_fields"]!
            .AsArray()
            .Add(new JsonObject { ["field_path"] = "/bank/iban", ["reason"] = "illegible" });
        int evidenceIndex = facts["evidence"]!
            .AsArray()
            .Select((item, index) => (Path: item!["field_path"]!.GetValue<string>(), Index: index))
            .Single(item => item.Path == "/bank/iban")
            .Index;
        var nullQuote = Payload(await Register(mcp, facts), isError: true)["error"]!;
        Assert.Equal("VALIDATION_ERROR", nullQuote["code"]!.GetValue<string>());
        Assert.Equal(
            $"/facts/evidence/{evidenceIndex}/field_path",
            Assert.Single(nullQuote["details"]!.AsArray())!["path"]!.GetValue<string>()
        );
        AssertSameJson(history, Payload(await Call(mcp, "get_case_history")));
    }

    [Fact]
    public async Task Should_reject_evidence_when_it_points_to_itself()
    {
        await using var app = _environment.Start();
        await using var mcp = await ConnectMcp(app);
        var history = Payload(await Call(mcp, "get_case_history"));
        var facts = PublicCases.CleanFacts();
        facts["evidence"]![0]!["field_path"] = "/evidence/0/excerpt";

        var error = Payload(await Register(mcp, facts), isError: true)["error"]!;

        Assert.Equal("VALIDATION_ERROR", error["code"]!.GetValue<string>());
        Assert.Equal(
            "/facts/evidence/0/field_path",
            Assert.Single(error["details"]!.AsArray())!["path"]!.GetValue<string>()
        );
        AssertSameJson(history, Payload(await Call(mcp, "get_case_history")));
    }

    [Fact]
    public async Task Should_return_a_source_validation_error_when_the_mcp_source_is_null()
    {
        await using var app = _environment.Start();
        await using var mcp = await ConnectMcp(app);
        var history = Payload(await Call(mcp, "get_case_history"));

        var error = Payload(
            await Register(mcp, PublicCases.CleanFacts(), source: null),
            isError: true
        )["error"]!;

        Assert.Equal("VALIDATION_ERROR", error["code"]!.GetValue<string>());
        Assert.Equal(
            "/source",
            Assert.Single(error["details"]!.AsArray())!["path"]!.GetValue<string>()
        );
        AssertSameJson(history, Payload(await Call(mcp, "get_case_history")));
    }

    [Fact]
    public async Task Should_return_saved_assessment_when_unchanged_inputs_are_assessed()
    {
        await using var app = _environment.Start();
        await using var mcp = await ConnectMcp(app);
        var registered = Payload(await Register(mcp, PublicCases.CleanFacts()));
        var history = Payload(await Call(mcp, "get_case_history"));

        var reassessed = Payload(await Call(mcp, "assess_invoice"));
        Assert.True(reassessed["inputs_unchanged"]!.GetValue<bool>());
        AssertSameJson(registered["assessment_id"], reassessed["assessment_id"]);
        AssertSameJson(registered["result"], reassessed["result"]);
        AssertSameJson(history, Payload(await Call(mcp, "get_case_history")));
        Assert.Equal(2, history.AsArray().Count);
        Assert.Single(history.AsArray(), revision => revision!["result"] is not null);
    }

    public void Dispose() => _environment.Dispose();
}
