using InvoiceDesk.Core.Cases;
using InvoiceDesk.Core.Model;

namespace InvoiceDesk.Web.CaseLoading;

public sealed class InvoiceRecheck(
    CaseFolders folders,
    CaseReader reader,
    CaseService cases,
    ILogger<InvoiceRecheck> logger
)
{
    public AssessmentResponse? Recheck(string caseId, RevisionActor actor)
    {
        if (!reader.Exists(caseId))
            return null;

        var response = cases.RecheckInvoice(caseId, folders.ReadCompanyDataUpdate(caseId), actor);
        if (!response.InputsUnchanged)
            logger.LogInformation(
                "Re-checked case {CaseId} at revision {Revision} with company data {ContextVersion}: {Decision}",
                caseId,
                response.Result.InputRevision,
                response.Result.ContextVersion,
                response.Result.Decision
            );

        return response;
    }
}
