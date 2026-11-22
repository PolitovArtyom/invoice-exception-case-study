using InvoiceDesk.Core.Cases;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Web.CaseLoading;

namespace InvoiceDesk.Web.Api;

internal static class CaseEndpoints
{
    public static void MapCaseEndpoints(this WebApplication app)
    {
        var cases = app.MapGroup("/api/cases");
        cases.MapGet("", (CaseService service) => Json(service.List()));
        cases.MapGet("/{caseId}", (string caseId, CaseService service) =>
            Found(service.Read(caseId))
        );
        cases.MapGet("/{caseId}/documents/{reference:long}/pdf", ReadDocument);
    }

    private static IResult ReadDocument(
        string caseId,
        long reference,
        CaseService service,
        CaseFolders folders
    )
    {
        var source = service
            .ReadHistory(caseId)
            ?.Select(revision => revision.Document)
            .FirstOrDefault(document => document?.Reference == reference)
            ?.Source;
        return source is not null && folders.FindPdf(source) is string path
            ? Results.File(path, "application/pdf", enableRangeProcessing: true)
            : Results.NotFound();
    }

    private static IResult Found<T>(T? value)
        where T : class => value is null ? Results.NotFound() : Json(value);

    private static IResult Json<T>(T value) => Results.Json(value, InvoiceJson.Options);
}
