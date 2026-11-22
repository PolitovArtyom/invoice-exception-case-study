using System.Text.Json.Nodes;
using InvoiceDesk.Core.Validation;
using InvoiceDesk.Tests.Support;
using RunnerAdapter = InvoiceDesk.Adapter.Adapter;

namespace InvoiceDesk.Tests.Scenarios;

public sealed class PublicReplayTests
{
    [Fact]
    public void Should_return_clear_and_advance_revision_when_public_clean_steps_are_replayed()
    {
        using var state = new TemporaryDirectory();
        int previousRevision = 0;
        var outcomes = new List<(string? Decision, string? Disposition)>();
        foreach (string step in PublicCases.Steps(PublicCases.CleanCase))
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

        Assert.Equal(("clear", "new"), Assert.Single(outcomes));
    }

    [Fact]
    public void Should_fail_without_a_verdict_when_a_rule_branch_is_unimplemented()
    {
        var step = PublicCases.ReadFirstStep();
        step["document"]!["facts"]!["lines"]![0]!["unit_net_price"] = "8.77";

        AssertRejectedWithoutRevision(step);
    }

    [Fact]
    public void Should_fail_without_a_verdict_when_an_amount_exceeds_the_supported_limit()
    {
        const string unitPrice = "70000000000000000000000000.01";
        const string declaredNet = "34930000000000000000000000.01";
        var step = PublicCases.ReadFirstStep();
        var facts = step["document"]!["facts"]!;
        var lines = facts["lines"]!.AsArray();
        while (lines.Count > 1)
            lines.RemoveAt(lines.Count - 1);
        facts["evidence"] = new JsonArray(
            facts["evidence"]!
                .AsArray()
                .Where(item =>
                {
                    string path = item!["field_path"]!.GetValue<string>();
                    return !path.StartsWith("/lines/", StringComparison.Ordinal)
                        || path.StartsWith("/lines/0/", StringComparison.Ordinal);
                })
                .Select(item => item!.DeepClone())
                .ToArray()
        );

        lines[0]!["quantity"] = "0.499";
        lines[0]!["unit_net_price"] = unitPrice;
        lines[0]!["tax_rate"] = "0.00";
        lines[0]!["declared_net_amount"] = declaredNet;
        lines[0]!["declared_tax_amount"] = "0.00";
        facts["declared_subtotal"] = declaredNet;
        facts["declared_tax_total"] = "0.00";
        facts["declared_total"] = declaredNet;
        step["context"]!["purchase_orders"]!["orders"]![0]!["lines"]![0]!["unit_net_price"] =
            unitPrice;

        AssertRejectedWithoutRevision(step);
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
