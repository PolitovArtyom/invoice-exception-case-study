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

Stage 03 completes `core-v1` through all ten public cases and twelve steps. Cases 004 and 010 start with company data and PDF references for the agent. The workspace shows the proposal mapping, independent checks after a hypothetical confirmation, and the saved audit trail. The adapter applies context, invoice, proposals and confirmations as one revision. Stage 04 delivers update-file pickup from the page and MCP, case 101 and a current-stage list. The page shows prior assessments and the full snapshots behind them. Stage 05 delivers the restricted [Claude Code demos](../submission/demos/), [extraction comparison](../submission/extraction/) and self-contained adapter exports. Stage 06 delivers discrepancy acceptance, evidence-adjacent correction of extracted values and the case 102 startup fixture. Supplier-issued corrected invoice scenario 103 remains part of the simulated prototype.

| Rule or lifecycle | API coverage |
| --- | --- |
| H-13–H-18 | [OrderMatchingTests](../tests/InvoiceDesk.Tests/Api/OrderMatchingTests.cs) covers an unknown reference, one or several complete candidates, absent supplier or buyer information and a closed order. |
| H-19, H-24/H-25, H-42 | [ProposalAssessmentTests](../tests/InvoiceDesk.Tests/Api/ProposalAssessmentTests.cs) covers a proposed order, unit and receipt precedence, and independent violations that remain after confirmation. |
| H-20–H-23 | [LineMatchingTests](../tests/InvoiceDesk.Tests/Api/LineMatchingTests.cs) covers absent exact links, alias and SKU matches, multiple exact targets and contested lines. |
| H-35 negative PO/SKU | [OrderMatchingTests](../tests/InvoiceDesk.Tests/Api/OrderMatchingTests.cs) and [LineMatchingTests](../tests/InvoiceDesk.Tests/Api/LineMatchingTests.cs) verify that separators and leading zeros affect identifier equality. |
| H-38/H-39 | [ProposalValidationTests](../tests/InvoiceDesk.Tests/Api/ProposalValidationTests.cs) validates actor, references, one-to-one links and contradictions. [AdapterProposalTests](../tests/InvoiceDesk.Tests/Scenarios/AdapterProposalTests.cs) exercises the runner actor schema and transaction boundaries. |
| Case 010 and revision history | [PublicProposalLifecycleTests](../tests/InvoiceDesk.Tests/Api/PublicProposalLifecycleTests.cs) covers the MCP and page lifecycle through restart. [ProposalRevisionTests](../tests/InvoiceDesk.Tests/Api/ProposalRevisionTests.cs) covers exact previews, persisted history, repeats and proposal precedence. |
| H-41 and case 101 | [CompanyDataRecheckTests](../tests/InvoiceDesk.Tests/Api/CompanyDataRecheckTests.cs) covers full replacement, unavailable receipts, deleted records, parsed equality and invalid updates. [LateReceiptTests](../tests/InvoiceDesk.Tests/Api/LateReceiptTests.cs) covers page and MCP re-check, stage counts and capacity. |
| F5/F6 and case 102 | [DiscrepancyAcceptanceTests](../tests/InvoiceDesk.Tests/Api/DiscrepancyAcceptanceTests.cs) and [FactCorrectionTests](../tests/InvoiceDesk.Tests/Api/FactCorrectionTests.cs) cover acceptance, expiry, corrections and restart. |
| A-15/A-16 | [InvoiceDuplicateTests](../tests/InvoiceDesk.Tests/Api/InvoiceDuplicateTests.cs) covers original-record priority. [MatchingContextTests](../tests/InvoiceDesk.Tests/Api/MatchingContextTests.cs) covers line-ID collisions and orphaned receipts. |

Every stage builds and runs the accumulated API and runner gates. GitHub Actions checks all five adapter exports and retains both Linux runner reports. Workspace QA and reviews precede integration.

The [rule scenario index](../submission/tests/) maps H-01 through H-42 to their named tests.
