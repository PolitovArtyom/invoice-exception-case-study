# Live Claude Code demos

The [run command](run-claude) records stream JSON using [the strict MCP config](mcp.json), [file permissions](settings.json) and [the system prompt](system-prompt.txt). The client was Claude Code 2.1.330 and resolved `claude-sonnet-5-5` in all three print sessions. The initialization events list Read and the seven invoice MCP tools. Read is limited to the working directory, which holds only the PDF copy. There is no agent tool for shell commands, web requests, edits or page confirmation. Session persistence and slash commands are disabled.

Each session starts in its own copy of the case's PDF directory, outside the checkout. The agent receives its prompt, that PDF and MCP replies. The Compose app supplies company data, stores its state in a Docker volume and mounts the case directories read-only. Neither the runner's inputs nor its reference outputs are available to Claude.

From the repository root, choose a fresh Compose volume and prepare the PDF copies:

```bash
INVOICE_PORT=18130 docker compose -p invoice-desk-demo up --build --wait
demo_root=$(mktemp -d)
mkdir "$demo_root"/{004,010,claude}
cp dataset/cases/case-004/documents/document-001.pdf "$demo_root/004/"
cp dataset/cases/case-010/documents/document-001.pdf "$demo_root/010/"
```

Use an authenticated Claude configuration in the temporary `claude` directory. The recordings used an isolated copy of existing client authentication. To repeat the calls:

```bash
for case_id in 004 010; do
  env -u CLAUDECODE -u CLAUDE_CODE_ENTRYPOINT CLAUDE_CONFIG_DIR="$demo_root/claude" \
    submission/demos/run-claude "$demo_root/$case_id" "$case_id/prompt.txt" \
    > "$demo_root/$case_id.jsonl"
done
```

To reproduce the scripted confirmation on the case 010 page, run the [Playwright script](010/confirm.mjs) from a temporary installation:

```bash
npm install --prefix "$demo_root/browser" playwright@1.58.2
"$demo_root/browser/node_modules/.bin/playwright" install firefox
cp submission/demos/010/confirm.mjs "$demo_root/browser/"
node "$demo_root/browser/confirm.mjs" "$demo_root"
```

Then run a second stateless print session:

```bash
env -u CLAUDECODE -u CLAUDE_CODE_ENTRYPOINT CLAUDE_CONFIG_DIR="$demo_root/claude" \
  submission/demos/run-claude "$demo_root/010" 010/after-confirmation-prompt.txt \
  > "$demo_root/010-after.jsonl"
```

The published stream JSON retains the resolved model, exposed tools, tool arguments and results, and PDF payloads. It drops `rate_limit_event` records, including quota, utilization, reset times and organization overage settings. It also removes `credential_expires_at`, `apiKeySource` and provider `request_id` fields. Those fields describe the client account or identify provider requests. The temporary working directory is redacted as `<isolated-demo>`. The retained runs contain synthetic case data.

- [004 · Instruction printed inside the invoice](004/)
- [010 · Agent proposal and browser confirmation](010/)
- [Post-run extraction and assessment comparison](../extraction/)
