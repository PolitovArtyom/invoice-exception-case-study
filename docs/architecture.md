# Invoice Desk architecture

`InvoiceDesk.Web` serves the Vue/PrimeVue workspace, JSON page API and stateless Streamable HTTP MCP at `/mcp`. `InvoiceDesk.Adapter` is a native apphost for the assignment runner. Both call `InvoiceDesk.Core` directly. Core embeds the original JSON schemas, owns case updates and SQLite storage, and has a pure evaluator organized by rule family.

| Table | Stored state |
| --- | --- |
| Cases | Case ID, origin, date, policy, available source references and current revision number. |
| Documents | Internal and external document IDs, import order, PDF reference/hash, reading origin and original facts with evidence and missing reasons. |
| CompanyDataSnapshots | Full company data with its supplied context version. |
| Revisions | Case and revision number, action time/actor, document/snapshot references and saved result. |

Facts, snapshots, source references, action and result payloads use typed JSON in SQLite text columns. A case update appends new inputs as needed, saves the result on its revision, and advances the case header in one transaction. Existing document, revision and snapshot rows are immutable. An assessment is the result stored on its case revision.

The twelve checks store comparisons and evidence pointers with source labels. Calculations use C# `decimal`, which reaches the JSON contract as strings. Money rounds half-up to two places, tax uses rounded net, and invoice totals sum the calculated lines. The browser renders saved results and comparisons. An assessment is available after all applicable stage-00 passing conditions hold. An unsupported condition raises an explicit error before a result can be saved.

Web applies migrations and loads trusted first-step context and source metadata before listening. Its fixture facts allow-list contains case 001. PDF selection is scoped to the case's declared sources and Web computes the import hash. The adapter applies the same migrations to its own state file and takes the document hash from the runner envelope.

MCP exposes opening a case, registering facts, reading company data and saved history/assessments, and returning an unchanged assessment. Stage 00 has no proposal, confirmation, acceptance, reading correction or changed-company-data action. See the [stage plan](plan.md) for delivery order.
