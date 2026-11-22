using InvoiceDesk.Core.Rules.Normalization;

namespace InvoiceDesk.Tests.Rules;

public sealed class InvoiceTextTests
{
    [Fact]
    public void Should_match_names_when_full_unicode_case_folding_is_required() =>
        Assert.True(InvoiceText.SameName("  Straße   Berlin", "STRASSE BERLIN"));
}
