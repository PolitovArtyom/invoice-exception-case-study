# Working on Invoice Desk

- Core owns the model, rules, use cases and SQLite storage. Web owns MCP, page JSON and trusted file loading. Adapter calls Core without a web host. Vue displays saved assessment data.
- Use full business names. Keep files small and split namespaces when they reach roughly five to ten related files. Prefer self-documenting code. Add a terse comment only for a non-obvious invariant or constraint.
- Preserve the assignment README text, dataset, schemas, policy, business rules, evaluation contract and runner. Keep private working material outside the repository.
- Keep financial arithmetic in Core with `decimal`. Keep fixture facts behind the explicit startup allow-list. Expected outputs are runner inputs only. MCP registration is limited to facts read from a case's registered source.
- Test public paths using `Should_<expected>_when_<condition>` names. Assert business outcomes independently of the evaluator. Use the official MCP client and a fresh migrated SQLite file for API tests.
- Run `dotnet build InvoiceDesk.slnx -c Release`, `dotnet format InvoiceDesk.slnx --verify-no-changes`, `dotnet test InvoiceDesk.slnx -c Release --no-build`, the frontend checks, Compose smoke and the current public runner subset. See [README](README.md) and the [stage plan](docs/plan.md).
- For a model change, run `dotnet tool restore`, then `dotnet ef migrations add <Name> --project src/InvoiceDesk.Core --output-dir Storage/Migrations`. Check the generated migration and snapshot. Web and the adapter apply migrations when opening their databases. An interrupted SQLite migration can leave an EF migration lock that needs operator cleanup.
- Work on the requested stage branch. Commit or push only with authorization for the current task. Stage reviews and QA follow implementation and gates.
