# Invoice Desk architecture

`InvoiceDesk.Web` serves the Vue/PrimeVue workspace, JSON page API and stateless Streamable HTTP MCP at `/mcp`. `InvoiceDesk.Adapter` is a native apphost for the assignment runner. Both call `InvoiceDesk.Core` directly. Core embeds the original JSON schemas and has a pure evaluator organized by rule family. `CaseService` handles registration and assessment writes. `CaseReader` shapes saved case and assessment views. `CaseRepository` stores cases in SQLite.

| Table | Stored state |
| --- | --- |
| Cases | Case ID, origin, date, available source references and current revision number. |
| Documents | Internal document ID, the revision that imported it, PDF reference/hash, reading origin and original facts with the external document ID, evidence and missing reasons. |
| CompanyDataSnapshots | Full company data with its supplied context version. |
| Revisions | Case and revision number, action time/actor, document/snapshot references, saved result and original invoice or duplicate attempt when applicable. |

Facts, snapshots, source references, action and result payloads use typed JSON in SQLite text columns. A case update appends new inputs as needed, saves the result on its revision, and advances the case header in one transaction. Existing document, revision and snapshot rows are immutable. An assessment is the result stored on its case revision.

The evaluator builds a shared context and runs seven document checks and five line checks. Each returns a verdict with its comparison or the reason it is waiting. Rules use `decimal`, calendar dates and enums. The JSON boundary writes the assignment’s string formats and keeps source labels on comparisons. Invoice evidence stays with the saved facts and appears on the case page. The browser renders saved results and comparisons with their sources. It lists failures and root unknowns and groups dependent unknowns by prerequisite. Resolver hints live in the page.

The [quantity checks](../src/InvoiceDesk.Core/Rules/Quantities) compare the invoiced quantity independently with accepted receipts and the order balance, each reduced by counted history excluding the current business key. Balances are signed. Unavailable receipts leave receipt and availability values null and produce a question for each affected line. A supplied empty receipt list gives a received quantity of zero.

The [pricing checks](../src/InvoiceDesk.Core/Rules/Pricing) use the inclusive 0.01 tolerance on the absolute unit-price difference. Arithmetic uses the invoice price and rate. Net rounds half-up to cents, tax uses the rounded net, and totals sum the rounded line amounts before exact comparisons with the declarations. The [bank check](../src/InvoiceDesk.Core/Rules/Bank) compares the invoice IBAN with the document supplier’s approved accounts.

Supplier and buyer resolution uses the document facts and directory independently from the selected order. The buyer may resolve by a unique normalized name or alias if its registration is absent. The supplier uses registration only. The evaluator gives order-dependent verdicts once matching can supply an order. Stage 03 owns non-pass order and line matching.

Classification checks supplied history, then earlier documents whose parties and number resolve to the same business key against the current company data. The first equal record wins, even if earlier same-key records differ. A duplicate linked to a local document references that document’s latest saved assessment. An import compares with earlier records even when external IDs are reused. Reassessment excludes the active document and any corresponding counted history record. Equal facts on reassessment do not create a duplicate attempt. Duplicate attempts store the submitted reading and original reference on the revision; no second document or assessment is inserted. List, case and MCP assessment reads select the most recent revision that has a saved assessment. A duplicate-only case appears in Closed.

Web applies migrations and loads trusted first-step context and source metadata before listening. The public dataset needs a `cases` directory. A malformed manifest or first step stops startup and identifies the folder and error. Both input files must use the folder’s case ID. Validated folders with existing cases reuse their saved revisions. Its fixture facts allow-list contains cases 001–003 and 005–009. PDF selection is scoped to the case's declared sources and Web computes the import hash. The page serves registered document and duplicate-attempt PDFs while their saved paths exist. Case manifests can include reference PDFs for history originals, which are selected by the stored SHA-256. The adapter applies the same migrations to its own state file and takes the document hash from the runner envelope.

MCP exposes opening a case, registering facts, reading company data and saved history/assessments, and returning an unchanged assessment. Proposal, confirmation, acceptance, reading correction and changed-company-data actions are scheduled for later stages. See the [stage plan](plan.md) for delivery order.
