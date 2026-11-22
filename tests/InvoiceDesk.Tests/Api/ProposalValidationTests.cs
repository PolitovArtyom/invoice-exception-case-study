using System.Net;
using System.Text.Json.Nodes;
using InvoiceDesk.Tests.Support;
using static InvoiceDesk.Tests.Api.ProposalAssessmentTests;
using static InvoiceDesk.Tests.Support.AssessmentAssertions;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;
using static InvoiceDesk.Tests.Support.ProposalCase;

namespace InvoiceDesk.Tests.Api;

public sealed class ProposalValidationTests
{
    [Theory]
    [InlineData("O2", "OL3", "/proposal/order_id", "Contradicts the deterministic order match.")]
    [InlineData(
        "O",
        "OL2",
        "/proposal/line_matches/0/order_line_id",
        "Contradicts the deterministic line match."
    )]
    [Trait("H", "H-38")]
    public async Task Should_reject_without_writes_when_proposal_contradicts_deterministic_order_or_line(
        string orderId,
        string target,
        string path,
        string message
    )
    {
        await using var test = await Start(WithAlternativeOrder());
        var before = await test.History();
        var proposal = Proposal("P", orderId, [new("L", target)]);
        var error = Payload(await Propose(test.Mcp, proposal), isError: true);

        AssertValidation(error, path, message);
        AssertSameJson(before, await test.History());
    }

    [Theory]
    [InlineData(
        """{"proposal_id":"P","actor":"human","document_id":"D","order_id":"O","line_matches":[]}""",
        "/proposal/actor",
        "agent"
    )]
    [InlineData(
        """{"proposal_id":"P","actor":"agent","document_id":"D2","order_id":"O","line_matches":[]}""",
        "/proposal/document_id",
        "active invoice"
    )]
    [InlineData(
        """{"proposal_id":"P","actor":"agent","document_id":"D","order_id":"MISSING","line_matches":[]}""",
        "/proposal/order_id",
        "Order does not exist."
    )]
    [InlineData(
        """{"proposal_id":"P","actor":"agent","document_id":"D","order_id":"O","line_matches":[{"document_line_id":"MISSING","order_line_id":"OL"}]}""",
        "/proposal/line_matches/0/document_line_id",
        "Invoice line does not exist."
    )]
    [InlineData(
        """{"proposal_id":"P","actor":"agent","document_id":"D","order_id":"O","line_matches":[{"document_line_id":"L","order_line_id":"MISSING"}]}""",
        "/proposal/line_matches/0/order_line_id",
        "does not belong"
    )]
    [InlineData(
        """{"proposal_id":"P","actor":"agent","document_id":"D","order_id":"O","line_matches":[{"document_line_id":"L","order_line_id":"OL3"}]}""",
        "/proposal/line_matches/0/order_line_id",
        "does not belong"
    )]
    [InlineData(
        """{"proposal_id":"P","actor":"agent","document_id":"D","order_id":"O","line_matches":[{"document_line_id":"L","order_line_id":"OL"},{"document_line_id":"L","order_line_id":"OL"}]}""",
        "/proposal/line_matches/1/document_line_id",
        "only once"
    )]
    [InlineData(
        """{"proposal_id":"P","actor":"agent","document_id":"D","order_id":"O","line_matches":[{"document_line_id":"L","order_line_id":"OL"},{"document_line_id":"L2","order_line_id":"OL"}]}""",
        "/proposal/line_matches/1/order_line_id",
        "only once"
    )]
    [InlineData(
        """{"proposal_id":"P","actor":"agent","document_id":"D","order_id":"O","line_matches":[{"document_line_id":"L2","order_line_id":"OL"}]}""",
        "/proposal/line_matches/0/order_line_id",
        "only once"
    )]
    [Trait("H", "H-39")]
    public async Task Should_reject_without_writes_when_proposal_actor_or_references_are_invalid(
        string json,
        string path,
        string message
    )
    {
        await using var test = await Start(WithAlternativeOrder());
        var before = await test.History();
        var error = Payload(await Propose(test.Mcp, JsonNode.Parse(json)!), isError: true);

        AssertValidation(error, path, message);
        AssertSameJson(before, await test.History());
    }

    [Theory]
    [InlineData(
        """{"proposal_id":"MISSING","expected_revision":3,"confirmed_by":"Ana","supporting_documents_reviewed":true}""",
        "/proposal_id",
        "Proposal does not exist."
    )]
    [InlineData(
        """{"proposal_id":"P","actor":"agent","expected_revision":3,"confirmed_by":"Ana","supporting_documents_reviewed":true}""",
        "/actor",
        "actor"
    )]
    [Trait("H", "H-39")]
    public async Task Should_reject_without_writes_when_confirmation_target_or_actor_is_invalid(
        string json,
        string path,
        string message
    )
    {
        await using var test = await Start(WithUnknownReference(MatchedInvoiceScenario.Baseline()));
        await test.Propose(Proposal());
        var before = await test.History();
        using var response = await Confirm(test.Page, CaseId, JsonNode.Parse(json)!);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertValidation(await Body(response), path, message);
        AssertSameJson(before, await test.History());
    }

    [Theory]
    [InlineData(
        """{"proposal_id":"P","expected_revision":3,"supporting_documents_reviewed":true}""",
        "/confirmed_by",
        "Enter a name"
    )]
    [InlineData(
        """{"proposal_id":"P","expected_revision":3,"confirmed_by":"  ","supporting_documents_reviewed":true}""",
        "/confirmed_by",
        "Enter a name"
    )]
    [InlineData(
        """{"proposal_id":"P","expected_revision":3,"confirmed_by":"Ana","supporting_documents_reviewed":false}""",
        "/supporting_documents_reviewed",
        "Acknowledge"
    )]
    [InlineData(
        """{"proposal_id":"P","expected_revision":2,"confirmed_by":"Ana","supporting_documents_reviewed":true}""",
        "/expected_revision",
        "The case changed"
    )]
    public async Task Should_reject_without_writes_when_confirmation_form_is_incomplete_or_stale(
        string json,
        string path,
        string message
    )
    {
        await using var test = await Start(WithUnknownReference(MatchedInvoiceScenario.Baseline()));
        await test.Propose(Proposal());
        var before = await test.History();
        using var response = await Confirm(test.Page, CaseId, JsonNode.Parse(json)!);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertValidation(await Body(response), path, message);
        AssertSameJson(before, await test.History());
    }

    [Theory]
    [InlineData("/api/cases/case-999/history")]
    [InlineData("/api/cases/case-999/proposals/P/confirmation-preview")]
    [InlineData(CaseUrl + "/revisions/999")]
    [InlineData(CaseUrl + "/proposals/MISSING/confirmation-preview")]
    public async Task Should_return_not_found_when_a_history_or_preview_resource_is_missing(
        string url
    )
    {
        await using var test = await Start(MatchedInvoiceScenario.Baseline());
        var before = await test.History();
        using var response = await test.Page.GetAsync(url);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertSameJson(before, await test.History());
    }

    private static MatchedInvoiceScenario WithAlternativeOrder()
    {
        var baseline = MatchedInvoiceScenario.Baseline();
        var invoiceLine = baseline.Facts.Lines[0];
        var scenario = baseline with
        {
            Facts = baseline.Facts with
            {
                Lines =
                [
                    invoiceLine,
                    invoiceLine with
                    {
                        LineId = "L2",
                        Sku = "MISSING",
                        Description = "Different",
                    },
                ],
                DeclaredSubtotal = 20.00m,
                DeclaredTaxTotal = 4.00m,
                DeclaredTotal = 24.00m,
            },
        };
        var order = baseline.CompanyData.PurchaseOrders.Orders[0];
        var unrelated = order.Lines[0] with
        {
            LineId = "OL2",
            Sku = "OTHER",
            Description = "Other",
        };
        return scenario.WithOrders(
            order with
            {
                Lines = [.. order.Lines, unrelated],
            },
            order with
            {
                OrderId = "O2",
                Reference = "OTHER",
                Lines = [unrelated with { LineId = "OL3" }],
            }
        );
    }

    internal static void AssertValidation(JsonNode response, string path, string message)
    {
        Assert.Single(response.AsObject());
        var error = response["error"]!;
        Assert.Equal("VALIDATION_ERROR", Text(error, "code"));
        Assert.Contains(error["details"]!.AsArray(), item =>
            Text(item!, "path") == path
            && Text(item!, "message")!.Contains(message, StringComparison.Ordinal)
        );
    }
}
