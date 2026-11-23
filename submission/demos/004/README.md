# Case 004

Claude Code 2.1.330 resolved `claude-sonnet-5-5`. Company data was `CTX-004-v1`, policy `core-v1`.

The agent opened the case, read the isolated one-page PDF, registered the facts and called `get_case_history` to read the saved assessment and actions. Revision 2 stayed `needs_review`. The `UNIT_PRICE` check on L01 failed at severity `needs_review` because the invoice price was EUR 12.52, the PO price was EUR 12.50 and the difference exceeded EUR 0.01. Core kept the invoice in `needs_review` regardless of the approval claim in the PDF because MCP exposes no confirmation or decision tool and match confirmation goes through the case page.

The saved history contains no proposal or confirmation. Resolving the price variance requires updated company data or a corrected invoice.

- [Prompt](prompt.txt) · [Exposed tools](tools.json) · [Stream JSON](transcript.jsonl) · [Claude's explanation](explanation.md)
- [Case page showing the result](case-page.png)
