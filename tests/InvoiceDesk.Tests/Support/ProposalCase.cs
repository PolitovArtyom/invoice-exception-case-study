using System.Net;
using System.Text.Json.Nodes;
using InvoiceDesk.Core.Cases;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;

namespace InvoiceDesk.Tests.Support;

internal sealed class ProposalCase : IAsyncDisposable
{
    public const string CaseId = CaseTestEnvironment.ReviewerCaseId;
    public const string CaseUrl = "/api/cases/" + CaseId;
    private readonly CaseTestEnvironment _environment = new();
    private readonly WebApplicationFactory<Program> _app;

    private ProposalCase(MatchedInvoiceScenario scenario)
    {
        _app = _environment.Start();
        Page = _app.CreateClient();
        scenario.Prepare(_environment, _app);
    }

    public HttpClient Page { get; }
    public McpClient Mcp { get; private set; } = null!;
    public JsonNode Initial { get; private set; } = null!;

    public static async Task<ProposalCase> Start(MatchedInvoiceScenario scenario)
    {
        var test = new ProposalCase(scenario);
        try
        {
            test.Mcp = await ConnectMcp(test._app);
            test.Initial = (await test.Register(scenario.Facts))["result"]!;
            return test;
        }
        catch
        {
            await test.DisposeAsync();
            throw;
        }
    }

    public async Task<JsonNode> Register(InvoiceFacts facts) =>
        Payload(
            await InvoiceApiClient.Register(
                Mcp,
                MatchedInvoiceScenario.RawFacts(facts),
                MatchedInvoiceScenario.Source()
            )
        );

    public async Task<JsonNode> Propose(JsonNode proposal) =>
        Payload(await InvoiceApiClient.Propose(Mcp, proposal))["result"]!;

    public async Task<JsonNode> Confirm(string proposalId, int revision, string name = "Ana")
    {
        using var response = await InvoiceApiClient.Confirm(
            Page,
            CaseId,
            Form(proposalId, revision, name)
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await Body(response))["result"]!;
    }

    public void WriteCompanyDataUpdate(string json) =>
        _environment.WriteCompanyDataUpdate(CaseId, json);

    public JsonNode ReplaceCompanyData(CompanyData data)
    {
        using var scope = _app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<CaseService>();
        var result = service.ReplaceCompanyData(CaseId, data)!.Result;
        return JsonNode.Parse(InvoiceJson.Write(result))!;
    }

    public static JsonObject Proposal(
        string proposalId = "P",
        string orderId = "O",
        params ProposedLineMatch[] links
    ) =>
        JsonNode
            .Parse(
                InvoiceJson.Write(
                    new StepProposal(proposalId, RevisionActor.Agent, "D", orderId, links)
                )
            )!
            .AsObject();

    public static JsonObject Form(string proposalId, int revision, string name = "Ana") =>
        new()
        {
            ["proposal_id"] = proposalId,
            ["expected_revision"] = revision,
            ["confirmed_by"] = name,
            ["supporting_documents_reviewed"] = true,
        };

    public async Task<JsonNode> History() => await Read(Page, CaseUrl + "/history");

    public async ValueTask DisposeAsync()
    {
        if (Mcp is not null)
            await Mcp.DisposeAsync();
        Page.Dispose();
        await _app.DisposeAsync();
        _environment.Dispose();
    }
}
