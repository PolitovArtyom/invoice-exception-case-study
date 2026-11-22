using InvoiceDesk.Core.Model.Results;

namespace InvoiceDesk.Core.Rules;

internal sealed record Verdict
{
    public CheckStatus Status { get; }
    public Comparison? Comparison { get; }
    public Severity? Severity { get; }
    public RequiredFact? RequiredFact { get; }
    public CheckCode? Dependency { get; }

    private Verdict(
        CheckStatus status,
        Comparison? comparison = null,
        Severity? severity = null,
        RequiredFact? requiredFact = null,
        CheckCode? dependency = null
    )
    {
        Status = status;
        Comparison = comparison;
        Severity = severity;
        RequiredFact = requiredFact;
        Dependency = dependency;
    }

    public static Verdict Pass(Comparison comparison) => new(CheckStatus.Pass, comparison);

    public static Verdict Fail(Severity severity, Comparison comparison) =>
        new(CheckStatus.Fail, comparison, severity);

    public static Verdict MissingFact(RequiredFact requiredFact, Comparison? comparison = null) =>
        new(CheckStatus.Unknown, comparison, requiredFact: requiredFact);

    public static Verdict DependsOn(CheckCode code) => new(CheckStatus.Unknown, dependency: code);

    public static Verdict NotApplicable() => new(CheckStatus.NotApplicable);
}

internal sealed record Comparison(Operand[] Actual, Operand[] Expected);
