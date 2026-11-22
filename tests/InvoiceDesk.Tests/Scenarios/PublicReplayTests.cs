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
            outcomes.Add((result["decision"]?.GetValue<string>(), result["import_disposition"]?.GetValue<string>()));
        }

        Assert.Equal(("clear", "new"), Assert.Single(outcomes));
    }

    [Fact]
    public void Should_fail_without_a_verdict_when_a_rule_branch_is_unimplemented()
    {
        using var state = new TemporaryDirectory();
        using var input = File.OpenText(PublicCases.Steps("case-004").Single());
        using var stdout = new StringWriter();

        Assert.Equal(1, RunnerAdapter.Run(input, stdout, state.Path));
        Assert.Equal("", stdout.ToString());

        using var supported = File.OpenText(PublicCases.Steps(PublicCases.CleanCase).Single());
        Assert.Equal(0, RunnerAdapter.Run(supported, stdout, state.Path));
        Assert.Equal(1, JsonNode.Parse(stdout.ToString())!["input_revision"]!.GetValue<int>());
    }
}
