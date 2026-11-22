# Invoice Desk delivery plan

| Stage | Scenario and scope | Public cases required |
| --- | --- | --- |
| 00 Foundation | Clean invoices through MCP, saved results, workspace and native adapter. | 001 |
| 01 Matched invoices with discrepancies | Quantities, pricing, arithmetic, bank, and line comparison views. | 001–005, 007 |
| 02 Parties and re-imports | Party rules, duplicates and conflicts, input errors, and dispute details. | 001–009 |
| 03 Unknown purchase order | Deterministic matching, agent proposals, and human confirmation. | All ten |
| 04 Company data updates | Re-check from operator snapshots, case 101, stage tabs and simulated handover. | All ten |
| 05 Demonstrations and packaging | Required agent demonstrations, retained evidence and portable adapter builds. | All ten |
| 06 Human resolution | Acceptance, reading correction and scenario 102. | All ten |
| 07 Final delivery | Clean-clone verification, rule-change rehearsal and submission. | All ten |

Stage 03 completes `core-v1` through all ten public cases and twelve steps. Cases 004 and 010 start with company data and PDF references for the agent. The workspace shows the proposal mapping, independent checks after a hypothetical confirmation, and the saved audit trail. The adapter applies context, invoice, proposals and confirmations as one revision. Stage 04 delivers update-file pickup from the page and MCP, case 101 and a current-stage list. The page shows prior assessments and the full snapshots behind them. Supplier-issued corrected invoice scenario 103 remains part of the simulated prototype.

| Rule or lifecycle | API coverage |
| --- | --- |
| H-13–H-18 | [OrderMatchingTests](../tests/InvoiceDesk.Tests/Api/OrderMatchingTests.cs) covers an unknown reference, one or several complete candidates, absent supplier or buyer information and a closed order. |
| H-19, H-24/H-25, H-42 | [ProposalAssessmentTests](../tests/InvoiceDesk.Tests/Api/ProposalAssessmentTests.cs) covers a proposed order, unit and receipt precedence, and independent violations that remain after confirmation. |
| H-20–H-23 | [LineMatchingTests](../tests/InvoiceDesk.Tests/Api/LineMatchingTests.cs) covers absent exact links, alias and SKU matches, multiple exact targets and contested lines. |
| H-35 negative PO/SKU | [OrderMatchingTests](../tests/InvoiceDesk.Tests/Api/OrderMatchingTests.cs) and [LineMatchingTests](../tests/InvoiceDesk.Tests/Api/LineMatchingTests.cs) verify that separators and leading zeros affect identifier equality. |
| H-38/H-39 | [ProposalValidationTests](../tests/InvoiceDesk.Tests/Api/ProposalValidationTests.cs) validates actor, references, one-to-one links and contradictions. [AdapterProposalTests](../tests/InvoiceDesk.Tests/Scenarios/AdapterProposalTests.cs) exercises the runner actor schema and transaction boundaries. |
| Case 010 and revision history | [PublicProposalLifecycleTests](../tests/InvoiceDesk.Tests/Api/PublicProposalLifecycleTests.cs) covers the MCP and page lifecycle through restart. [ProposalRevisionTests](../tests/InvoiceDesk.Tests/Api/ProposalRevisionTests.cs) covers exact previews, persisted history, repeats and proposal precedence. |
| H-41 and case 101 | [CompanyDataRecheckTests](../tests/InvoiceDesk.Tests/Api/CompanyDataRecheckTests.cs) covers full replacement, unavailable receipts, deleted records, parsed equality and invalid updates. [LateReceiptTests](../tests/InvoiceDesk.Tests/Api/LateReceiptTests.cs) covers page and MCP re-check, stage counts and capacity. |
| A-15/A-16 | [InvoiceDuplicateTests](../tests/InvoiceDesk.Tests/Api/InvoiceDuplicateTests.cs) covers original-record priority. [MatchingContextTests](../tests/InvoiceDesk.Tests/Api/MatchingContextTests.cs) covers line-ID collisions and orphaned receipts. |

Every stage builds and runs the accumulated API and runner gates. GitHub Actions retains the public runner report. Workspace QA and reviews precede integration.

### Rule scenario index

Parameterized tests enumerate their variants alongside the named test.

| Scenario | Test |
| --- | --- |
| H-01 | [`Should_block_currency_when_invoice_currency_differs`](../tests/InvoiceDesk.Tests/Api/InvoicePartyTests.cs) |
| H-02 | [`Should_block_buyer_when_document_identifies_another_buyer`](../tests/InvoiceDesk.Tests/Api/InvoicePartyTests.cs) |
| H-03 | [`Should_resolve_buyer_when_missing_registration_has_a_unique_name_or_alias`](../tests/InvoiceDesk.Tests/Api/InvoicePartyTests.cs) |
| H-04 | [`Should_ask_for_buyer_when_name_is_absent_from_directory_or_ambiguous`](../tests/InvoiceDesk.Tests/Api/InvoicePartyTests.cs) |
| H-05 | [`Should_block_buyer_when_present_registration_is_unknown`](../tests/InvoiceDesk.Tests/Api/InvoicePartyTests.cs) |
| H-06 | [`Should_ask_for_supplier_when_registration_is_missing`](../tests/InvoiceDesk.Tests/Api/InvoicePartyTests.cs) |
| H-07 | [`Should_block_supplier_when_registration_is_unknown`](../tests/InvoiceDesk.Tests/Api/InvoicePartyTests.cs) |
| H-08 | [`Should_ask_for_currency_when_it_is_missing`](../tests/InvoiceDesk.Tests/Api/InvoicePartyTests.cs) |
| H-09 | [`Should_ask_for_bank_account_when_iban_is_missing`](../tests/InvoiceDesk.Tests/Api/InvoiceCalculationTests.cs) |
| H-10 | [`Should_ask_for_both_facts_when_supplier_registration_and_iban_are_missing`](../tests/InvoiceDesk.Tests/Api/InvoicePartyTests.cs) |
| H-11 | [`Should_keep_received_values_null_when_receipt_data_is_unavailable`](../tests/InvoiceDesk.Tests/Api/InvoiceQuantityTests.cs)<br>[`Should_ask_once_per_line_when_receipt_data_is_unavailable_for_two_lines`](../tests/InvoiceDesk.Tests/Api/InvoiceQuantityTests.cs) |
| H-12 | [`Should_block_received_quantity_when_available_receipts_are_empty`](../tests/InvoiceDesk.Tests/Api/InvoiceQuantityTests.cs) |
| H-13 | [`Should_ask_for_order_when_explicit_reference_is_unknown`](../tests/InvoiceDesk.Tests/Api/OrderMatchingTests.cs) |
| H-14 | [`Should_select_order_when_one_open_order_matches_every_line`](../tests/InvoiceDesk.Tests/Api/OrderMatchingTests.cs) |
| H-15 | [`Should_ask_for_order_when_multiple_orders_fully_match`](../tests/InvoiceDesk.Tests/Api/OrderMatchingTests.cs) |
| H-16 | [`Should_depend_on_supplier_when_reference_and_registration_are_missing`](../tests/InvoiceDesk.Tests/Api/OrderMatchingTests.cs) |
| H-17 | [`Should_select_order_when_supplier_matches_and_buyer_is_unresolved`](../tests/InvoiceDesk.Tests/Api/OrderMatchingTests.cs) |
| H-18 | [`Should_select_closed_order_when_reference_is_explicit`](../tests/InvoiceDesk.Tests/Api/OrderMatchingTests.cs) |
| H-19 | [`Should_confirm_proposed_order_when_explicit_reference_is_unknown`](../tests/InvoiceDesk.Tests/Api/ProposalAssessmentTests.cs) |
| H-20 | [`Should_ask_for_line_when_no_exact_target_exists`](../tests/InvoiceDesk.Tests/Api/LineMatchingTests.cs) |
| H-21 | [`Should_match_line_when_only_explicit_alias_or_unique_sku_matches`](../tests/InvoiceDesk.Tests/Api/LineMatchingTests.cs) |
| H-22 | [`Should_leave_line_unresolved_when_multiple_exact_targets_or_keys_compete`](../tests/InvoiceDesk.Tests/Api/LineMatchingTests.cs) |
| H-23 | [`Should_leave_all_lines_unresolved_when_they_compete_for_one_order_line`](../tests/InvoiceDesk.Tests/Api/LineMatchingTests.cs)<br>[`Should_exclude_an_order_when_lines_share_the_same_target_and_no_reference_is_printed`](../tests/InvoiceDesk.Tests/Api/LineMatchingTests.cs) |
| H-24 | [`Should_keep_unit_review_when_match_is_deterministic_proposed_or_confirmed`](../tests/InvoiceDesk.Tests/Api/ProposalAssessmentTests.cs) |
| H-25 | [`Should_skip_quantity_and_price_comparison_when_units_differ_and_receipts_are_unavailable`](../tests/InvoiceDesk.Tests/Api/ProposalAssessmentTests.cs) |
| H-26 | [`Should_need_information_when_price_review_and_missing_bank_coexist`](../tests/InvoiceDesk.Tests/Api/InvoiceCalculationTests.cs)<br>[`Should_prioritize_blocking_currency_when_price_review_and_missing_bank_coexist`](../tests/InvoiceDesk.Tests/Api/InvoicePartyTests.cs) |
| H-27 | [`Should_apply_inclusive_symmetric_tolerance_when_invoice_price_changes`](../tests/InvoiceDesk.Tests/Api/InvoiceCalculationTests.cs) |
| H-28 | [`Should_round_half_up_when_net_or_tax_reaches_a_half_cent`](../tests/InvoiceDesk.Tests/Api/InvoiceCalculationTests.cs) |
| H-29 | [`Should_sum_rounded_line_taxes_when_two_small_lines_are_invoiced`](../tests/InvoiceDesk.Tests/Api/InvoiceCalculationTests.cs) |
| H-30 | [`Should_block_line_arithmetic_when_declared_net_or_tax_differs`](../tests/InvoiceDesk.Tests/Api/InvoiceCalculationTests.cs) |
| H-31 | [`Should_use_invoice_rate_when_optional_line_tax_is_missing`](../tests/InvoiceDesk.Tests/Api/InvoiceCalculationTests.cs) |
| H-32 | [`Should_block_totals_when_declared_subtotal_or_tax_total_differs`](../tests/InvoiceDesk.Tests/Api/InvoiceCalculationTests.cs) |
| H-33 | [`Should_fail_only_the_exceeded_limit_when_receipts_or_order_balance_is_insufficient`](../tests/InvoiceDesk.Tests/Api/InvoiceQuantityTests.cs) |
| H-34 | [`Should_keep_negative_balances_when_counted_quantity_exceeds_capacity`](../tests/InvoiceDesk.Tests/Api/InvoiceQuantityTests.cs) |
| H-35 | [`Should_preserve_matches_when_registration_reference_sku_name_and_iban_use_normalized_text`](../tests/InvoiceDesk.Tests/Api/InvoicePartyTests.cs)<br>[`Should_block_supplier_when_meaningful_separator_or_leading_zero_changes`](../tests/InvoiceDesk.Tests/Api/InvoicePartyTests.cs)<br>[`Should_ask_for_line_when_sku_separator_or_leading_zero_changes`](../tests/InvoiceDesk.Tests/Api/LineMatchingTests.cs)<br>[`Should_ask_for_order_when_reference_separator_or_leading_zero_changes`](../tests/InvoiceDesk.Tests/Api/OrderMatchingTests.cs) |
| H-36 | [`Should_return_duplicate_when_equal_facts_are_reimported_with_ignored_changes`](../tests/InvoiceDesk.Tests/Api/InvoiceDuplicateTests.cs)<br>[`Should_assess_conflict_when_repeated_line_multiplicity_changes`](../tests/InvoiceDesk.Tests/Api/InvoiceDuplicateTests.cs) |
| H-37 | [`Should_choose_first_equal_original_when_multiple_records_share_a_key`](../tests/InvoiceDesk.Tests/Api/InvoiceDuplicateTests.cs)<br>[`Should_preserve_original_and_assess_conflict_when_compared_facts_change`](../tests/InvoiceDesk.Tests/Api/InvoiceReimportTests.cs)<br>[`Should_exclude_same_key_history_from_quantities_when_it_causes_a_conflict`](../tests/InvoiceDesk.Tests/Api/InvoiceReimportTests.cs) |
| H-38 | [`Should_reject_without_writes_when_proposal_contradicts_deterministic_order_or_line`](../tests/InvoiceDesk.Tests/Api/ProposalValidationTests.cs) |
| H-39 | [`Should_reject_without_writes_when_proposal_actor_or_references_are_invalid`](../tests/InvoiceDesk.Tests/Api/ProposalValidationTests.cs)<br>[`Should_reject_without_writes_when_confirmation_target_or_actor_is_invalid`](../tests/InvoiceDesk.Tests/Api/ProposalValidationTests.cs)<br>[`Should_reject_actor_when_it_violates_the_runner_schema`](../tests/InvoiceDesk.Tests/Scenarios/AdapterProposalTests.cs) |
| H-40 | [`Should_reject_without_writes_when_fact_input_is_invalid`](../tests/InvoiceDesk.Tests/Api/InvoiceFactValidationTests.cs) |
| H-41 | [`Should_replace_full_context_when_new_company_data_is_rechecked`](../tests/InvoiceDesk.Tests/Api/CompanyDataRecheckTests.cs) |
| H-42 | [`Should_keep_independent_violation_when_a_person_confirms_a_proposal`](../tests/InvoiceDesk.Tests/Api/ProposalAssessmentTests.cs) |
