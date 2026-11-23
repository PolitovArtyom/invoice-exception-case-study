using System.Text.Json.Nodes;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.HumanResolution;
using InvoiceDesk.Tests.Support;
using static InvoiceDesk.Tests.Support.AssessmentAssertions;
using static InvoiceDesk.Tests.Support.InvoiceApiClient;

namespace InvoiceDesk.Tests.Api;

public sealed class InvoicePartyTests
{
    [Fact]
    [Trait("H", "H-01")]
    public async Task Should_block_currency_when_invoice_currency_differs()
    {
        var scenario = MatchedInvoiceScenario.Baseline();
        var result = await (
            scenario with
            {
                Facts = scenario.Facts with { Currency = "USD" },
            }
        ).RegisterAndRead();

        AssertBaselineAmounts(result, "blocked", [new("CURRENCY_MATCH", "D", "fail", "blocked")]);
    }

    [Fact]
    [Trait("H", "H-02")]
    public async Task Should_block_buyer_when_document_identifies_another_buyer()
    {
        var scenario = MatchedInvoiceScenario.Baseline();
        scenario = scenario.WithCountedInvoice(scenario.Facts, "OTHER-BUYER");
        var buyer = scenario.CompanyData.Buyers.Buyers[0];
        var result = await (
            scenario with
            {
                Facts = scenario.Facts with { Buyer = new("Other GmbH", "OTHER-REG", "DE") },
                CompanyData = scenario.CompanyData with
                {
                    Buyers = scenario.CompanyData.Buyers with
                    {
                        Buyers =
                        [
                            buyer,
                            buyer with
                            {
                                BuyerId = "B2",
                                LegalName = "Other GmbH",
                                RegistrationId = "OTHER-REG",
                            },
                        ],
                    },
                },
            }
        ).RegisterAndRead();

        AssertOutcome(
            result,
            "blocked",
            "D",
            ["L"],
            [new("BUYER_IDENTITY", "D", "fail", "blocked")]
        );
        AssertTotals(result, "10.00", "2.00", "12.00");
        AssertLine(
            result,
            "L",
            "OL",
            "deterministic",
            "10.00",
            "2.00",
            previous: "4",
            available: "6",
            remaining: "6"
        );
        var check = Assert.Single(result["checks"]!.AsArray(), item =>
            Text(item!, "code") == "BUYER_IDENTITY"
        )!;
        Assert.Equal("OTHER-REG", Text(check["actual"]!["registration_id"]!, "value"));
        Assert.Equal("B", Text(check["expected"]!["buyer_id"]!, "value"));
        Assert.Equal("BUY-REG", Text(check["expected"]!["registration_id"]!, "value"));
        Assert.Equal("Buyer GmbH", Text(check["expected"]!["legal_name"]!, "value"));
    }

    [Theory]
    [Trait("H", "H-03")]
    [InlineData("  BUYER   GmbH ")]
    [InlineData("  TOOL   HUB ")]
    public async Task Should_resolve_buyer_when_missing_registration_has_a_unique_name_or_alias(
        string name
    )
    {
        var scenario = WithoutBuyerRegistration(name);

        AssertBaselineAmounts(await scenario.RegisterAndRead(), "clear", []);
    }

    [Theory]
    [Trait("H", "H-04")]
    [InlineData("Unlisted GmbH", false)]
    [InlineData("Tool Hub", true)]
    public async Task Should_ask_for_buyer_when_name_is_absent_from_directory_or_ambiguous(
        string name,
        bool ambiguous
    )
    {
        var scenario = WithoutBuyerRegistration(name);
        var buyers = scenario.CompanyData.Buyers;
        if (ambiguous)
        {
            scenario = scenario with
            {
                CompanyData = scenario.CompanyData with
                {
                    Buyers = buyers with
                    {
                        Buyers =
                        [
                            .. buyers.Buyers,
                            buyers.Buyers[0] with
                            {
                                BuyerId = "B2",
                                RegistrationId = "OTHER-REG",
                            },
                        ],
                    },
                },
            };
        }

        AssertBaselineAmounts(
            await scenario.RegisterAndRead(),
            "needs_information",
            [
                new("BUYER_IDENTITY", "D", "unknown"),
                new("DOCUMENT_CONFLICT", "D", "unknown", Dependency: "BUYER_IDENTITY"),
            ],
            [("buyer.registration_id", "D")]
        );
    }

    [Fact]
    [Trait("H", "H-05")]
    public async Task Should_block_buyer_when_present_registration_is_unknown()
    {
        var scenario = MatchedInvoiceScenario.Baseline();
        var result = await (
            scenario with
            {
                Facts = scenario.Facts with
                {
                    Buyer = scenario.Facts.Buyer with { RegistrationId = "UNLISTED" },
                },
            }
        ).RegisterAndRead();

        AssertBaselineAmounts(
            result,
            "blocked",
            [
                new("BUYER_IDENTITY", "D", "fail", "blocked"),
                new("DOCUMENT_CONFLICT", "D", "unknown", Dependency: "BUYER_IDENTITY"),
            ]
        );
    }

    [Fact]
    [Trait("H", "H-06")]
    public async Task Should_ask_for_supplier_when_registration_is_missing()
    {
        var scenario = WithSupplierRegistration(MatchedInvoiceScenario.Baseline(), null);
        AssertUnresolvedSupplier(await scenario.RegisterAndRead(), missing: true);
    }

    [Fact]
    [Trait("H", "H-07")]
    public async Task Should_block_supplier_when_registration_is_unknown()
    {
        var scenario = WithSupplierRegistration(MatchedInvoiceScenario.Baseline(), "UNLISTED");
        AssertUnresolvedSupplier(await scenario.RegisterAndRead(), missing: false);
    }

    [Fact]
    [Trait("H", "H-08")]
    public async Task Should_ask_for_currency_when_it_is_missing()
    {
        var scenario = MatchedInvoiceScenario.Baseline();
        var result = await (
            scenario with
            {
                Facts = scenario.Facts with
                {
                    Currency = null,
                    MissingFields = [new("/currency", MissingReason.Illegible)],
                },
            }
        ).RegisterAndRead();

        AssertBaselineAmounts(
            result,
            "needs_information",
            [new("CURRENCY_MATCH", "D", "unknown")],
            [("document.currency", "D")]
        );
    }

    [Fact]
    [Trait("H", "H-10")]
    public async Task Should_ask_for_both_facts_when_supplier_registration_and_iban_are_missing()
    {
        var scenario = WithSupplierRegistration(
            MatchedInvoiceScenario.Baseline().WithMissingIban(),
            null
        );
        AssertBaselineAmounts(
            await scenario.RegisterAndRead(),
            "needs_information",
            [
                new("SUPPLIER_IDENTITY", "D", "unknown"),
                new("DOCUMENT_CONFLICT", "D", "unknown", Dependency: "SUPPLIER_IDENTITY"),
                new("BANK_ACCOUNT", "D", "unknown"),
            ],
            [("supplier.registration_id", "D"), ("bank_account", "D")]
        );
    }

    [Fact]
    [Trait("H", "H-26")]
    public async Task Should_prioritize_blocking_currency_when_price_review_and_missing_bank_coexist()
    {
        var scenario = MatchedInvoiceScenario
            .Baseline()
            .WithLineAmounts(1m, 10.02m, 0.20m, 10.02m, 2.00m, 12.02m)
            .WithMissingIban();
        var result = await (
            scenario with
            {
                Facts = scenario.Facts with { Currency = "USD" },
            }
        ).RegisterAndRead();

        AssertOutcome(
            result,
            "blocked",
            "D",
            ["L"],
            [
                new("CURRENCY_MATCH", "D", "fail", "blocked"),
                new("UNIT_PRICE", "L", "fail", "needs_review"),
                new("BANK_ACCOUNT", "D", "unknown"),
            ],
            [("bank_account", "D")]
        );
        AssertTotals(result, "10.02", "2.00", "12.02");
        AssertLine(result, "L", "OL", "deterministic", "10.02", "2.00");
    }

    [Theory]
    [Trait("H", "H-35")]
    [InlineData("supplier")]
    [InlineData("buyer")]
    [InlineData("reference")]
    [InlineData("sku")]
    [InlineData("name")]
    [InlineData("description")]
    [InlineData("iban")]
    public async Task Should_preserve_matches_when_registration_reference_sku_name_and_iban_use_normalized_text(
        string field
    )
    {
        var scenario = MatchedInvoiceScenario.Baseline();
        if (field == "name")
        {
            scenario = WithoutBuyerRegistration("  buyer  gmbh  ");
        }
        else
        {
            var facts = scenario.Facts;
            var line = facts.Lines[0];
            scenario = scenario with
            {
                Facts = field switch
                {
                    "supplier" => facts with
                    {
                        Supplier = facts.Supplier with { RegistrationId = "  sup-reg  " },
                    },
                    "buyer" => facts with
                    {
                        Buyer = facts.Buyer with { RegistrationId = " buy-reg " },
                    },
                    "reference" => facts with { PurchaseOrderReference = "  po-baseline  " },
                    "sku" => facts with
                    {
                        Lines =
                        [
                            line with
                            {
                                Sku = " glove ",
                                Description = "Other printed description",
                            },
                        ],
                    },
                    "description" => facts with
                    {
                        Lines = [line with { Description = "  SAFETY   GLOVES  " }],
                    },
                    _ => facts with
                    {
                        Bank = facts.Bank with { Iban = " de00 synthetic 0000000001 " },
                    },
                },
            };
            if (field == "description")
            {
                var orderLine = scenario.CompanyData.PurchaseOrders.Orders[0].Lines[0];
                scenario = scenario.WithOrderLines(
                    orderLine with
                    {
                        Sku = "OTHER",
                        Description = "Safety Gloves",
                    }
                );
            }
        }

        AssertBaselineAmounts(await scenario.RegisterAndRead(), "clear", []);
    }

    [Theory]
    [Trait("H", "H-35")]
    [InlineData("SUP001")]
    [InlineData("SUP-1")]
    public async Task Should_block_supplier_when_meaningful_separator_or_leading_zero_changes(
        string registration
    )
    {
        var scenario = WithSupplierRegistration(MatchedInvoiceScenario.Baseline(), registration);
        var suppliers = scenario.CompanyData.Suppliers;
        scenario = scenario with
        {
            CompanyData = scenario.CompanyData with
            {
                Suppliers = suppliers with
                {
                    Suppliers = [suppliers.Suppliers[0] with { RegistrationId = "SUP-001" }],
                },
            },
        };

        AssertUnresolvedSupplier(await scenario.RegisterAndRead(), missing: false);
    }

    [Fact]
    public async Task Should_keep_document_supplier_bank_check_independent_when_order_supplier_differs()
    {
        using var environment = new CaseTestEnvironment();
        environment.CopyCase("case-006");
        await using var app = environment.Start();
        using var page = app.CreateClient();
        var saved = await Read(page, "/api/cases/case-006");
        var result = saved["revision"]!["result"]!;

        AssertOutcome(
            result,
            "blocked",
            "DOC-006",
            ["L01", "L02"],
            [new("SUPPLIER_IDENTITY", "DOC-006", "fail", "blocked")],
            matchedOrderId: "ORD-006"
        );
        var check = Assert.Single(result["checks"]!.AsArray(), item =>
            Text(item!, "code") == "SUPPLIER_IDENTITY"
        )!;
        Assert.Equal("DE-SYN-60002", Text(check["actual"]!["registration_id"]!, "value"));
        Assert.Equal("SUP-003", Text(check["expected"]!["supplier_id"]!, "value"));
        Assert.Equal("DE-SYN-60001", Text(check["expected"]!["registration_id"]!, "value"));
        AssertTotals(result, "734.60", "146.92", "881.52");
        AssertLine(
            result,
            "L01",
            "OL-006-01",
            "deterministic",
            "555.00",
            "111.00",
            ordered: "30",
            received: "30",
            available: "30",
            remaining: "30"
        );
        AssertLine(
            result,
            "L02",
            "OL-006-02",
            "deterministic",
            "179.60",
            "35.92",
            ordered: "4",
            received: "4",
            available: "4",
            remaining: "4"
        );
    }

    private static MatchedInvoiceScenario WithoutBuyerRegistration(string name)
    {
        var scenario = MatchedInvoiceScenario.Baseline();
        var buyers = scenario.CompanyData.Buyers;
        return scenario with
        {
            Facts = scenario.Facts with
            {
                Buyer = scenario.Facts.Buyer with { RegistrationId = null, LegalName = name },
                MissingFields = [new("/buyer/registration_id", MissingReason.NotPresentInDocument)],
            },
            CompanyData = scenario.CompanyData with
            {
                Buyers = buyers with
                {
                    Buyers = [buyers.Buyers[0] with { ExplicitAliases = ["Tool Hub"] }],
                },
            },
        };
    }

    private static MatchedInvoiceScenario WithSupplierRegistration(
        MatchedInvoiceScenario scenario,
        string? registration
    ) =>
        scenario with
        {
            Facts = scenario.Facts with
            {
                Supplier = scenario.Facts.Supplier with { RegistrationId = registration },
                MissingFields = registration is null
                    ?
                    [
                        .. scenario.Facts.MissingFields,
                        new("/supplier/registration_id", MissingReason.NotPresentInDocument),
                    ]
                    : scenario.Facts.MissingFields,
            },
        };

    private static void AssertBaselineAmounts(
        JsonNode result,
        string decision,
        ExpectedCheck[] exceptions,
        (string Fact, string ObjectId)[]? questions = null
    )
    {
        AssertOutcome(result, decision, "D", ["L"], exceptions, questions);
        AssertTotals(result, "10.00", "2.00", "12.00");
        AssertLine(result, "L", "OL", "deterministic", "10.00", "2.00");
    }

    private static void AssertUnresolvedSupplier(JsonNode result, bool missing) =>
        AssertBaselineAmounts(
            result,
            missing ? "needs_information" : "blocked",
            [
                new(
                    "SUPPLIER_IDENTITY",
                    "D",
                    missing ? "unknown" : "fail",
                    missing ? null : "blocked"
                ),
                new("DOCUMENT_CONFLICT", "D", "unknown", Dependency: "SUPPLIER_IDENTITY"),
                new("BANK_ACCOUNT", "D", "unknown", Dependency: "SUPPLIER_IDENTITY"),
            ],
            missing ? [("supplier.registration_id", "D")] : []
        );
}
