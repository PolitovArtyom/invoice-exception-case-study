using System.Text.Json.Serialization;
using InvoiceDesk.Core.Model.CompanyData;
using InvoiceDesk.Core.Model.Proposals;
using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Cases;

public sealed record ProposalView(
    RegisteredProposal Registration,
    PurchaseOrder? Order,
    string? SupplierName,
    bool Applicable,
    bool CanPreview,
    bool CanConfirm
);

public sealed record ConfirmationPreview(
    int ExpectedRevision,
    string ProposalId,
    InvoiceResult Result
);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConfirmationForm(
    string ProposalId,
    int ExpectedRevision,
    string? ConfirmedBy,
    bool SupportingDocumentsReviewed
);
