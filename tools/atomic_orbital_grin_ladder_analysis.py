#!/usr/bin/env python3
"""Summarize the V1 atomic orbital GRIN ladder."""

from __future__ import annotations

import csv
import json
import sys
from pathlib import Path


STRICT_CELLS = {"A0_straight_baseline", "A1_no_cloud_reference", "A2_static_hydrogen"}


def read_json(path: Path) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return {}


def latest_atomic_row(cell: Path) -> dict:
    path = cell / "atomic_frame_telemetry.csv"
    if not path.exists():
        return {}
    rows = list(csv.DictReader(path.open(newline="", encoding="utf-8")))
    return rows[-1] if rows else {}


def hit_csv_stats(cell: Path) -> dict:
    csvs = sorted(cell.glob("*.hit_diagnostics.csv"))
    if not csvs:
        return {}
    total = hits = misses = budget = max_steps = 0
    steps_sum = 0.0
    max_step_count = 0.0
    with csvs[0].open(newline="", encoding="utf-8") as fh:
        for row in csv.DictReader(fh):
            total += 1
            had_hit = row.get("had_hit", "0") == "1"
            if had_hit:
                hits += 1
            else:
                misses += 1
            if row.get("budget_exhausted_without_hit", "0") == "1":
                budget += 1
            if row.get("max_steps_reached", "0") == "1":
                max_steps += 1
            try:
                step_count = float(row.get("step_count", "0") or 0)
            except ValueError:
                step_count = 0.0
            steps_sum += step_count
            max_step_count = max(max_step_count, step_count)
    return {
        "total_pixels": total,
        "hit_pixels": hits,
        "miss_pixels": misses,
        "budget_exhausted_pixels": budget,
        "max_steps_reached_pixels": max_steps,
        "closure_rate": hits / total if total else 0.0,
        "mean_steps_per_pixel": steps_sum / total if total else 0.0,
        "max_steps_per_pixel": max_step_count,
    }


def fallback_stats(row: dict) -> dict:
    if not row:
        return {}
    hit = int(float(row.get("hit_pixels") or 0))
    miss = int(float(row.get("miss_pixels") or 0))
    total = hit + miss
    return {
        "total_pixels": total,
        "hit_pixels": hit,
        "miss_pixels": miss,
        "budget_exhausted_pixels": int(float(row.get("budget_exhausted_pixels") or 0)),
        "closure_rate": hit / total if total else 0.0,
        "mean_steps_per_pixel": float(row.get("mean_steps_per_pixel") or 0),
        "max_steps_per_pixel": float(row.get("max_steps_per_pixel") or 0),
    }


def evaluate(cell_id: str, stats: dict) -> tuple[str, str]:
    closure = float(stats.get("closure_rate", 0.0))
    miss = int(stats.get("miss_pixels", 0))
    budget = int(stats.get("budget_exhausted_pixels", 0))
    if cell_id in STRICT_CELLS:
        ok = closure >= 0.999 and miss == 0 and budget == 0
        reason = "strict_v1_gate" if ok else "strict_v1_gate_failed"
        return ("PASS" if ok else "FAIL", reason)
    if closure >= 0.999 and budget <= 0:
        return "PASS", "clocked_no_classified_difference"
    return "REPORT", "clocked_requires_classification"


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: atomic_orbital_grin_ladder_analysis.py <output_dir>", file=sys.stderr)
        return 2
    root = Path(sys.argv[1])
    cells = sorted(p for p in root.glob("cells/A*") if p.is_dir())
    rows = []
    for cell in cells:
        meta = read_json(cell / "metadata.json")
        atomic = latest_atomic_row(cell)
        stats = hit_csv_stats(cell) or fallback_stats(atomic)
        verdict, reason = evaluate(cell.name, stats)
        rows.append((cell, meta, atomic, stats, verdict, reason))

    report = root / "atomic_orbital_grin_ladder_report.md"
    lines = [
        "# Atomic Orbital GRIN V1 Ladder Report",
        "",
        "| cell | verdict | closure_rate | miss_pixels | budget_exhausted_pixels | mean_steps | max_steps | reason |",
        "| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |",
    ]
    summary = []
    for cell, _meta, _atomic, stats, verdict, reason in rows:
        closure = float(stats.get("closure_rate", 0.0))
        miss = int(stats.get("miss_pixels", 0))
        budget = int(stats.get("budget_exhausted_pixels", 0))
        mean_steps = float(stats.get("mean_steps_per_pixel", 0.0))
        max_steps = float(stats.get("max_steps_per_pixel", 0.0))
        lines.append(
            f"| `{cell.name}` | {verdict} | {closure:.6f} | {miss} | {budget} | "
            f"{mean_steps:.3f} | {max_steps:.0f} | {reason} |"
        )
        summary.append(
            {
                "cell": cell.name,
                "verdict": verdict,
                "reason": reason,
                **stats,
            }
        )
    lines.append("")
    lines.append("V1 strict gates apply to A0-A2. A3 may report classified differences.")
    report.write_text("\n".join(lines) + "\n", encoding="utf-8")
    (root / "atomic_orbital_grin_ladder_summary.json").write_text(
        json.dumps(summary, indent=2, sort_keys=True),
        encoding="utf-8",
    )
    print(f"[atomic-ladder-analysis] report={report}")
    return 0 if all(item[4] != "FAIL" for item in rows) else 1


if __name__ == "__main__":
    raise SystemExit(main())
