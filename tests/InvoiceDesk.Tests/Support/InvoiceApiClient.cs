using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Mvc.Testing;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace InvoiceDesk.Tests.Support;

internal static class InvoiceApiClient
{
    public static async Task<McpClient> ConnectMcp(WebApplicationFactory<Program> application)
    {
        var http = application.CreateClient();
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(http.BaseAddress!, "mcp"),
            TransportMode = HttpTransportMode.StreamableHttp
        }, http, ownsHttpClient: true);
        return await McpClient.CreateAsync(transport);
    }

    public static ValueTask<CallToolResult> Call(McpClient client, string tool, string caseId = PublicCases.CleanCase) =>
        client.CallToolAsync(tool, new Dictionary<string, object?> { ["case_id"] = caseId });

    public static ValueTask<CallToolResult> Register(McpClient client, JsonNode facts) =>
        Register(client, facts, new { root = "public", relative_path = "cases/case-001/documents/document-001.pdf" });

    public static ValueTask<CallToolResult> Register(McpClient client, JsonNode facts, object? source) =>
        client.CallToolAsync("register_invoice", new Dictionary<string, object?>
        {
            ["case_id"] = PublicCases.CleanCase,
            ["source"] = source,
            ["facts"] = facts
        });

    public static JsonNode Payload(CallToolResult result, bool isError = false)
    {
        Assert.Equal(isError, result.IsError == true);
        return JsonNode.Parse(Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text)!;
    }

    public static async Task<JsonNode> Read(HttpClient browser, string path)
    {
        using var response = await browser.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    }

    public static void AssertSameJson(JsonNode? expected, JsonNode? actual) =>
        Assert.True(JsonNode.DeepEquals(expected, actual), $"Expected {expected}, got {actual}.");
}
