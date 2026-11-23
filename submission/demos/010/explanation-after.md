**case-010 (invoice INV-2026-4110, DOC-010) is clear.** The re-check changed nothing and saved no new assessment.

**Decisions in order**
1. Revision 2 (register_invoice, assessment 2): `needs_information`. The question was which purchase order the invoice refers to. The invoice prints no PO reference.
2. Revision 3 (propose_match, `case-010-prop-1`, assessment 3): `needs_information`. The order was ORD-010, but no line matches were linked. L01 was unmatched, and the question was which order lines match the listed invoice lines.
3. Revision 4 (propose_match, `case-010-prop-2`, assessment 4): `needs_review`. This proposal linked L01 to OL-010-01. The ORDER_MATCH and LINE_MATCH checks for L01 failed with needs_review severity because nothing was confirmed yet.
4. Revision 5 (confirm_proposal, assessment 5): `clear`. All checks pass and there are no open questions.

Revision 1 was the trusted loader's `load_case` step. It produced no decision.

**Who confirmed**
Demo AP Clerk confirmed proposal `case-010-prop-2` in revision 5. The proposal was made by the agent and matched L01 to OL-010-01 on ORD-010 (PO-2026-7110). The clerk did not confirm `case-010-prop-1`, which has no line matches.

**Line-match sources in assessment 5**
- **L01 → OL-010-01:** `human_confirmation`. The invoice's "Galvanised wall plug 10mm" does not match the order's "Wall plug galv. 10 mm" on description. That is why it needed a proposal and confirmation.
- **L02 → OL-010-02:** `deterministic`. It matched on description ("Concrete repair mortar 25 kg").

The order match source is also `human_confirmation`.

**Did the re-check save another assessment?**
No. `assess_invoice` returned assessment ID 5 with `inputs_unchanged: true`. The saved assessment shows `inputs_changed: false`, and the current revision is still 5. The history has no revision 6.

The totals are EUR 410.00 net, 82.00 tax and 492.00 gross. The stage is `ready_for_accounting`.
