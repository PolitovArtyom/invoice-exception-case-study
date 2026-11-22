using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Validation;

using ModelContextProtocol.Protocol;

namespace InvoiceDesk.Web.Mcp;

internal static class ToolResponse
{
    public static CallToolResult Run<T>(Func<T> operation, ILogger logger)
    {
        try
        {
            return Text(InvoiceJson.Write(operation()));
        }
        catch (InvoiceValidationException error)
        {
            logger.LogInformation("Rejected MCP input ({ErrorCount} validation errors)", error.Details.Count);
            return Text(InvoiceJson.Write(error.Response), true);
        }
        catch (UnsupportedInvoiceCaseException error)
        {
            logger.LogInformation("Unsupported invoice received over MCP: {Condition}", error.Message);
            return Text(error.Message, true);
        }
    }

    private static CallToolResult Text(string text, bool isError = false) =>
        new() { IsError = isError, Content = [new TextContentBlock { Text = text }] };
}
