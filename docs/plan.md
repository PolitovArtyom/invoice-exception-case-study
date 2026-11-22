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

Stage 00 delivers the passing branches for an explicit order with uncontested lines, a real persistence and API harness, and CI for the listed runner subset. Cases outside the allow-list begin with company data and source metadata. Supplier-issued corrected invoice scenario 103 is part of the simulated prototype.

Every stage builds and runs the accumulated API and runner gates. GitHub Actions retains the public runner report. Workspace QA and reviews precede integration.
