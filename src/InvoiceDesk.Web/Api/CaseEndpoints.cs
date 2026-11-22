using InvoiceDesk.Core.Cases;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Web.CaseLoading;

namespace InvoiceDesk.Web.Api;

internal static class CaseEndpoints
{
    public static void MapCaseEndpoints(this WebApplication app)
    {
        var cases = app.MapGroup("/api/cases");
        cases.MapGet("", (CaseReader reader) => Json(reader.List()));
        cases.MapGet("/{caseId}", (string caseId, CaseReader reader, CasePdfs pdfs) =>
            reader.Read(caseId) is { } view ? Json(pdfs.WithAvailability(view)) : Results.NotFound()
        );
        cases.MapGet(
            "/{caseId}/documents/{reference:long}/pdf",
            (string caseId, long reference, CasePdfs pdfs) =>
                Pdf(pdfs.FindDocument(caseId, reference))
        );
        cases.MapGet(
            "/{caseId}/revisions/{number:int}/duplicate-pdf",
            (string caseId, int number, CasePdfs pdfs) => Pdf(pdfs.FindDuplicate(caseId, number))
        );
        cases.MapGet(
            "/{caseId}/revisions/{number:int}/original-pdf",
            (string caseId, int number, CasePdfs pdfs) => Pdf(pdfs.FindOriginal(caseId, number))
        );
    }

    private static IResult Pdf(string? path) =>
        path is null
            ? Results.NotFound()
            : Results.File(path, "application/pdf", enableRangeProcessing: true);

    private static IResult Json<T>(T value) => Results.Json(value, InvoiceJson.Options);
}
