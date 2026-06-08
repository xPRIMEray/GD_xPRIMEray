#!/usr/bin/env python3
"""Assign Observatory maturity scores to cataloged artifacts and named concepts."""

from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


CATALOG_PATH = Path("reports/observatory_catalog.json")
JSON_OUTPUT = Path("reports/observatory_maturity_ladder.json")
MD_OUTPUT = Path("reports/observatory_maturity_ladder.md")

MATURITY_LADDER = [
    {
        "stage": "Proposed",
        "score": 0,
        "meaning": "Named concept or intended observatory primitive; design exists but repeatable artifact evidence is not yet established.",
        "minimum_evidence": "Concept document, prompt, or architecture note.",
    },
    {
        "stage": "Experimental",
        "score": 1,
        "meaning": "Artifact exists, but the contract, inputs, or interpretation are still unstable.",
        "minimum_evidence": "At least one generated artifact or report with caveats.",
    },
    {
        "stage": "Observed",
        "score": 2,
        "meaning": "Artifact has been produced from real run data and can be inspected, but coverage or closure may be partial.",
        "minimum_evidence": "Source path plus visible artifact or report.",
    },
    {
        "stage": "Confirmed",
        "score": 3,
        "meaning": "Artifact satisfies its local validation gate for at least one run or fixture.",
        "minimum_evidence": "PASS closure/verdict or equivalent local gate.",
    },
    {
        "stage": "Characterized",
        "score": 4,
        "meaning": "Artifact has repeatable structure, documented interpretation, and known caveats.",
        "minimum_evidence": "Documented method, schema/tooling, or repeated comparable outputs.",
    },
    {
        "stage": "Canonical",
        "score": 5,
        "meaning": "Artifact is part of the stable Observatory language and can anchor other interpretations.",
        "minimum_evidence": "Promoted visitor-facing artifact, stable fixture contract, and clear non-ground-truth caveats.",
    },
]

STAGE_BY_SCORE = {entry["score"]: entry["stage"] for entry in MATURITY_LADDER}
SCORE_BY_STAGE = {entry["stage"]: entry["score"] for entry in MATURITY_LADDER}

CURATED_ASSIGNMENTS = [
    {
        "id": "artifact:observer_storyboard_demo",
        "name": "Observer Storyboard Demo",
        "kind": "Artifact",
        "stage": "Characterized",
        "source_path": "reports/observer_storyboard_demo.png",
        "basis": "Renderer-agnostic nine-panel framework has a schema, rendering tool, demo artifact, and explicit PASS/FAIL/PARTIAL/MISSING vocabulary.",
    },
    {
        "id": "artifact:query_observatory",
        "name": "Query Observatory",
        "kind": "Artifact",
        "stage": "Observed",
        "source_path": "reports/query_storyboard_v1.png",
        "basis": "A real storyboard artifact exists, but it has not yet been promoted into a stable catalog or canonical visitor-facing contract.",
    },
    {
        "id": "concept:cost_basin",
        "name": "Cost Basin",
        "kind": "Vocabulary Term",
        "stage": "Proposed",
        "source_path": "reports/cost_basin_v1.md",
        "basis": "Concept architecture is documented; no dedicated Cost Basin artifact generation or validation gate exists yet.",
    },
    {
        "id": "artifact:hermetic_storyboard_v2",
        "name": "Hermetic Storyboard",
        "kind": "Artifact",
        "stage": "Canonical",
        "source_path": "Docs/assets/observatory/hermetic_storyboard_v2.png",
        "basis": "Promoted Gallery anchor for the sealed-room fixture with closure, coverage, curvature signature, verdict, and explicit scene-contract caveats.",
    },
]


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def load_catalog(path: Path) -> list[dict[str, Any]]:
    if not path.exists():
        return []
    data = json.loads(path.read_text(encoding="utf-8"))
    if isinstance(data, list):
        return [item for item in data if isinstance(item, dict)]
    if isinstance(data, dict):
        for key in ("artifacts", "records", "items"):
            value = data.get(key)
            if isinstance(value, list):
                return [item for item in value if isinstance(item, dict)]
    return []


def stage_for_catalog_record(record: dict[str, Any]) -> tuple[str, str]:
    artifact_type = str(record.get("artifact_type") or "")
    fixture = str(record.get("fixture") or "")
    coverage = str(record.get("coverage") or "MISSING")
    closure = str(record.get("closure") or "MISSING")
    verdict = str(record.get("verdict") or "MISSING")
    source_path = str(record.get("source_path") or "")
    run_id = str(record.get("run_id") or "")

    if artifact_type == "hermetic_storyboard_v2":
        return (
            "Canonical",
            "Hermetic Storyboard v2 is the promoted sealed-room Observatory anchor with PASS closure and coverage.",
        )
    if artifact_type == "observer_storyboard":
        return (
            "Characterized",
            "Observer Storyboard has stable panel semantics, a JSON schema, rendering tool, and demo artifact.",
        )
    if artifact_type == "renderer_storyboard_v1":
        return (
            "Characterized",
            "Renderer Storyboard v1 has a documented nine-panel cost framing and PASS catalog status.",
        )
    if artifact_type == "observatory_story_reference":
        return (
            "Characterized",
            "Reference sheet defines the Observatory panel vocabulary even though it is not a fixture validation result.",
        )
    if artifact_type == "curvature_signature_ladder" and fixture == "hermetic_curved_room":
        if coverage == "PASS" and closure == "PASS" and verdict == "PASS":
            return (
                "Characterized",
                "Full-coverage hermetic curvature ladder has PASS closure/coverage and repeatable five-level sweep semantics.",
            )
        if closure == "PASS" and coverage == "PARTIAL":
            return (
                "Confirmed",
                "Curvature ladder confirms sealed closure for the run but records partial coverage.",
            )
        return (
            "Observed",
            "Curvature ladder image exists, but coverage or verdict metadata is missing for this run.",
        )
    if verdict == "PASS" and coverage == "PASS" and closure == "PASS":
        return ("Confirmed", "Catalog record reports PASS coverage, closure, and verdict.")
    if source_path and run_id != "unknown":
        return ("Observed", "Catalog record has a concrete source path but lacks a complete PASS gate.")
    return ("Experimental", "Artifact is discoverable but its evidence contract is incomplete.")


def make_catalog_entry(record: dict[str, Any], index: int) -> dict[str, Any]:
    stage, basis = stage_for_catalog_record(record)
    artifact_type = str(record.get("artifact_type") or "unknown")
    fixture = str(record.get("fixture") or "unknown")
    run_id = str(record.get("run_id") or "unknown")
    return {
        "id": f"catalog:{fixture}:{artifact_type}:{run_id}:{index:03d}",
        "name": f"{fixture} / {artifact_type} / {run_id}",
        "kind": "Artifact",
        "fixture": fixture,
        "artifact_type": artifact_type,
        "run_id": run_id,
        "stage": stage,
        "score": SCORE_BY_STAGE[stage],
        "coverage": record.get("coverage", "MISSING"),
        "closure": record.get("closure", "MISSING"),
        "verdict": record.get("verdict", "MISSING"),
        "source_path": record.get("source_path", ""),
        "basis": basis,
    }


def make_curated_entry(item: dict[str, str]) -> dict[str, Any]:
    stage = item["stage"]
    return {
        **item,
        "score": SCORE_BY_STAGE[stage],
    }


def build_payload(catalog_path: Path) -> dict[str, Any]:
    catalog = load_catalog(catalog_path)
    entries = [make_catalog_entry(record, index) for index, record in enumerate(catalog)]
    entries.extend(make_curated_entry(item) for item in CURATED_ASSIGNMENTS)
    entries.sort(key=lambda item: (-int(item["score"]), item["name"], item["id"]))

    return {
        "schema": "xprimeray.observatory_maturity_ladder.v1",
        "generated_at": utc_now(),
        "generated_from": {
            "catalog": catalog_path.as_posix(),
            "curated_sources": [
                "docs/observer_storyboard/observer_storyboard_v1.md",
                "reports/query_storyboard_v1.png",
                "reports/cost_basin_v1.md",
                "Docs/assets/observatory/hermetic_storyboard_v2.png",
            ],
        },
        "ladder": MATURITY_LADDER,
        "entries": entries,
    }


def write_markdown(payload: dict[str, Any], path: Path) -> None:
    entries = payload["entries"]
    lines = [
        "# Observatory Maturity Ladder v1",
        "",
        "The Observatory now tracks not only what exists, but how strongly each artifact should be trusted. Maturity is evidence strength, not physical truth.",
        "",
        "## Ladder",
        "",
        "| score | stage | meaning | minimum evidence |",
        "|---:|---|---|---|",
    ]
    for rung in payload["ladder"]:
        lines.append(f"| {rung['score']} | **{rung['stage']}** | {rung['meaning']} | {rung['minimum_evidence']} |")

    lines += [
        "",
        "## Anchor Assignments",
        "",
        "| artifact | maturity | score | source | basis |",
        "|---|---:|---:|---|---|",
    ]
    anchor_ids = {item["id"] for item in CURATED_ASSIGNMENTS}
    for entry in [item for item in entries if item["id"] in anchor_ids]:
        lines.append(
            f"| **{entry['name']}** | {entry['stage']} | {entry['score']} | "
            f"`{entry.get('source_path', '')}` | {entry['basis']} |"
        )

    lines += [
        "",
        "## Catalog Artifact Scores",
        "",
        "| fixture | artifact | run | maturity | score | coverage | closure | verdict | source |",
        "|---|---|---|---:|---:|---|---|---|---|",
    ]
    for entry in [item for item in entries if item["id"].startswith("catalog:")]:
        lines.append(
            f"| `{entry['fixture']}` | `{entry['artifact_type']}` | `{entry['run_id']}` | "
            f"{entry['stage']} | {entry['score']} | {entry['coverage']} | {entry['closure']} | {entry['verdict']} | "
            f"`{entry['source_path']}` |"
        )

    lines += [
        "",
        "## Reading Rule",
        "",
        "A higher score means the artifact has stronger evidence, clearer caveats, and more stable interpretation. It does not mean the artifact proves physical correctness. Canonical means it can anchor Observatory interpretation within its declared scene contract.",
        "",
    ]
    path.write_text("\n".join(lines), encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", type=Path, default=CATALOG_PATH)
    parser.add_argument("--json-output", type=Path, default=JSON_OUTPUT)
    parser.add_argument("--markdown-output", type=Path, default=MD_OUTPUT)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    payload = build_payload(args.catalog)
    args.json_output.parent.mkdir(parents=True, exist_ok=True)
    args.markdown_output.parent.mkdir(parents=True, exist_ok=True)
    args.json_output.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    write_markdown(payload, args.markdown_output)
    print(f"wrote {args.json_output} and {args.markdown_output} ({len(payload['entries'])} entries)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
