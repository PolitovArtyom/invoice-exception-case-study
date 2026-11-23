# PDF extraction comparison

The comparison runs after the agent sessions. It reads the saved facts from the latest assessment in each `get_case_history` response. It compares values to the reference extraction, looks for every submitted quote on its cited page, and lists any registered field for which the reference has a citation but the agent supplied none. Both agents cited the printed invoice header under `/type`.

| Case | Scalar fields checked | Value differences | Quotes checked | Quotes not found | Fields missing provenance |
| --- | ---: | ---: | ---: | ---: | ---: |
| 004 | 38 | 0 | 36 | 0 | 0 |
| 010 | 38 | 0 | 33 | 0 | 0 |

Decimals are compared by value. There are zero value differences after removing IBAN spaces. The IBAN differs as text in both cases because the saved readings kept the printed spaces and the reference omits them. R5 compares IBANs with spaces removed. The missing reasons also match the reference. Case 004 has none. Case 010 records an absent PO reference and two absent SKUs.

Poppler's `pdftotext -f N -l N -layout` reads each cited PDF page. The quote check collapses layout whitespace and searches for the resulting literal substring.

The separate rule comparison uses the official runner's result schema and field comparator. Live results label the envelope by application revision. The comparison maps those labels to the corresponding public step numbers and checks that the actual revisions increase. All four assessments with public counterparts matched the public rules results. Case 004 stayed `needs_review`. Case 010 records four assessments. The order-only proposal at revision 3 exposed the L01 question. That intermediate revision passes the result schema and has no public counterpart. Case 010's revisions 2, 4 and 5 correspond to its three public steps.

Install Poppler and use the runner's Python environment from the repository root:

```sh
.venv/bin/python submission/extraction/compare.py
```

[Comparison script](compare.py) · [Recorded results](results.json)
