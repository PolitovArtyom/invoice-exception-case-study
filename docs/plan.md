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

Stage 02 delivers party and currency checks, field-specific fact validation, duplicate history and conflict assessments. The page shows original-versus-current differences and PDF evidence and closes cases that contain only a resend. API tests exercise directory resolution, normalized re-imports, exact duplicate responses, validation without writes, and restart persistence. The official runner gates cases 001–009. The startup allow-list includes 001–003 and 005–009. Proposal-based resolution starts in stage 03. Supplier-issued corrected invoice scenario 103 remains part of the simulated prototype.

Every stage builds and runs the accumulated API and runner gates. GitHub Actions retains the public runner report. Workspace QA and reviews precede integration.
