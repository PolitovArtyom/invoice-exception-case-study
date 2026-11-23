using System.Text.Json;
using System.Text.Json.Nodes;
using InvoiceDesk.Core.Cases;
using InvoiceDesk.Core.Cases.HumanResolution;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Validation;
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
        cases.MapGet("/{caseId}/history", (string caseId, CaseReader reader) =>
            reader.ReadHistory(caseId) is { } history ? Json(history) : Results.NotFound()
        );
        cases.MapGet(
            "/{caseId}/revisions/{number:int}",
            (string caseId, int number, CaseReader reader, CasePdfs pdfs) =>
                reader.ReadRevisionView(caseId, number) is { } view
                    ? Json(pdfs.WithAvailability(view))
                    : Results.NotFound()
        );
        cases.MapGet("/{caseId}/proposals/{proposalId}/confirmation-preview", ReadPreview);
        cases.MapPost("/{caseId}/confirmations", ConfirmProposal);
        cases.MapPost("/{caseId}/discrepancy-acceptances", AcceptDiscrepancy);
        cases.MapPost("/{caseId}/fact-corrections", CorrectFact);
        cases.MapPost("/{caseId}/rechecks", RecheckInvoice);
        cases.MapGet(
            "/{caseId}/sources/{index:int}/pdf",
            (string caseId, int index, CasePdfs pdfs) => Pdf(pdfs.FindSource(caseId, index))
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

    private static IResult RecheckInvoice(string caseId, InvoiceRecheck recheck)
    {
        try
        {
            return recheck.Recheck(caseId, RevisionActor.Human) is { } response
                ? Json(response)
                : Results.NotFound();
        }
        catch (InvoiceValidationException error)
        {
            return BadRequest(error);
        }
        catch (UnsupportedInvoiceCaseException error)
        {
            return BadRequest(new InvoiceValidationException(error.Path, error.FieldMessage));
        }
    }

    private static IResult ReadPreview(string caseId, string proposalId, CaseService service)
    {
        try
        {
            return
                service.PreviewConfirmation(caseId, Uri.UnescapeDataString(proposalId))
                    is { } preview
                ? Json(preview)
                : Results.NotFound();
        }
        catch (InvoiceValidationException error)
        {
            return BadRequest(error);
        }
    }

    private static Task<IResult> ConfirmProposal(
        string caseId,
        HttpRequest request,
        CaseReader reader,
        CaseService service,
        ILogger<Program> logger,
        CancellationToken cancellation
    ) => ReadForm<ConfirmationForm>(caseId, request, reader, cancellation, form =>
        {
            var response = service.ConfirmProposal(caseId, form);
            logger.LogInformation(
                "Confirmed proposal {ProposalId} for case {CaseId} at revision {Revision}",
                form.ProposalId,
                caseId,
                response.Result.InputRevision
            );
            return Json(response);
        });

    private static Task<IResult> AcceptDiscrepancy(
        string caseId,
        HttpRequest request,
        CaseReader reader,
        CaseService service,
        ILogger<Program> logger,
        CancellationToken cancellation
    ) => ReadForm<AcceptanceForm>(caseId, request, reader, cancellation, form =>
        {
            var acceptance = service.AcceptDiscrepancy(caseId, form);
            logger.LogInformation(
                "Accepted discrepancy for case {CaseId} at revision {Revision}",
                caseId,
                form.ExpectedRevision
            );
            return Json(acceptance);
        });

    private static Task<IResult> CorrectFact(
        string caseId,
        HttpRequest request,
        CaseReader reader,
        CaseService service,
        ILogger<Program> logger,
        CancellationToken cancellation
    ) => ReadForm<FactCorrectionForm>(caseId, request, reader, cancellation, form =>
        {
            var response = service.CorrectFact(caseId, form);
            logger.LogInformation(
                "Corrected fact {FieldPath} for case {CaseId} at revision {Revision}",
                form.FieldPath,
                caseId,
                response.Result.InputRevision
            );
            return Json(response);
        });

    private static async Task<IResult> ReadForm<T>(
        string caseId,
        HttpRequest request,
        CaseReader reader,
        CancellationToken cancellation,
        Func<T, IResult> handle
    )
    {
        if (!reader.Exists(caseId))
            return Results.NotFound();

        try
        {
            var body = await JsonNode.ParseAsync(
                request.Body,
                documentOptions: new() { AllowDuplicateProperties = false },
                cancellationToken: cancellation
            );
            return handle(IntakeValidation.Read<T>(body, ""));
        }
        catch (JsonException)
        {
            return BadRequest(new InvoiceValidationException("", "Expected a JSON action form."));
        }
        catch (InvoiceValidationException error)
        {
            return BadRequest(error);
        }
        catch (UnsupportedInvoiceCaseException error)
        {
            return BadRequest(new InvoiceValidationException(error.Path, error.FieldMessage));
        }
    }

    private static IResult BadRequest(InvoiceValidationException error) =>
        Results.Json(
            error.Response,
            InvoiceJson.Options,
            statusCode: StatusCodes.Status400BadRequest
        );

    private static IResult Pdf(string? path) =>
        path is null
            ? Results.NotFound()
            : Results.File(path, "application/pdf", enableRangeProcessing: true);

    private static IResult Json<T>(T value) => Results.Json(value, InvoiceJson.Options);
}
