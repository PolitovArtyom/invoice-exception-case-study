using InvoiceDesk.Core.Rules.Normalization;

namespace InvoiceDesk.Tests.Rules;

public sealed class InvoiceTextTests
{
    [Fact]
    public void Should_match_names_when_invariant_lowercasing_and_collapsing_whitespace_are_enough()
    {
        Assert.True(InvoiceText.SameName("  Straße\t  Berlin  ", "straße berlin"));
        Assert.False(InvoiceText.SameName("Straße Berlin", "STRASSE BERLIN"));
    }
}
