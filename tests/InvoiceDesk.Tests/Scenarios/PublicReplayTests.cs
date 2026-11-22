using System.Text.Json.Nodes;
using InvoiceDesk.Core.Validation;
using InvoiceDesk.Tests.Support;
using RunnerAdapter = InvoiceDesk.Adapter.Adapter;

namespace InvoiceDesk.Tests.Scenarios;

public sealed class PublicReplayTests
{
    [Theory]
    [InlineData("case-001", "clear")]
    [InlineData("case-002", "clear")]
    [InlineData("case-003", "blocked")]
    [InlineData("case-004", "needs_review")]
    [InlineData("case-005", "blocked")]
    [InlineData("case-007", "blocked")]
    public void Should_return_the_published_outcome_when_public_steps_are_replayed(
        string caseId,
        string decision
    )
    {
        using var state = new TemporaryDirectory();
        int previousRevision = 0;
        var outcomes = new List<(string? Decision, string? Disposition)>();
        foreach (string step in PublicCases.Steps(caseId))
        {
            using var input = File.OpenText(step);
            using var stdout = new StringWriter();
            int exitCode = RunnerAdapter.Run(input, stdout, state.Path);

            Assert.Equal(0, exitCode);
            var result = JsonNode.Parse(stdout.ToString())!;
            AssignmentSchemas.Instance.Validate("step-output", result);
            int revision = result["input_revision"]!.GetValue<int>();
            Assert.True(revision > previousRevision);
            previousRevision = revision;
            outcomes.Add(
                (
                    result["decision"]?.GetValue<string>(),
                    result["import_disposition"]?.GetValue<string>()
                )
            );
        }

        Assert.Equal((decision, "new"), Assert.Single(outcomes));
    }

    [Fact]
    public void Should_fail_without_a_verdict_when_a_rule_branch_is_unimplemented()
    {
        var step = PublicCases.ReadFirstStep();
        step["document"]!["facts"]!["purchase_order_reference"] = "UNMATCHED-PO";

        AssertRejectedWithoutRevision(step);
    }

    [Fact]
    public void Should_fail_without_a_verdict_when_an_amount_exceeds_the_supported_limit()
    {
        var step = PublicCases.ReadFirstStep();
        step["document"]!["facts"]!["lines"]![0]!["unit_net_price"] = "1000000000.01";

        AssertRejectedWithoutRevision(step);
    }

    [Fact]
    public void Should_report_only_the_receipt_path_when_a_step_contains_unavailable_receipts()
    {
        var step = PublicCases.ReadFirstStep();
        step["context"]!["goods_receipts"]!["availability"] = "unavailable";
        using var state = new TemporaryDirectory();
        using var input = new StringReader(step.ToJsonString());
        using var stdout = new StringWriter();

        Assert.Equal(2, RunnerAdapter.Run(input, stdout, state.Path));

        var error = JsonNode.Parse(stdout.ToString())!["error"]!;
        Assert.Equal("VALIDATION_ERROR", error["code"]!.GetValue<string>());
        var detail = Assert.Single(error["details"]!.AsArray())!;
        Assert.Equal("/context/goods_receipts/receipts", detail["path"]!.GetValue<string>());
    }

    [Fact]
    public void Should_ask_for_the_bank_account_when_a_runner_fact_is_null_without_a_reason()
    {
        var step = PublicCases.ReadFirstStep();
        step["document"]!["facts"]!["bank"]!["iban"] = null;
        using var state = new TemporaryDirectory();
        using var input = new StringReader(step.ToJsonString());
        using var stdout = new StringWriter();

        Assert.Equal(0, RunnerAdapter.Run(input, stdout, state.Path));

        var result = JsonNode.Parse(stdout.ToString())!;
        AssignmentSchemas.Instance.Validate("step-output", result);
        Assert.Equal("needs_information", result["decision"]!.GetValue<string>());
        var bank = Assert.Single(result["checks"]!.AsArray(), check =>
            check!["code"]!.GetValue<string>() == "BANK_ACCOUNT"
        )!;
        Assert.Equal("unknown", bank["status"]!.GetValue<string>());
        Assert.Null(bank["depends_on"]);
        var question = Assert.Single(result["questions"]!.AsArray())!;
        Assert.Equal("bank_account", question["required_fact"]!.GetValue<string>());
        Assert.Equal(
            "DOC-001",
            Assert.Single(question["target_object_ids"]!.AsArray())!.GetValue<string>()
        );
    }

    private static void AssertRejectedWithoutRevision(JsonObject step)
    {
        using var state = new TemporaryDirectory();
        using var input = new StringReader(step.ToJsonString());
        using var stdout = new StringWriter();

        Assert.Equal(1, RunnerAdapter.Run(input, stdout, state.Path));
        Assert.Equal("", stdout.ToString());

        using var supported = File.OpenText(PublicCases.Steps(PublicCases.CleanCase).Single());
        Assert.Equal(0, RunnerAdapter.Run(supported, stdout, state.Path));
        Assert.Equal(1, JsonNode.Parse(stdout.ToString())!["input_revision"]!.GetValue<int>());
    }
}
