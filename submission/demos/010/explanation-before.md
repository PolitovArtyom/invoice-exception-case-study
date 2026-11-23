I registered the invoice facts and proposed order ORD-010 with one line link. The case now stands at **needs_review**, and I'm stopping here for the clerk to confirm on the case page.

**Registration.** I registered INV-2026-4110 from the isolated `document-001.pdf`, with page 1 evidence and a verbatim quote for each fact. `/type` cites the "INVOICE" header. The lines are L01 (wall plugs) and L02 (mortar). Three fields are marked `not_present_in_document`: the PO reference and the SKU on each line. The first assessment (#2) decided **needs_information**. It asked which purchase order the invoice refers to. The supplier, buyer, currency, totals, line arithmetic and bank account were not blocking. The supplier and buyer checks showed unknown only because no order was matched yet. The bank account IBAN is on the approved list.

**First proposal.** The invoice has no PO reference, so I proposed ORD-010 (PO-2026-7110) with no line links, since that question named no lines. I chose ORD-010 over ORD-010-B (PO-2026-7999) because only ORD-010 has accepted goods receipts, dated 2026-10-23. Both orders have identical lines, so this is a judgment call for the clerk. That assessment (#3) matched L02 to OL-010-02 by description, and every check on it passed. It then asked which order line matches L01.

**Second proposal.** I linked L01 to OL-010-01 and nothing else. The unit, quantity of 100 and price of 0.35 agree, and 100 pieces were received.

**Current result (#4).** Two checks fail, both at needs_review severity:
- **ORDER_MATCH:** the invoice prints no PO reference, and the order expects PO-2026-7110. The order link rests on my proposal and the receipts, not on the invoice.
- **LINE_MATCH for L01:** the invoice says "Galvanised wall plug 10mm" and the order says "Wall plug galv. 10 mm". There is no SKU or alias, so the system won't match them automatically. The link comes from my proposal.

Everything else passes, including quantities, unit prices, line and total arithmetic, supplier, buyer, currency and bank account. No questions remain open.

**For the clerk:** confirm or reject ORD-010 versus ORD-010-B, and the L01 link to OL-010-01.
