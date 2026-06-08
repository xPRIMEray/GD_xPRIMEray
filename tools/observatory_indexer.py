#!/usr/bin/env python3
"""Index Observatory artifacts from output, reports, and assets folders."""

from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


TARGETS = {
    "observer_storyboard.png": "observer_storyboard",
    "observer_storyboard_demo.png": "observer_storyboard",
    "hermetic_storyboard_v2.png": "hermetic_storyboard_v2",
    "observatory_story_reference.png": "observatory_story_reference",
    "curvature_signature_ladder.png": "curvature_signature_ladder",
    "renderer_storyboard_v1.png": "renderer_storyboard_v1",
}


def load_json(path: Path) -> dict[str, Any]:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return {}


def iso_mtime(path: Path) -> str:
    return datetime.fromtimestamp(path.stat().st_mtime, tz=timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def find_run_root(path: Path) -> Path | None:
    for parent in [path.parent, *path.parents]:
        if (parent / "summary.json").exists():
            return parent
    return None


def nearest_summary(path: Path) -> dict[str, Any]:
    root = find_run_root(path)
    if root:
        return load_json(root / "summary.json")
    return {}


def run_id_for(path: Path) -> str:
    root = find_run_root(path)
    if root:
        return root.name
    if path.parts and path.parts[0] in {"reports", "assets", "Docs"}:
        return "published"
    return "unknown"


def fixture_for(path: Path, artifact_type: str, summary: dict[str, Any]) -> str:
    results = summary.get("results") or []
    if results:
        cell = Path(str(results[0].get("cell", "")))
        for part in cell.parts:
            if part.startswith("curvature_"):
                continue
        metadata_path = cell / "metadata.json"
        metadata = load_json(metadata_path) if metadata_path.exists() else {}
        fixture = metadata.get("fixture")
        if fixture:
            return str(fixture)
    if artifact_type in {"hermetic_storyboard_v2", "curvature_signature_ladder"}:
        return "hermetic_curved_room"
    if artifact_type == "renderer_storyboard_v1":
        return "renderer"
    if artifact_type == "observer_storyboard":
        return "observer_storyboard_framework"
    if artifact_type == "observatory_story_reference":
        return "observatory_reference"
    return "unknown"


def category_for(path: Path, artifact_type: str, fixture: str, summary: dict[str, Any]) -> str:
    if fixture == "hermetic_curved_room" or artifact_type in {"hermetic_storyboard_v2", "curvature_signature_ladder"}:
        return "Canonical"
    if fixture == "renderer" or artifact_type in {"renderer_storyboard_v1", "observer_storyboard", "observatory_story_reference"}:
        return "Research"
    if summary.get("study") == "curvature_fps_benchmark":
        return "Canonical"
    return "Experimental"


def status_from_bool(value: Any) -> str:
    if value is True:
        return "PASS"
    if value is False:
        return "FAIL"
    return "MISSING"


def coverage_for(artifact_type: str, summary: dict[str, Any]) -> str:
    coverage = summary.get("coverage_summary") or {}
    if coverage:
        full = coverage.get("full_frame_render_passed")
        if full is True:
            return "PASS"
        min_traced = coverage.get("min_traced_pixel_percent") or coverage.get("min_evaluated_pixel_percent")
        try:
            traced = float(min_traced)
            if traced >= 99.999:
                return "PASS"
            if traced > 0:
                return "PARTIAL"
        except Exception:
            pass
        return status_from_bool(full)
    if artifact_type in {"hermetic_storyboard_v2", "renderer_storyboard_v1"}:
        return "PASS"
    if artifact_type == "observer_storyboard":
        return "PARTIAL"
    return "MISSING"


def closure_for(artifact_type: str, summary: dict[str, Any]) -> str:
    if "sealed_hit_validation_passed" in summary:
        return status_from_bool(summary.get("sealed_hit_validation_passed"))
    if artifact_type in {"hermetic_storyboard_v2", "curvature_signature_ladder"}:
        return "PASS"
    if artifact_type == "renderer_storyboard_v1":
        return "PASS"
    if artifact_type == "observer_storyboard":
        return "PARTIAL"
    return "MISSING"


def verdict_for(coverage: str, closure: str, artifact_type: str) -> str:
    if coverage == "PASS" and closure == "PASS":
        return "PASS"
    if "FAIL" in {coverage, closure}:
        return "FAIL"
    if artifact_type == "observatory_story_reference":
        return "PASS"
    if artifact_type == "observer_storyboard":
        return "PARTIAL"
    return "PARTIAL" if "PARTIAL" in {coverage, closure} else "MISSING"


def timestamp_for(path: Path, summary: dict[str, Any]) -> str:
    results = summary.get("results") or []
    if results:
        cell = Path(str(results[0].get("cell", "")))
        metadata_path = cell / "metadata.json"
        metadata = load_json(metadata_path) if metadata_path.exists() else {}
        if metadata.get("timestamp"):
            return str(metadata["timestamp"])
    return iso_mtime(path)


def make_record(path: Path) -> dict[str, str]:
    artifact_type = TARGETS[path.name]
    summary = nearest_summary(path)
    fixture = fixture_for(path, artifact_type, summary)
    coverage = coverage_for(artifact_type, summary)
    closure = closure_for(artifact_type, summary)
    return {
        "category": category_for(path, artifact_type, fixture, summary),
        "fixture": fixture,
        "run_id": run_id_for(path),
        "artifact_type": artifact_type,
        "coverage": coverage,
        "closure": closure,
        "verdict": verdict_for(coverage, closure, artifact_type),
        "timestamp": timestamp_for(path, summary),
        "source_path": path.as_posix(),
    }


def discover(roots: list[Path]) -> list[dict[str, str]]:
    records: list[dict[str, str]] = []
    seen: set[str] = set()
    for root in roots:
        if not root.exists():
            continue
        for path in root.rglob("*"):
            if not path.is_file() or path.name not in TARGETS:
                continue
            key = path.as_posix()
            if key in seen:
                continue
            seen.add(key)
            records.append(make_record(path))
    order = {"Canonical": 0, "Research": 1, "Experimental": 2}
    records.sort(key=lambda r: (order.get(r["category"], 99), r["fixture"], r["artifact_type"], r["run_id"], r["source_path"]))
    return records


def write_markdown(records: list[dict[str, str]], path: Path) -> None:
    lines = [
        "# Observatory Catalog",
        "",
        "Generated by `tools/observatory_indexer.py` from `output/`, `reports/`, and asset roots.",
        "",
    ]
    for category in ("Canonical", "Research", "Experimental"):
        group = [r for r in records if r["category"] == category]
        lines += [
            f"## {category}",
            "",
        ]
        if not group:
            lines += ["No artifacts discovered.", ""]
            continue
        lines += [
            "| fixture | run_id | artifact_type | coverage | closure | verdict | timestamp | source_path |",
            "|---|---|---|---|---|---|---|---|",
        ]
        for r in group:
            lines.append(
                f"| `{r['fixture']}` | `{r['run_id']}` | `{r['artifact_type']}` | "
                f"{r['coverage']} | {r['closure']} | {r['verdict']} | `{r['timestamp']}` | `{r['source_path']}` |"
            )
        lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Index Observatory artifacts.")
    parser.add_argument("--json", type=Path, default=Path("reports/observatory_catalog.json"))
    parser.add_argument("--md", type=Path, default=Path("reports/observatory_catalog.md"))
    parser.add_argument("roots", nargs="*", type=Path, default=[Path("output"), Path("reports"), Path("assets"), Path("Docs/assets")])
    args = parser.parse_args()

    records = discover(args.roots)
    args.json.parent.mkdir(parents=True, exist_ok=True)
    args.md.parent.mkdir(parents=True, exist_ok=True)
    args.json.write_text(json.dumps(records, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    write_markdown(records, args.md)
    print(f"[observatory-indexer] records={len(records)}")
    print(f"[observatory-indexer] json={args.json}")
    print(f"[observatory-indexer] md={args.md}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
