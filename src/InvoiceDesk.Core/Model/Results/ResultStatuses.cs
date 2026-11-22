namespace InvoiceDesk.Core.Model.Results;

public enum Decision
{
    Clear,
    NeedsInformation,
    NeedsReview,
    Blocked,
}

public enum ImportDisposition
{
    New,
    Duplicate,
    Conflict,
}

public enum MatchSource
{
    Deterministic,
    AgentProposal,
    HumanConfirmation,
}

public enum CheckStatus
{
    Pass,
    Fail,
    Unknown,
    NotApplicable,
}

public enum Severity
{
    Blocked,
    NeedsReview,
}
