#!/usr/bin/env python3
import json
import subprocess
import sys
from collections import Counter
from decimal import Decimal
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
from run_evals import compare, load_validator, schema_failure

DECIMAL_FIELDS = {
    "quantity", "unit_net_price", "declared_net_amount", "tax_rate",
    "declared_tax_amount", "declared_subtotal", "declared_tax_total", "declared_total",
}


def tool_response(transcript, tool):
    calls = set()
    for line in transcript.read_text().splitlines():
        event = json.loads(line)
        for block in event.get("message", {}).get("content", []):
            if block["type"] == "tool_use" and block["name"] == f"mcp__invoice__{tool}":
                calls.add(block["id"])
            if block["type"] == "tool_result" and block["tool_use_id"] in calls:
                if block.get("is_error"):
                    raise ValueError(f"{tool} returned an error in {transcript.name}")
                return json.loads(block["content"][0]["text"])
    raise ValueError(f"No {tool} response in {transcript.name}")


def leaves(value, path=""):
    if isinstance(value, dict):
        for key, entry in value.items():
            if key not in {"evidence", "missing_fields"}:
                yield from leaves(entry, f"{path}/{key}")
    elif isinstance(value, list):
        for index, entry in enumerate(value):
            yield from leaves(entry, f"{path}/{index}")
    else:
        yield path, value


def normalize(path, value):
    if value is None:
        return None
    if path.rsplit("/", 1)[-1] in DECIMAL_FIELDS:
        return Decimal(value)
    if path == "/bank/iban":
        return "".join(value.split())
    return value


def compare_facts(saved, reference):
    actual = dict(leaves(saved))
    expected = dict(leaves(reference))
    errors, formatting = [], []
    missing = object()
    for path in sorted(actual.keys() | expected.keys()):
        value = actual.get(path, missing)
        target = expected.get(path, missing)
        if value is missing or target is missing:
            errors.append({"path": path, "issue": "missing" if value is missing else "extra"})
        elif value != target:
            difference = {"path": path, "saved": value, "reference": target}
            if normalize(path, value) == normalize(path, target):
                formatting.append(difference)
            else:
                errors.append(difference)
    reasons = lambda facts: Counter(
        (entry["field_path"], entry["reason"]) for entry in facts["missing_fields"]
    )
    return {
        "scalar_fields": len(expected),
        "value_differences": errors,
        "formatting_differences": formatting,
        "missing_reasons": sum(reasons(saved).values()),
        "missing_reason_errors": {
            "missing": sorted((reasons(reference) - reasons(saved)).elements()),
            "unexpected": sorted((reasons(saved) - reasons(reference)).elements()),
        },
    }


def check_provenance(saved, reference):
    registered = dict(leaves(saved))
    cited = {entry["field_path"] for entry in saved["evidence"]}
    reference_citations = {entry["field_path"] for entry in reference["evidence"]}
    return sorted((reference_citations & registered.keys()) - cited)


def check_quotes(facts, pdf):
    errors, pages = [], {}
    for quote in facts["evidence"]:
        page = quote["page"]
        if page not in pages:
            text = subprocess.check_output(
                ["pdftotext", "-f", str(page), "-l", str(page), "-layout", str(pdf), "-"],
                text=True,
            )
            pages[page] = " ".join(text.split())
        if (quote["document_id"] != facts["document_id"]
                or " ".join(quote["excerpt"].split()) not in pages[page]):
            errors.append(quote)
    return {"quotes_checked": len(facts["evidence"]), "quotes_not_found": errors}


def compare_assessments(history, case_folder, validator, public_step_revisions):
    revisions = [item for item in reversed(history) if item["result"] is not None]
    case_definition = json.loads((case_folder / "case.json").read_text())
    steps_by_revision = dict(zip(public_step_revisions, case_definition["steps"], strict=True))
    if not steps_by_revision.keys() <= {revision["number"] for revision in revisions}:
        raise ValueError(f"Missing public comparison revisions for {case_folder.name}")

    results = []
    previous_revision = 0
    for revision in revisions:
        actual = revision["result"]
        number = revision["number"]
        assert actual["input_revision"] == number > previous_revision
        previous_revision = number
        step = steps_by_revision.get(number)
        failure = schema_failure(validator, actual)
        differences = [failure] if failure else None
        if step and failure is None:
            expected = json.loads((case_folder / step["expected"]).read_text())
            as_runner_output = {**actual, "step": step["step"]}
            differences = compare(expected, as_runner_output)
        results.append({
            "revision": number,
            "decision": actual["decision"],
            "public_step": step["step"] if step else None,
            "differences": differences,
        })
    return results


def main():
    dataset = ROOT / "dataset"
    validator = load_validator(dataset)
    result = {}
    for case, public_step_revisions in {"004": (2,), "010": (2, 4, 5)}.items():
        case_folder = dataset / "cases" / f"case-{case}"
        transcript = ROOT / "submission/demos" / case / (
            "after-confirmation.jsonl" if case == "010" else "transcript.jsonl"
        )
        history = tool_response(transcript, "get_case_history")
        latest_assessment = next(revision for revision in history if revision["result"] is not None)
        facts = latest_assessment["document"]["facts"]
        reference = json.loads((case_folder / "expected/extraction.json").read_text())
        result[case] = {
            "extraction": {
                **compare_facts(facts, reference),
                **check_quotes(facts, case_folder / "documents/document-001.pdf"),
                "missing_provenance": check_provenance(facts, reference),
            },
            "rule_outcomes": compare_assessments(history, case_folder, validator, public_step_revisions),
        }
    print(json.dumps(result, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
