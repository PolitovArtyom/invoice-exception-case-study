using System.Text.Json.Nodes;
using InvoiceDesk.Tests.Support;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;

namespace InvoiceDesk.Tests.Api;

public sealed class StartupLoadingTests
{
    [Fact]
    public async Task Should_register_only_allow_listed_facts_when_a_fresh_app_starts()
    {
        using var environment = new CaseTestEnvironment();
        environment.CopyCase(PublicCases.CleanCase);
        foreach (
            string id in new[]
            {
                "case-002",
                "case-003",
                "case-004",
                "case-005",
                "case-006",
                "case-007",
                "case-008",
                "case-009",
                "case-010",
            }
        )
            environment.CopyCase(id);
        await using (var app = environment.Start())
        {
            using var page = app.CreateClient();

            await AssertStartupCases(page);
        }

        await using var restarted = environment.Start();
        using var restartedPage = restarted.CreateClient();
        await AssertStartupCases(restartedPage);
    }

    private static async Task AssertStartupCases(HttpClient page)
    {
        var list = (await Read(page, "/api/cases"))["cases"]!.AsArray();
        Assert.Equal(
            new[]
            {
                "case-001",
                "case-002",
                "case-003",
                "case-004",
                "case-005",
                "case-006",
                "case-007",
                "case-008",
                "case-009",
                "case-010",
            },
            list.Select(row => row!["case_id"]!.GetValue<string>())
        );
        var clean = (await Read(page, "/api/cases/case-001"))["revision"]!;
        Assert.Equal("fixture", clean["document"]!["reading_origin"]!.GetValue<string>());
        CleanAssessment.AssertResult(clean["result"]!, 1);

        foreach (
            var (id, decision) in new[]
            {
                ("case-002", "clear"),
                ("case-003", "blocked"),
                ("case-005", "blocked"),
                ("case-006", "blocked"),
                ("case-007", "blocked"),
                ("case-009", "blocked"),
            }
        )
        {
            var loaded = (await Read(page, $"/api/cases/{id}"))["revision"]!;
            Assert.Equal("fixture", loaded["document"]!["reading_origin"]!.GetValue<string>());
            Assert.Equal(decision, loaded["result"]!["decision"]!.GetValue<string>());
        }

        foreach (
            var (id, version) in new[] { ("case-004", "CTX-004-v1"), ("case-010", "CTX-010-v1") }
        )
        {
            var pending = await Read(page, $"/api/cases/{id}");
            Assert.Null(pending["revision"]!["document"]);
            Assert.Null(pending["revision"]!["result"]);
            Assert.Equal(version, pending["revision"]!["context_version"]!.GetValue<string>());
            Assert.NotEmpty(pending["case"]!["sources"]!.AsArray());
        }
    }

    [Fact]
    public async Task Should_leave_facts_unread_when_a_reviewer_adds_an_unlisted_case()
    {
        using var environment = new CaseTestEnvironment();
        string folder = environment.AddReviewerCase();
        string stepPath = Path.Combine(folder, "steps", "step-001.json");
        var step = JsonNode.Parse(File.ReadAllText(stepPath))!;
        step["document"]!["facts"]!.AsObject().Remove("lines");
        File.WriteAllText(stepPath, step.ToJsonString());
        await using var app = environment.Start();
        using var page = app.CreateClient();
        await using var mcp = await ConnectMcp(app);

        var pending = await Read(page, "/api/cases/case-911");
        Assert.Equal("own", pending["case"]!["origin"]!.GetValue<string>());
        Assert.Equal("not_read", pending["stage"]!.GetValue<string>());
        var rows = await Read(page, "/api/cases");
        AssertSameJson(Assert.Single(rows["cases"]!.AsArray())!["stage"], pending["stage"]);
        Assert.Equal(1, pending["current_revision"]!.GetValue<int>());
        Assert.Null(pending["revision"]!["document"]);
        Assert.Null(pending["revision"]!["result"]);
        var opened = Payload(await Call(mcp, "open_case", "case-911"));
        Assert.Null(opened["current_document_id"]);
        var source = Assert.Single(opened["sources"]!.AsArray())!;
        Assert.True(source["available"]!.GetValue<bool>());
        Assert.Equal("own", source["root"]!.GetValue<string>());
        Assert.Equal(
            "scenarios/cases/case-911/documents/document-001.pdf",
            source["host_path"]!.GetValue<string>()
        );
        Assert.Equal(
            Path.Combine(
                environment.ScenarioPath,
                "cases",
                "case-911",
                "documents",
                "document-001.pdf"
            ),
            source["container_path"]!.GetValue<string>()
        );
        Assert.Equal("CTX-001-v1", pending["revision"]!["context_version"]!.GetValue<string>());
        Assert.DoesNotContain(
            "REVIEWER-FACTS-MUST-NOT-LOAD",
            opened.ToJsonString(),
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "EXPECTED DATA MUST NOT BE READ",
            opened.ToJsonString(),
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData("case.json")]
    [InlineData("steps/step-001.json")]
    public async Task Should_fail_startup_when_a_case_input_id_differs_from_the_folder_name(
        string relativePath
    )
    {
        using var environment = new CaseTestEnvironment();
        string folder = environment.CopyCase(PublicCases.CleanCase);
        string path = Path.Combine(folder, relativePath);
        var input = JsonNode.Parse(File.ReadAllText(path))!;
        input["case_id"] = "case-900";
        File.WriteAllText(path, input.ToJsonString());
        await using var app = environment.Start();

        var error = Assert.Throws<InvalidDataException>(() => app.CreateClient());

        Assert.Contains(folder, error.Message, StringComparison.Ordinal);
        Assert.Contains(relativePath, error.Message, StringComparison.Ordinal);
        Assert.Contains(
            "case_id 'case-900' must match folder name 'case-001'",
            error.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Should_name_both_folders_when_a_saved_case_id_is_reused_across_roots()
    {
        using var environment = new CaseTestEnvironment();
        string publicFolder = environment.CopyCase(PublicCases.CleanCase);
        await using (var app = environment.Start())
        {
            using var page = app.CreateClient();
            var saved = await Read(page, "/api/cases/case-001");
            CleanAssessment.AssertResult(saved["revision"]!["result"]!, 1);
        }

        string ownFolder = environment.CopyCase(PublicCases.CleanCase, own: true);
        await using var restarted = environment.Start();

        var error = Assert.Throws<InvalidDataException>(() => restarted.CreateClient());
        Assert.Contains("Case ID 'case-001'", error.Message, StringComparison.Ordinal);
        Assert.Contains(publicFolder, error.Message, StringComparison.Ordinal);
        Assert.Contains(ownFolder, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_fail_startup_when_the_fixture_hash_differs_from_the_manifest()
    {
        using var environment = new CaseTestEnvironment();
        string folder = environment.CopyCase(PublicCases.CleanCase);
        string path = Path.Combine(folder, "case.json");
        var manifest = JsonNode.Parse(File.ReadAllText(path))!;
        manifest["input_documents"]![0]!["content_sha256"] = new string('0', 64);
        File.WriteAllText(path, manifest.ToJsonString());
        await using var app = environment.Start();

        var error = Assert.Throws<InvalidDataException>(() => app.CreateClient());
        Assert.Contains(folder, error.Message, StringComparison.Ordinal);
        Assert.Contains("DOC-001", error.Message, StringComparison.Ordinal);
        Assert.Contains("SHA-256", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_fail_startup_when_the_first_step_of_a_saved_case_is_missing()
    {
        using var environment = new CaseTestEnvironment();
        string folder = environment.CopyCase(PublicCases.CleanCase);
        await using (var app = environment.Start())
        {
            using var page = app.CreateClient();
            var saved = await Read(page, "/api/cases/case-001");
            CleanAssessment.AssertResult(saved["revision"]!["result"]!, 1);
        }

        File.Delete(Path.Combine(folder, "steps", "step-001.json"));
        await using var restarted = environment.Start();

        var error = Assert.Throws<InvalidDataException>(() => restarted.CreateClient());
        Assert.Contains(folder, error.Message, StringComparison.Ordinal);
        Assert.Contains("First step file is missing", error.Message, StringComparison.Ordinal);
    }
}
