using System.Text.Json.Nodes;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Tests.Support;
using static InvoiceDesk.Tests.Api.OrderMatchingTests;
using static InvoiceDesk.Tests.Api.ProposalAssessmentTests;
using static InvoiceDesk.Tests.Api.ProposalValidationTests;
using static InvoiceDesk.Tests.Support.AssessmentAssertions;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;
using RunnerAdapter = InvoiceDesk.Adapter.Adapter;

namespace InvoiceDesk.Tests.Scenarios;

public sealed class AdapterProposalTests
{
    [Fact]
    public async Task Should_apply_one_revision_when_a_combined_step_is_valid()
    {
        using var state = new TemporaryDirectory();
        var scenario = WithUnknownReference(MatchedInvoiceScenario.Baseline());
        var result = Submit(Combined(scenario), state.Path);
        AssertConfirmed(result, 1);

        using var environment = new CaseTestEnvironment();
        await using var app = environment.Start(Path.Combine(state.Path, "state.sqlite"));
        using var page = app.CreateClient();
        var revision = Assert.Single(
            (await Read(page, ProposalCase.CaseUrl + "/history")).AsArray()
        )!;
        AssertSameJson(result, revision["result"]);
        Assert.Equal("P", revision["action"]!["proposal_ids"]![0]!.GetValue<string>());
        Assert.Equal("Reviewer", Text(revision["action"]!["confirmations"]![0]!, "confirmed_by"));
    }

    [Fact]
    public async Task Should_leave_state_unchanged_when_a_later_step_section_is_invalid()
    {
        using var state = new TemporaryDirectory();
        var baseline = MatchedInvoiceScenario.Baseline();
        var first = Submit(Step(baseline), state.Path);
        Assert.Equal(1, first["input_revision"]!.GetValue<int>());
        var original = baseline.Facts;
        var pending = WithUnknownReference(
            baseline with
            {
                Facts = original with { Number = original.Number + "-NEXT" },
                CompanyData = baseline.CompanyData with { ContextVersion = "CTX-NEXT" },
            }
        );
        var combined = Combined(pending, 2);
        combined["confirmations"]![0]!["proposal_id"] = "UNKNOWN";

        var failure = Submit(combined, state.Path, 2);
        AssertValidation(failure, "/confirmations/0/proposal_id", "Proposal does not exist.");

        using var environment = new CaseTestEnvironment();
        await using (var app = environment.Start(Path.Combine(state.Path, "state.sqlite")))
        {
            using var page = app.CreateClient();
            var current = await Read(page, ProposalCase.CaseUrl);
            Assert.Equal(1, current["current_revision"]!.GetValue<int>());
            var revision = Assert.Single(
                (await Read(page, ProposalCase.CaseUrl + "/history")).AsArray()
            )!;
            Assert.Equal("CTX-BASELINE", Text(revision, "context_version"));
            AssertSameJson(first, revision["result"]);
            Assert.Empty(current["proposals"]!.AsArray());
        }

        combined["confirmations"]![0]!["proposal_id"] = "P";
        var retried = Submit(combined, state.Path);
        AssertConfirmed(retried, 2);
        Assert.Equal("CTX-NEXT", Text(retried, "context_version"));
    }

    [Fact]
    public void Should_bind_a_reused_proposal_to_the_new_invoice_without_its_old_confirmation()
    {
        using var state = new TemporaryDirectory();
        var scenario = WithUnknownReference(MatchedInvoiceScenario.Baseline());
        var initial = Combined(scenario);
        AssertConfirmed(Submit(initial, state.Path), 1);

        var resend = Step(scenario, 2);
        resend.Remove("context");
        resend["document"]!["content_sha256"] = new string('b', 64);
        resend["document"]!["facts"]!["due_date"] = "2026-12-31";
        var changed = Submit(resend, state.Path);
        AssertOutcome(
            changed,
            "blocked",
            "D",
            ["L"],
            [
                new("DOCUMENT_CONFLICT", "D", "fail", "blocked"),
                new("ORDER_MATCH", "D", "unknown"),
                Dependent("SUPPLIER_IDENTITY", "D", "ORDER_MATCH"),
                Dependent("BUYER_IDENTITY", "D", "ORDER_MATCH"),
                Dependent("CURRENCY_MATCH", "D", "ORDER_MATCH"),
                .. ComparisonsWithoutOrder("L"),
            ],
            [("order_reference", "D")],
            matchedOrderId: null,
            orderMatchSource: null,
            disposition: "conflict",
            originalDocumentId: "D",
            lineMatchSources: [null]
        );
        AssertUnmatchedLine(changed);
        AssertTotals(changed, "10.00", "2.00", "12.00");

        var proposal = initial["proposals"]!.DeepClone();
        var rebound = Submit(FollowUp(3, "proposals", proposal), state.Path);
        AssertOutcome(
            rebound,
            "blocked",
            "D",
            ["L"],
            [
                new("DOCUMENT_CONFLICT", "D", "fail", "blocked"),
                new("ORDER_MATCH", "D", "fail", "needs_review"),
            ],
            orderMatchSource: "agent_proposal",
            disposition: "conflict",
            originalDocumentId: "D"
        );
        AssertLine(rebound, "L", "OL", "deterministic", "10.00", "2.00");
        AssertTotals(rebound, "10.00", "2.00", "12.00");
        Assert.Equal(3, rebound["input_revision"]!.GetValue<int>());
    }

    [Fact]
    public void Should_record_runner_confirmation_when_the_proposed_order_is_missing()
    {
        using var state = new TemporaryDirectory();
        var scenario = WithUnknownReference(MatchedInvoiceScenario.Baseline());
        var initial = Combined(scenario);
        var confirmation = initial["confirmations"]!.DeepClone();
        initial.Remove("confirmations");
        Assert.Equal("agent_proposal", Text(Submit(initial, state.Path), "order_match_source"));

        var missingOrder = FollowUp(2, "confirmations", confirmation);
        missingOrder["context"] = JsonNode.Parse(
            InvoiceJson.Write(scenario.WithOrders().CompanyData)
        );
        AssertUnknownOrder(Submit(missingOrder, state.Path));

        var restored = FollowUp(3, "context", initial["context"]!.DeepClone());
        AssertConfirmed(Submit(restored, state.Path), 3);
    }

    [Theory]
    [InlineData("proposals", "human", "/proposals/0/actor", "agent")]
    [InlineData("confirmations", "agent", "/confirmations/0/actor", "human")]
    [Trait("H", "H-39")]
    public void Should_reject_actor_when_it_violates_the_runner_schema(
        string section,
        string actor,
        string path,
        string expectedActor
    )
    {
        using var state = new TemporaryDirectory();
        var combined = Combined(WithUnknownReference(MatchedInvoiceScenario.Baseline()));
        combined[section]![0]!["actor"] = actor;
        AssertValidation(Submit(combined, state.Path, 2), path, expectedActor);
        Assert.Empty(Directory.GetFiles(state.Path));
    }

    [Fact]
    public void Should_keep_duplicate_disposition_when_context_follows_a_duplicate_only_import()
    {
        using var state = new TemporaryDirectory();
        var initial = PublicCases.ReadFirstStep("case-008");
        AssertDuplicateOnly(Submit(initial, state.Path), 1, "CTX-008-v1");
        var following = initial.DeepClone().AsObject();
        following.Remove("document");
        following["context"]!["context_version"] = "CTX-REFRESHED";
        following["step"] = 2;

        AssertDuplicateOnly(Submit(following, state.Path), 2, "CTX-REFRESHED");
        following.Remove("context");
        var proposal = ProposalCase.Proposal("P", "ORD-008");
        proposal["document_id"] = "DOC-008";
        following["proposals"] = new JsonArray(proposal);
        following["step"] = 3;
        AssertValidation(
            Submit(following, state.Path, 2),
            "/proposals/0/document_id",
            "active invoice"
        );
        following.Remove("proposals");
        following["context"] = initial["context"]!.DeepClone();
        AssertDuplicateOnly(Submit(following, state.Path), 3, "CTX-008-v1");
    }

    private static JsonObject FollowUp(int step, string field, JsonNode value) =>
        new()
        {
            ["schema_version"] = "1.0",
            ["case_id"] = ProposalCase.CaseId,
            ["step"] = step,
            ["as_of"] = "2026-11-22",
            ["policy_ref"] = "core-v1",
            [field] = value,
        };

    private static JsonObject Combined(MatchedInvoiceScenario scenario, int number = 1)
    {
        var step = Step(scenario, number);
        step["proposals"] = new JsonArray(ProposalCase.Proposal("P", "O", [new("L", "OL")]));
        step["confirmations"] = JsonNode.Parse(
            """[{"proposal_id":"P","actor":"human","confirmed_by":"Reviewer"}]"""
        );
        return step;
    }

    private static JsonObject Step(MatchedInvoiceScenario scenario, int number = 1) =>
        new()
        {
            ["schema_version"] = "1.0",
            ["case_id"] = ProposalCase.CaseId,
            ["step"] = number,
            ["as_of"] = "2026-11-22",
            ["policy_ref"] = "core-v1",
            ["context"] = JsonNode.Parse(InvoiceJson.Write(scenario.CompanyData)),
            ["document"] = new JsonObject
            {
                ["content_sha256"] = new string('a', 64),
                ["facts"] = MatchedInvoiceScenario.RawFacts(scenario.Facts),
            },
        };

    private static JsonNode Submit(JsonObject step, string path, int expectedExit = 0)
    {
        using var input = new StringReader(step.ToJsonString());
        using var output = new StringWriter();
        Assert.Equal(expectedExit, RunnerAdapter.Run(input, output, path));
        return JsonNode.Parse(output.ToString())!;
    }

    private static void AssertConfirmed(JsonNode result, int revision)
    {
        Assert.Equal(revision, result["input_revision"]!.GetValue<int>());
        AssertOutcome(result, "clear", "D", ["L"], [], orderMatchSource: "human_confirmation");
        AssertLine(result, "L", "OL", "deterministic", "10.00", "2.00");
        AssertTotals(result, "10.00", "2.00", "12.00");
    }

    private static void AssertDuplicateOnly(JsonNode result, int revision, string context)
    {
        Assert.Equal(revision, result["input_revision"]!.GetValue<int>());
        Assert.Equal(context, Text(result, "context_version"));
        Assert.Equal("duplicate", Text(result, "import_disposition"));
        Assert.Equal("DOC-008", Text(result, "document_id"));
        Assert.Equal("ORIGINAL-008", Text(result, "original_document_id"));
        Assert.Null(result["decision"]);
        Assert.Null(result["totals"]);
        Assert.Null(result["matched_order_id"]);
        Assert.Null(result["order_match_source"]);
        Assert.Null(result["existing_assessment_ref"]);
        Assert.Empty(result["lines"]!.AsArray());
        Assert.Empty(result["checks"]!.AsArray());
        Assert.Empty(result["questions"]!.AsArray());
    }
}
