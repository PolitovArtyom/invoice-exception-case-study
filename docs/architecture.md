# Invoice Desk architecture

`InvoiceDesk.Web` serves the Vue/PrimeVue workspace, JSON page API and stateless Streamable HTTP MCP at `/mcp`. `InvoiceDesk.Adapter` is a native apphost for the assignment runner. Both call `InvoiceDesk.Core` directly. Core embeds the original JSON schemas and has a pure evaluator organized by rule family. `CaseService` handles registration, assessment and reads, and `CaseRepository` stores cases in SQLite.

| Table | Stored state |
| --- | --- |
| Cases | Case ID, origin, date, available source references and current revision number. |
| Documents | Internal document ID, the revision that imported it, PDF reference/hash, reading origin and original facts with the external document ID, evidence and missing reasons. |
| CompanyDataSnapshots | Full company data with its supplied context version. |
| Revisions | Case and revision number, action time/actor, document/snapshot references and saved result. |

Facts, snapshots, source references, action and result payloads use typed JSON in SQLite text columns. A case update appends new inputs as needed, saves the result on its revision, and advances the case header in one transaction. Existing document, revision and snapshot rows are immutable. An assessment is the result stored on its case revision.

The evaluator builds a shared context and runs seven document checks and five line checks. Each returns a verdict with its comparison or the reason it is waiting. Rules use `decimal`, calendar dates and enums. The JSON boundary writes the assignment’s string formats and keeps source labels on comparisons. Invoice evidence stays with the saved facts and appears on the case page. Money rounds half-up to two places, tax uses rounded net, and invoice totals sum the calculated lines. The browser renders saved results and comparisons. An assessment is available after all applicable stage-00 passing conditions hold. An unsupported condition raises an explicit error before a result can be saved.

Web applies migrations and loads trusted first-step context and source metadata before listening. The public dataset needs a `cases` directory. Web reports and skips malformed case folders. Its fixture facts allow-list contains case 001. PDF selection is scoped to the case's declared sources and Web computes the import hash. The adapter applies the same migrations to its own state file and takes the document hash from the runner envelope.

MCP exposes opening a case, registering facts, reading company data and saved history/assessments, and returning an unchanged assessment. Stage 00 has no proposal, confirmation, acceptance, reading correction or changed-company-data action. See the [stage plan](plan.md) for delivery order.
