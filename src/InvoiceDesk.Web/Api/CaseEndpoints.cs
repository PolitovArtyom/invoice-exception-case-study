using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.UseCases;
using InvoiceDesk.Web.CaseLoading;

namespace InvoiceDesk.Web.Api;

internal static class CaseEndpoints
{
    public static void MapCaseEndpoints(this WebApplication app)
    {
        var cases = app.MapGroup("/api/cases");
        cases.MapGet("", (CaseQueries queries) => Json(queries.List()));
        cases.MapGet("/{caseId}", (string caseId, CaseQueries queries) => Found(queries.Read(caseId)));
        cases.MapGet("/{caseId}/documents/{reference:long}/pdf", ReadDocument);
    }

    private static IResult ReadDocument(string caseId, long reference, CaseQueries queries, CaseFolders folders)
    {
        var source = queries.ReadHistory(caseId)?.Select(revision => revision.Document)
            .FirstOrDefault(document => document?.Reference == reference)?.Source;
        return source is not null && folders.FindPdf(source) is string path
            ? Results.File(path, "application/pdf", enableRangeProcessing: true)
            : Results.NotFound();
    }

    private static IResult Found<T>(T? value) where T : class => value is null ? Results.NotFound() : Json(value);

    private static IResult Json<T>(T value) => Results.Json(value, InvoiceJson.Options);
}
