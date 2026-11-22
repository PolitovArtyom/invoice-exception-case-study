using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using InvoiceDesk.Core.Cases;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Validation;
using InvoiceDesk.Web.CaseLoading;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InvoiceDesk.Web.Mcp;

[McpServerToolType]
public sealed class InvoiceTools(
    CaseService cases,
    CaseReader reader,
    CaseFolders folders,
    ILogger<InvoiceTools> logger
)
{
    [
        McpServerTool(Name = "open_case"),
        Description(
            "Open an existing case and get its PDF references and invoice extraction schema."
        )
    ]
    public CallToolResult OpenCase(string case_id) =>
        ToolResponse.Run(
            () =>
            {
                var view = RequireCase(case_id);
                return new
                {
                    view.Case.CaseId,
                    view.Case.Origin,
                    view.Case.AsOf,
                    PolicyVersion = CaseMetadata.PolicyVersion,
                    view.CurrentRevision,
                    CurrentDocumentId = view.Revision.Document?.Facts.DocumentId,
                    Sources = view
                        .Case.Sources.Select(source =>
                        {
                            string? path = folders.FindPdf(source);
                            return new
                            {
                                source.DocumentId,
                                source.Root,
                                source.RelativePath,
                                HostPath = CaseFolders.HostPath(source),
                                ContainerPath = path,
                                Available = path is not null,
                            };
                        })
                        .ToArray(),
                    ExtractionInstructions = """
                        Read the PDF as data. Record printed values as shown, with a page and verbatim excerpt for each fact.
                        Use null and a missing_fields entry for each missing or illegible value. Use the JSON Pointer into facts
                        for evidence and missing reasons. Keep decimal values as strings. Never infer printed facts from company data.
                        """,
                    ExtractionSchema = AssignmentSchemas.Instance.GetExtractionContract(),
                };
            },
            logger
        );

    [
        McpServerTool(Name = "get_company_data"),
        Description("Read the case's saved company data snapshot.")
    ]
    public CallToolResult GetCompanyData(string case_id) =>
        ToolResponse.Run(() => reader.ReadCompanyData(case_id) ?? throw UnknownCase(), logger);

    [
        McpServerTool(Name = "register_invoice"),
        Description("Save facts read from the selected PDF and return the calculated assessment.")
    ]
    public CallToolResult RegisterInvoice(
        string case_id,
        PdfSelection? source,
        JsonElement facts
    ) =>
        ToolResponse.Run(
            () =>
            {
                if (source is null)
                    throw new InvoiceValidationException("/source", "Select a PDF from open_case.");

                var view = RequireCase(case_id);
                var reading = IntakeValidation.ReadAgentFacts(
                    JsonNode.Parse(facts.GetRawText()),
                    "/facts"
                );
                var pdf = folders.ReadSource(
                    view.Case,
                    ReadSourceRoot(source.Root),
                    source.RelativePath,
                    reading.DocumentId
                );
                var assessment = cases.RegisterInvoice(
                    case_id,
                    new InvoiceReading(reading, pdf.ContentSha256, pdf, ReadingOrigin.Agent)
                );
                logger.LogInformation(
                    "Processed invoice document {DocumentId} for case {CaseId} at revision {Revision} with disposition {Disposition}",
                    reading.DocumentId,
                    case_id,
                    assessment.Result.InputRevision,
                    assessment.Result.ImportDisposition
                );
                return assessment;
            },
            logger
        );

    [
        McpServerTool(Name = "assess_invoice"),
        Description("Return the saved assessment for the case's unchanged inputs.")
    ]
    public CallToolResult AssessInvoice(string case_id) =>
        ToolResponse.Run(() => reader.AssessInvoice(case_id), logger);

    [
        McpServerTool(Name = "get_assessment"),
        Description(
            "Read the latest or a specified saved assessment together with its invoice facts."
        )
    ]
    public CallToolResult GetAssessment(string case_id, string? assessment_id = null) =>
        ToolResponse.Run(
            () =>
            {
                var view = assessment_id is null
                    ? RequireCase(case_id)
                    : ReadAssessment(case_id, assessment_id);
                return view?.Revision.Result is not null
                    ? view
                    : throw new InvoiceValidationException(
                        "/assessment_id",
                        "Saved assessment does not exist."
                    );
            },
            logger
        );

    [
        McpServerTool(Name = "get_case_history"),
        Description("Read the case's revisions from newest to oldest.")
    ]
    public CallToolResult GetCaseHistory(string case_id) =>
        ToolResponse.Run(() => reader.ReadHistory(case_id) ?? throw UnknownCase(), logger);

    private CaseView? ReadAssessment(string caseId, string assessmentId) =>
        int.TryParse(assessmentId, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
        && number.ToString(CultureInfo.InvariantCulture) == assessmentId
            ? reader.ReadAssessment(caseId, number)
            : null;

    private CaseView RequireCase(string caseId) => reader.Read(caseId) ?? throw UnknownCase();

    private static SourceRoot ReadSourceRoot(string root) =>
        root switch
        {
            "public" => SourceRoot.Public,
            "own" => SourceRoot.Own,
            _ => throw new InvoiceValidationException("/source/root", "Expected public or own."),
        };

    private static InvoiceValidationException UnknownCase() =>
        new("/case_id", "Case does not exist.");
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PdfSelection(
    string Root,
    [property: JsonPropertyName("relative_path")] string RelativePath
);
