using System.Globalization;
using InvoiceDesk.Core.Model.Results;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Rules.Matching;
using InvoiceDesk.Core.Rules.Pricing;
using InvoiceDesk.Core.Rules.Quantities;
using InvoiceDesk.Tests.Support;

namespace InvoiceDesk.Tests.Rules;

public sealed class InvoiceComparisonTests
{
    [Theory]
    [InlineData("10.01", CheckStatus.Pass, null)]
    [InlineData("9.99", CheckStatus.Pass, null)]
    [InlineData("10.02", CheckStatus.Fail, Severity.NeedsReview)]
    [InlineData("9.98", CheckStatus.Fail, Severity.NeedsReview)]
    public void Should_apply_symmetric_price_tolerance_when_the_difference_is_on_either_side(
        string price,
        CheckStatus status,
        Severity? severity
    )
    {
        var context = Context();
        var line = context.Lines[0];
        line = line with
        {
            Invoice = line.Invoice with
            {
                UnitNetPrice = decimal.Parse(price, CultureInfo.InvariantCulture),
            },
        };

        var verdict = new UnitPriceCheck().Evaluate(context, line);

        Assert.Equal(status, verdict.Status);
        Assert.Equal(severity, verdict.Severity);
    }

    [Theory]
    [InlineData("10.00", "2.00", "12.00", CheckStatus.Pass)]
    [InlineData("10.01", "2.00", "12.00", CheckStatus.Fail)]
    [InlineData("10.00", "2.01", "12.00", CheckStatus.Fail)]
    [InlineData("10.00", "2.00", "12.01", CheckStatus.Fail)]
    public void Should_require_exact_totals_when_each_declared_total_is_checked(
        string subtotal,
        string tax,
        string total,
        CheckStatus status
    )
    {
        var context = Context();
        var facts = context.Facts with
        {
            DeclaredSubtotal = decimal.Parse(subtotal, CultureInfo.InvariantCulture),
            DeclaredTaxTotal = decimal.Parse(tax, CultureInfo.InvariantCulture),
            DeclaredTotal = decimal.Parse(total, CultureInfo.InvariantCulture),
        };

        var verdict = new TotalsArithmeticCheck().Evaluate(context with { Facts = facts });

        Assert.Equal(status, verdict.Status);
        Assert.Equal(status == CheckStatus.Fail ? Severity.Blocked : null, verdict.Severity);
    }

    [Fact]
    public void Should_keep_signed_balances_when_previously_invoiced_exceeds_both_limits()
    {
        var quantities = new LineQuantities(10m, 9m, 12m);

        Assert.Equal(-3m, quantities.AvailableToInvoice);
        Assert.Equal(-2m, quantities.RemainingOnOrder);
    }

    private static CheckContext Context()
    {
        var (facts, company) = MatchedInvoiceScenario.Baseline();
        var order = company.PurchaseOrders.Orders[0];
        return new(
            facts,
            company,
            company.Suppliers.Suppliers[0],
            company.Buyers.Buyers[0],
            order,
            [
                new(
                    facts.Lines[0],
                    order.Lines[0],
                    LineMatchReason.Sku,
                    new(10.00m, 2.00m),
                    new(10m, 10m, 0m)
                ),
            ],
            null,
            null,
            new(10.00m, 2.00m, 12.00m)
        );
    }
}
