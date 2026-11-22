# Invoice Desk architecture

`InvoiceDesk.Web` serves the Vue/PrimeVue workspace, JSON page API and stateless Streamable HTTP MCP at `/mcp`. `InvoiceDesk.Adapter` is a native apphost for the assignment runner. Both call `InvoiceDesk.Core` directly. Core embeds the original JSON schemas and has a pure evaluator organized by rule family. `CaseService` handles registration, assessment and reads, and `CaseRepository` stores cases in SQLite.

| Table | Stored state |
| --- | --- |
| Cases | Case ID, origin, date, available source references and current revision number. |
| Documents | Internal document ID, the revision that imported it, PDF reference/hash, reading origin and original facts with the external document ID, evidence and missing reasons. |
| CompanyDataSnapshots | Full company data with its supplied context version. |
| Revisions | Case and revision number, action time/actor, document/snapshot references and saved result. |

Facts, snapshots, source references, action and result payloads use typed JSON in SQLite text columns. A case update appends new inputs as needed, saves the result on its revision, and advances the case header in one transaction. Existing document, revision and snapshot rows are immutable. An assessment is the result stored on its case revision.

The evaluator builds a shared context and runs seven document checks and five line checks. Each returns a verdict with its comparison or the reason it is waiting. Rules use `decimal`, calendar dates and enums. The JSON boundary writes the assignment’s string formats and keeps source labels on comparisons. Invoice evidence stays with the saved facts and appears on the case page. The browser renders saved results and comparisons with their sources. It lists failures and root unknowns and groups dependent unknowns by prerequisite. Resolver hints live in the page.

The [quantity checks](../src/InvoiceDesk.Core/Rules/Quantities) compare the invoiced quantity independently with accepted receipts and the order balance, each reduced by counted history excluding the current business key. Balances are signed. Unavailable receipts leave receipt and availability values null and produce a question for each affected line. A supplied empty receipt list gives a received quantity of zero.

The [pricing checks](../src/InvoiceDesk.Core/Rules/Pricing) use the inclusive 0.01 tolerance on the absolute unit-price difference. Arithmetic uses the invoice price and rate. Net rounds half-up to cents, tax uses the rounded net, and totals sum the rounded line amounts before exact comparisons with the declarations. The [bank check](../src/InvoiceDesk.Core/Rules/Bank) compares the invoice IBAN with the document supplier’s approved accounts.

Parties, matching and re-import branches outside the delivered slice raise an explicit error before a result can be saved.

Web applies migrations and loads trusted first-step context and source metadata before listening. The public dataset needs a `cases` directory. A malformed manifest or first step stops startup and identifies the folder and error. Both input files must use the folder’s case ID. Validated folders with existing cases reuse their saved revisions. Its fixture facts allow-list contains cases 001, 002, 003, 005 and 007. PDF selection is scoped to the case's declared sources and Web computes the import hash. The adapter applies the same migrations to its own state file and takes the document hash from the runner envelope.

MCP exposes opening a case, registering facts, reading company data and saved history/assessments, and returning an unchanged assessment. Proposal, confirmation, acceptance, reading correction and changed-company-data actions are scheduled for later stages. See the [stage plan](plan.md) for delivery order.
