using InvoiceDesk.Tests.Support;

using static InvoiceDesk.Tests.Support.InvoiceApiClient;

namespace InvoiceDesk.Tests.Api;

public sealed class StartupLoadingTests
{
    [Fact]
    public async Task Should_register_only_allow_listed_facts_when_a_fresh_app_starts()
    {
        using var environment = new CaseTestEnvironment();
        string cleanFolder = environment.CopyCase(PublicCases.CleanCase);
        environment.CopyCase("case-002");
        environment.CopyCase("case-004");
        environment.CopyCase("case-010");
        await using (var app = environment.Start(loadCasesOnStartup: true))
        {
            using var page = app.CreateClient();

            await AssertStartupCases(page);
        }

        File.WriteAllText(Path.Combine(cleanFolder, "case.json"), "Existing case must be skipped.");
        await using var restarted = environment.Start(loadCasesOnStartup: true);
        using var restartedPage = restarted.CreateClient();
        await AssertStartupCases(restartedPage);
    }

    private static async Task AssertStartupCases(HttpClient page)
    {
        var list = (await Read(page, "/api/cases")).AsArray();
        Assert.Equal(new[] { "case-001", "case-002", "case-004", "case-010" },
            list.Select(row => row!["case_id"]!.GetValue<string>()));
        var clean = (await Read(page, "/api/cases/case-001"))["revision"]!;
        Assert.Equal("loaded from test data, not read by the agent", clean["document"]!["reading_origin"]!.GetValue<string>());
        CleanAssessment.AssertResult(clean["result"]!, 1);

        foreach (var (id, version) in new[] { ("case-002", "CTX-002-v1"), ("case-004", "CTX-004-v1"), ("case-010", "CTX-010-v1") })
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
        environment.AddReviewerCase();
        await using var app = environment.Start(loadCasesOnStartup: true);
        using var page = app.CreateClient();
        await using var mcp = await ConnectMcp(app);

        var pending = await Read(page, "/api/cases/case-911");
        Assert.Equal("own", pending["case"]!["origin"]!.GetValue<string>());
        Assert.Equal(1, pending["current_revision"]!.GetValue<int>());
        Assert.Null(pending["revision"]!["document"]);
        Assert.Null(pending["revision"]!["result"]);
        var opened = Payload(await Call(mcp, "open_case", "case-911"));
        Assert.Null(opened["current_document_id"]);
        var source = Assert.Single(opened["sources"]!.AsArray())!;
        Assert.True(source["available"]!.GetValue<bool>());
        Assert.Equal("own", source["root"]!.GetValue<string>());
        Assert.Equal("scenarios/cases/case-911/documents/document-001.pdf", source["host_path"]!.GetValue<string>());
        Assert.Equal(Path.Combine(environment.ScenarioPath, "cases", "case-911", "documents", "document-001.pdf"),
            source["container_path"]!.GetValue<string>());
        Assert.Equal("CTX-001-v1", pending["revision"]!["context_version"]!.GetValue<string>());
        Assert.DoesNotContain("REVIEWER-FACTS-MUST-NOT-LOAD", opened.ToJsonString(), StringComparison.Ordinal);
        Assert.DoesNotContain("EXPECTED DATA MUST NOT BE READ", opened.ToJsonString(), StringComparison.Ordinal);
    }
}
