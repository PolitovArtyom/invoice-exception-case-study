using InvoiceDesk.Core.Rules.Duplicates;
using InvoiceDesk.Tests.Support;

namespace InvoiceDesk.Tests.Rules;

public sealed class InvoiceContentTests
{
    [Fact]
    public void Should_compare_normalized_line_multisets_when_layout_and_non_business_fields_differ()
    {
        var facts = MatchedInvoiceScenario.Baseline().WithSecondLine().Facts;
        var current = facts with
        {
            DocumentId = "D2",
            PurchaseOrderReference = " po-baseline ",
            Bank = facts.Bank with { Iban = " de00 synthetic 0000000001 ", Bic = null },
            PaymentTerms = "Payment on receipt",
            Lines =
            [
                facts.Lines[1] with
                {
                    Sku = " tape ",
                    LineId = "A",
                    Description = "Second item",
                },
                facts.Lines[0] with
                {
                    Sku = " glove ",
                    LineId = "B",
                    Description = "First item",
                    Quantity = 1.000m,
                },
            ],
        };

        Assert.True(
            InvoiceContent.Equal(InvoiceContent.Normalize(facts), InvoiceContent.Normalize(current))
        );
    }

    [Fact]
    public void Should_detect_different_multiplicities_when_the_same_line_values_are_present()
    {
        var facts = MatchedInvoiceScenario.Baseline().WithSecondLine().Facts;
        var original = InvoiceContent.Normalize(
            facts with
            {
                Lines = [facts.Lines[0], facts.Lines[0], facts.Lines[1]],
            }
        );
        var current = InvoiceContent.Normalize(
            facts with
            {
                Lines = [facts.Lines[0], facts.Lines[1], facts.Lines[1]],
            }
        );

        Assert.False(InvoiceContent.Equal(original, current));
        var differences = InvoiceContent.LineDifferences(original, current);
        Assert.Equal("GLOVE", Assert.Single(differences.Original).Sku);
        Assert.Equal("TAPE", Assert.Single(differences.Current).Sku);
    }

    [Fact]
    public void Should_detect_a_financial_difference_when_the_unit_price_changes_by_one_cent()
    {
        var facts = MatchedInvoiceScenario.Baseline().Facts;
        var changed = facts with { Lines = [facts.Lines[0] with { UnitNetPrice = 10.01m }] };

        Assert.False(
            InvoiceContent.Equal(InvoiceContent.Normalize(facts), InvoiceContent.Normalize(changed))
        );
    }
}
