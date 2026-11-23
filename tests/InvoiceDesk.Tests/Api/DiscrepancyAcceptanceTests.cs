using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Tests.Support;
using ModelContextProtocol.Client;
using static InvoiceDesk.Tests.Support.AssessmentAssertions;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;

namespace InvoiceDesk.Tests.Api;

public sealed class DiscrepancyAcceptanceTests
{
    internal const string CaseId = "case-004";
    internal const string CaseUrl = "/api/cases/" + CaseId;
    internal const string Person = "Ana Petrović";
    internal const string Reason = "The supplier and buyer agreed to 12.52.";
    private const string IneligibleMessage =
        "The current assessment is ineligible for discrepancy acceptance.";

    [Fact]
    public async Task Should_keep_rules_result_unchanged_when_a_person_accepts_an_eligible_discrepancy()
    {
        using var environment = new CaseTestEnvironment();
        environment.CopyCase(CaseId);
        await using var app = environment.Start();
        using var page = app.CreateClient();
        await using var mcp = await ConnectMcp(app);
        var result = await RegisterPublicInvoice(mcp, CaseId);
        var before = await Read(page, CaseUrl);
        AssertReview(result);
        Assert.Equal("needs_attention", Text(before, "stage"));
        Assert.True(before["can_accept"]!.GetValue<bool>());

        var accepted = await Accept(page);
        Assert.Equal("accepted_with_discrepancy", Text(accepted, "stage"));
        Assert.Equal(2, accepted["current_revision"]!.GetValue<int>());
        Assert.False(accepted["can_accept"]!.GetValue<bool>());
        AssertAcceptance(accepted["acceptance"], "2");
        AssertSameJson(result, accepted["revision"]!["result"]);
        Assert.Equal("2", Text(accepted["revision"]!, "assessment_id"));
        var row = Assert.Single((await Read(page, "/api/cases"))["cases"]!.AsArray())!;
        Assert.Equal("ready_for_accounting", Text(row, "tab"));
        Assert.Equal("needs_review", Text(row, "decision"));
        Assert.Equal(Person, Text(row, "accepted_by"));
        Assert.Equal(2, row["assessed_revision"]!.GetValue<int>());
        Assert.Equal("CTX-004-v1", Text(row, "context_version"));
        var history = (await Read(page, CaseUrl + "/history")).AsArray();
        Assert.Equal(2, history.Count);
        AssertSameJson(accepted["acceptance"], history[0]!["acceptance"]);
        AssertSameJson(result, history[0]!["result"]);

        using var recheck = await Recheck(page, CaseId);
        recheck.EnsureSuccessStatusCode();
        AssertSameJson(result, (await Body(recheck))["result"]);
        AssertSameJson(accepted, await Read(page, CaseUrl));
        using var repeated = await SendAcceptance(page, CaseId, 2, "2", "Another person", Reason);
        await AssertRejected(repeated, "/assessment_id");
        AssertSameJson(history, await Read(page, CaseUrl + "/history"));

        AssertDuplicate(await RegisterPublicReading(mcp, CaseId), "DOC-004", "DOC-004", "2", 3);
        var afterImport = await Read(page, CaseUrl);
        Assert.Equal(3, afterImport["current_revision"]!.GetValue<int>());
        Assert.Equal("accepted_with_discrepancy", Text(afterImport, "stage"));
        Assert.False(afterImport["can_accept"]!.GetValue<bool>());
        AssertSameJson(accepted["acceptance"], afterImport["acceptance"]);
        AssertSameJson(result, afterImport["revision"]!["result"]);
        var afterDuplicate = (await Read(page, CaseUrl + "/history")).AsArray();
        AssertSameJson(accepted["acceptance"], afterDuplicate[0]!["acceptance"]);
        Assert.Null(afterDuplicate[0]!["result"]);
        AssertSameJson(history[0], afterDuplicate[1]);
        var duplicateView = await Read(page, CaseUrl + "/revisions/3");
        Assert.True(duplicateView["show_duplicate_reading"]!.GetValue<bool>());
        Assert.False(duplicateView["can_correct"]!.GetValue<bool>());
        AssertSameJson(accepted["acceptance"], duplicateView["acceptance"]);
        Assert.Null(duplicateView["revision"]!["result"]);
    }

    [Fact]
    public async Task Should_skip_own_counted_record_when_a_prior_assessment_was_accepted()
    {
        var baseline = MatchedInvoiceScenario.Baseline();
        var orderLine = baseline.CompanyData.PurchaseOrders.Orders[0].Lines[0];
        var scenario = baseline.WithOrderLines(orderLine with { UnitNetPrice = 9.98m });
        await using var test = await ProposalCase.Start(scenario);
        AssertOutcome(
            test.Initial,
            "needs_review",
            "D",
            ["L"],
            [new("UNIT_PRICE", "L", "fail", "needs_review")]
        );

        using var accepted = await SendAcceptance(
            test.Page,
            ProposalCase.CaseId,
            2,
            "2",
            Person,
            Reason
        );
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var view = await Read(test.Page, ProposalCase.CaseUrl);
        Assert.Equal("accepted_with_discrepancy", Text(view, "stage"));
        string contentSha256 = Text(view["revision"]!["document"]!, "content_sha256")!;
        var counted = scenario.WithCountedInvoice(scenario.Facts, "D").CompanyData;
        test.WriteCompanyDataUpdate(
            InvoiceJson.Write(
                counted with
                {
                    ContextVersion = "CTX-COUNTED",
                    InvoiceHistory = counted.InvoiceHistory with
                    {
                        Documents =
                        [
                            counted.InvoiceHistory.Documents[0] with
                            {
                                ContentSha256 = contentSha256,
                            },
                        ],
                    },
                }
            )
        );
        using var response = await Recheck(test.Page, ProposalCase.CaseId);
        response.EnsureSuccessStatusCode();
        var reassessed = await Body(response);
        Assert.Equal("3", Text(reassessed, "assessment_id"));
        AssertOutcome(
            reassessed["result"]!,
            "needs_review",
            "D",
            ["L"],
            [new("UNIT_PRICE", "L", "fail", "needs_review")]
        );
    }

    [Theory]
    [InlineData("blocked", "case-005", 1, "1", Person, Reason, "/assessment_id", IneligibleMessage)]
    [InlineData(
        "needs_information",
        "case-010",
        2,
        "2",
        Person,
        Reason,
        "/assessment_id",
        IneligibleMessage
    )]
    [InlineData(
        "proposed",
        "case-010",
        3,
        "3",
        Person,
        Reason,
        "/assessment_id",
        IneligibleMessage
    )]
    [InlineData(
        "review",
        CaseId,
        1,
        "2",
        Person,
        Reason,
        "/expected_revision",
        "The case changed. Open the latest revision and review the action again."
    )]
    [InlineData(
        "review",
        CaseId,
        2,
        "2",
        " ",
        Reason,
        "/person",
        "Enter a name of up to 128 characters."
    )]
    [InlineData("review", CaseId, 2, "2", Person, " ", "/reason", "Enter a reason.")]
    [InlineData("review", CaseId, 2, "1", Person, Reason, "/assessment_id", IneligibleMessage)]
    public async Task Should_reject_without_writes_when_acceptance_is_ineligible(
        string setup,
        string caseId,
        int revision,
        string assessmentId,
        string person,
        string reason,
        string path,
        string expectedMessage
    )
    {
        using var environment = new CaseTestEnvironment();
        environment.CopyCase("case-005");
        environment.CopyCase("case-010");
        environment.CopyCase(CaseId);
        await using var app = environment.Start();
        using var page = app.CreateClient();
        await using var mcp = await ConnectMcp(app);
        Dictionary<string, (Func<Task> Prepare, Action<JsonNode> AssertAssessment)> scenarios =
            new()
            {
                ["blocked"] = (
                    () => Task.CompletedTask,
                    PublicDiscrepancyTests.AssertBlockedTotals
                ),
                ["needs_information"] = (
                    async () => await RegisterPublicInvoice(mcp, "case-010"),
                    PublicProposalLifecycleTests.AssertNeedsInformation
                ),
                ["proposed"] = (
                    async () =>
                    {
                        await RegisterPublicInvoice(mcp, "case-010");
                        var proposal = JsonNode.Parse(
                            File.ReadAllText(PublicCases.Steps("case-010").ElementAt(1))
                        )!;
                        Payload(await Propose(mcp, proposal["proposals"]![0]!, "case-010"));
                    },
                    PublicProposalLifecycleTests.AssertAppliedProposal
                ),
                ["review"] = (async () => await RegisterPublicInvoice(mcp, CaseId), AssertReview),
            };
        var (prepare, assertAssessment) = scenarios[setup];
        await prepare();

        string url = $"/api/cases/{caseId}";
        var view = await Read(page, url);
        assertAssessment(view["revision"]!["result"]!);
        var before = await Read(page, url + "/history");
        using var response = await SendAcceptance(
            page,
            caseId,
            revision,
            assessmentId,
            person,
            reason
        );
        await AssertRejected(response, path, expectedMessage);
        AssertSameJson(before, await Read(page, url + "/history"));
        AssertSameJson(view, await Read(page, url));
    }

    [Theory]
    [InlineData("context", "2", 3)]
    [InlineData("proposal", "2", 3)]
    [InlineData("confirmation", "3", 4)]
    [InlineData("correction", "2", 3)]
    public async Task Should_expire_acceptance_when_a_new_assessment_is_saved(
        string action,
        string assessment,
        int nextRevision
    )
    {
        using var environment = new CaseTestEnvironment();
        environment.CopyCase(CaseId);
        await using var app = environment.Start();
        using var page = app.CreateClient();
        await using var mcp = await ConnectMcp(app);
        await RegisterPublicInvoice(mcp, CaseId);

        var proposal = new JsonObject
        {
            ["proposal_id"] = "P-004",
            ["actor"] = "agent",
            ["document_id"] = "DOC-004",
            ["order_id"] = "ORD-004",
            ["line_matches"] = new JsonArray(),
        };
        var context = PublicCases.ReadFirstStep(CaseId)["context"]!;
        context["context_version"] = "CTX-004-v2";
        Func<Task> propose = async () =>
            AssertReview(Payload(await Propose(mcp, proposal, CaseId))["result"]!);
        Func<Task> unchanged = () => Task.CompletedTask;
        Dictionary<string, (Func<Task> Prepare, Func<Task> Apply)> actions = new()
        {
            ["context"] = (unchanged, async () =>
            {
                environment.WriteCompanyDataUpdate(CaseId, context.ToJsonString());
                using var response = await Recheck(page, CaseId);
                response.EnsureSuccessStatusCode();
                var revision = (await Read(page, CaseUrl + "/revisions/3"))["revision"]!;
                Assert.Equal("CTX-004-v2", Text(revision, "context_version"));
                Assert.True(revision["action"]!["company_data_submitted"]!.GetValue<bool>());
            }),
            ["proposal"] = (unchanged, propose),
            ["confirmation"] = (propose, async () =>
            {
                using var response = await Confirm(page, CaseId, ProposalCase.Form("P-004", 3));
                response.EnsureSuccessStatusCode();
                var revision = (await Read(page, CaseUrl + "/revisions/4"))["revision"]!;
                var confirmation = Assert.Single(revision["action"]!["confirmations"]!.AsArray())!;
                Assert.Equal("P-004", Text(confirmation, "proposal_id"));
                Assert.Equal("Ana", Text(confirmation, "confirmed_by"));
            }),
            ["correction"] = (unchanged, async () =>
                await FactCorrectionTests.Correct(
                    page,
                    CaseId,
                    2,
                    "/payment_terms",
                    "Bank transfer"
                )
            ),
        };
        var (prepare, update) = actions[action];
        await prepare();

        var accepted = await Accept(page, nextRevision - 1, assessment);
        AssertAcceptance(accepted["acceptance"], assessment);
        await update();

        var current = await Read(page, CaseUrl);
        Assert.Equal(nextRevision, current["current_revision"]!.GetValue<int>());
        Assert.Equal("needs_attention", Text(current, "stage"));
        Assert.Null(current["acceptance"]);
        AssertReview(current["revision"]!["result"]!);
        var history = (await Read(page, CaseUrl + "/history")).AsArray();
        Assert.Null(history[0]!["acceptance"]);
        AssertSameJson(accepted["acceptance"], history[1]!["acceptance"]);
        var historical = await Read(page, $"{CaseUrl}/revisions/{nextRevision - 1}");
        AssertSameJson(accepted["acceptance"], historical["acceptance"]);
        Assert.False(historical["can_accept"]!.GetValue<bool>());
    }

    internal static async Task<JsonNode> RegisterPublicInvoice(McpClient mcp, string caseId) =>
        (await RegisterPublicReading(mcp, caseId))["result"]!;

    private static async Task<JsonNode> RegisterPublicReading(McpClient mcp, string caseId) =>
        Payload(
            await Register(
                mcp,
                PublicCases.ReadFirstStep(caseId)["document"]!["facts"]!,
                new
                {
                    root = "public",
                    relative_path = $"cases/{caseId}/documents/document-001.pdf",
                },
                caseId
            )
        );

    internal static async Task<JsonNode> Accept(
        HttpClient page,
        int revision = 2,
        string assessment = "2"
    )
    {
        using var response = await SendAcceptance(
            page,
            CaseId,
            revision,
            assessment,
            Person,
            Reason
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var acceptance = await Body(response);
        AssertAcceptance(acceptance, assessment);
        var saved = await Read(page, CaseUrl);
        AssertSameJson(acceptance, saved["acceptance"]);
        return saved;
    }

    private static Task<HttpResponseMessage> SendAcceptance(
        HttpClient page,
        string caseId,
        int revision,
        string assessment,
        string person,
        string reason
    ) =>
        page.PostAsJsonAsync(
            $"/api/cases/{caseId}/discrepancy-acceptances",
            new
            {
                expected_revision = revision,
                assessment_id = assessment,
                person,
                reason,
            }
        );

    internal static async Task AssertRejected(
        HttpResponseMessage response,
        string path,
        string? expectedMessage = null
    )
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await Body(response))["error"]!;
        Assert.Equal("VALIDATION_ERROR", Text(error, "code"));
        var detail = Assert.Single(error["details"]!.AsArray(), detail =>
            Text(detail!, "path") == path
        );
        string? message = Text(detail!, "message");
        Assert.False(string.IsNullOrWhiteSpace(message));
        if (expectedMessage is not null)
            Assert.Equal(expectedMessage, message);
    }

    internal static void AssertAcceptance(JsonNode? acceptance, string assessment)
    {
        Assert.NotNull(acceptance);
        Assert.Equal(assessment, Text(acceptance, "assessment_id"));
        Assert.Equal(Person, Text(acceptance, "person"));
        Assert.Equal(Reason, Text(acceptance, "reason"));
        Assert.True(DateTimeOffset.TryParse(Text(acceptance, "recorded_at"), out _));
    }

    private static void AssertReview(JsonNode result)
    {
        AssertOutcome(
            result,
            "needs_review",
            "DOC-004",
            ["L01", "L02"],
            [new("UNIT_PRICE", "L01", "fail", "needs_review")],
            matchedOrderId: "ORD-004"
        );
        AssertTotals(result, "1116.80", "223.36", "1340.16");
        AssertLine(
            result,
            "L01",
            "OL-004-01",
            "deterministic",
            "1001.60",
            "200.32",
            "80",
            "80",
            available: "80",
            remaining: "80"
        );
        AssertLine(
            result,
            "L02",
            "OL-004-02",
            "deterministic",
            "115.20",
            "23.04",
            "24",
            "24",
            available: "24",
            remaining: "24"
        );
    }
}
