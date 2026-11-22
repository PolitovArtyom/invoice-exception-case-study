# Invoice Desk delivery plan

| Stage | Scenario and scope | Public cases required |
| --- | --- | --- |
| 00 Foundation | Clean invoices through MCP, saved results, workspace and native adapter. | 001 |
| 01 Matched invoices with discrepancies | Quantities, pricing, arithmetic, bank, and line comparison views. | 001–005, 007 |
| 02 Parties and re-imports | Party rules, duplicates and conflicts, input errors, and dispute details. | 001–009 |
| 03 Unknown purchase order | Deterministic matching, agent proposals, and human confirmation. | All ten |
| 04 Company data updates | Recheck using new snapshots and late-receipt scenario 101. | All ten |
| 05 Demonstrations and packaging | Required agent demonstrations, retained evidence and portable adapter builds. | All ten |
| 06 Human resolution | Acceptance, reading correction and scenario 102. | All ten |
| 07 Final delivery | Clean-clone verification, rule-change rehearsal and submission. | All ten |

Stage 03 completes `core-v1` through all ten public cases and twelve steps. Cases 004 and 010 start with company data and PDF references for the agent. The workspace shows the proposal mapping, independent checks after a hypothetical confirmation, and the saved audit trail. The adapter applies context, invoice, proposals and confirmations as one revision. Stage 04 adds operator update-file pickup, recheck and the late-receipt journey. Supplier-issued corrected invoice scenario 103 remains part of the simulated prototype.

| Rule or lifecycle | API coverage |
| --- | --- |
| H-13–H-18 | [OrderMatchingTests](../tests/InvoiceDesk.Tests/Api/OrderMatchingTests.cs) covers an unknown reference, one or several complete candidates, absent supplier or buyer information and a closed order. |
| H-19, H-24/H-25, H-42 | [ProposalAssessmentTests](../tests/InvoiceDesk.Tests/Api/ProposalAssessmentTests.cs) covers a proposed order, unit and receipt precedence, and independent violations that remain after confirmation. |
| H-20–H-23 | [LineMatchingTests](../tests/InvoiceDesk.Tests/Api/LineMatchingTests.cs) covers absent exact links, alias and SKU matches, multiple exact targets and contested lines. |
| H-35 negative PO/SKU | [OrderMatchingTests](../tests/InvoiceDesk.Tests/Api/OrderMatchingTests.cs) and [LineMatchingTests](../tests/InvoiceDesk.Tests/Api/LineMatchingTests.cs) verify that separators and leading zeros affect identifier equality. |
| H-38/H-39 | [ProposalValidationTests](../tests/InvoiceDesk.Tests/Api/ProposalValidationTests.cs) validates actor, references, one-to-one links and contradictions. [AdapterProposalTests](../tests/InvoiceDesk.Tests/Scenarios/AdapterProposalTests.cs) exercises the runner actor schema and transaction boundaries. |
| Case 010 and revision history | [PublicProposalLifecycleTests](../tests/InvoiceDesk.Tests/Api/PublicProposalLifecycleTests.cs) covers the MCP and page lifecycle through restart. [ProposalRevisionTests](../tests/InvoiceDesk.Tests/Api/ProposalRevisionTests.cs) covers exact previews, persisted history, repeats and proposal precedence. |
| A-15/A-16 | [InvoiceDuplicateTests](../tests/InvoiceDesk.Tests/Api/InvoiceDuplicateTests.cs) covers original-record priority. [MatchingContextTests](../tests/InvoiceDesk.Tests/Api/MatchingContextTests.cs) covers line-ID collisions and orphaned receipts. |

Every stage builds and runs the accumulated API and runner gates. GitHub Actions retains the public runner report. Workspace QA and reviews precede integration.
