# Case 010

Both print sessions used Claude Code 2.1.330 with resolved model `claude-sonnet-5-5`, company data `CTX-010-v1` and policy `core-v1`.

1. The agent opened the case, read the isolated PDF and registered the facts. Revision 2 became `needs_information` with the order question.
2. The order question named the invoice. The agent proposed ORD-010 without line links, and revision 3 stayed `needs_information` with a line-match question naming L01.
3. The recorded second proposal links only L01 → OL-010-01. Revision 4 became `needs_review`, and the application matched L02 → OL-010-02 deterministically.
4. A scripted Playwright browser acting as the AP clerk submitted the confirmation on the case 010 page 25.6 seconds after the L01 proposal. The script used `Demo AP Clerk` and only operated the form. Revision 5 became `clear`. The page confirmation applied to the order and L01. L02 stayed deterministic.
5. In a separate print session, the agent called `assess_invoice`, `get_assessment` and `get_case_history`. Re-check returned saved assessment 5, the fourth assessment, with `inputs_unchanged: true`. It created no new revision. The [history hashes](restart-history.txt) match before and after a Compose restart.

| Session | Prompt | Exposed tools | Stream JSON | Explanation |
| --- | --- | --- | --- | --- |
| Registration and proposal | [Prompt](prompt.txt) | [List](tools-before.json) | [Transcript](before-confirmation.jsonl) | [Claude's explanation](explanation-before.md) |
| After confirmation | [Prompt](after-confirmation-prompt.txt) | [List](tools-after.json) | [Transcript](after-confirmation.jsonl) | [Claude's explanation](explanation-after.md) |

[Playwright confirmation script](confirm.mjs) · [Proposal preview and form](01-review-before-confirming.png) · [Saved confirmation and decision history](02-confirmed-history.png)
