using InvoiceDesk.Core.Rules.Normalization;

namespace InvoiceDesk.Tests.Rules;

public sealed class InvoiceTextTests
{
    [Fact]
    public void Should_match_names_when_case_folding_and_whitespace_normalization_make_them_equal()
    {
        Assert.True(InvoiceText.SameName("  Straße\t  Berlin  ", "STRASSE BERLIN"));
    }

    [Fact]
    public void Should_match_names_when_one_contains_a_ligature()
    {
        Assert.True(InvoiceText.SameName("ﬁle", "FILE"));
    }
}
