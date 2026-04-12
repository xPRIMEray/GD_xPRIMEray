#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

from PIL import Image
from image_compare import compare_metrics


CASES = (
    ("wormhole_clean_curved", "Clean curved film capture"),
    ("wormhole_reference_only", "Reference Reality inset only"),
    ("wormhole_reference_plus_semantic", "Reference Reality plus semantic glyph overlay"),
    ("wormhole_reference_plus_curvature", "Reference Reality plus curvature heat map overlay"),
    ("wormhole_reference_plus_collision", "Reference Reality plus collision radar overlay"),
    ("wormhole_full_stack_curvature", "Reference Reality plus semantic, collision, and curvature overlays"),
)

PERF_RE = re.compile(
    r"Film perf: px=(\d+) tpx=(\d+) fpx=(\d+) effPx=(\d+) segs=(\d+) tested=(\d+) hits=(\d+).*?"
    r"ms p1=([0-9.]+) sched=([0-9.]+) p2p=([0-9.]+) p2e=([0-9.]+) p2g=([0-9.]+) p2q=([0-9.]+) p2r=([0-9.]+)"
)
PROTO_RE = re.compile(r"proto_caustic_invariant pass=(true|false)")
BUDGET_RE = re.compile(r"low_value_sector_budget pass=(true|false)")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Compare the wormhole DualRealityTransport capture matrix against the clean curved render."
    )
    parser.add_argument("--run-root", type=Path, required=True)
    return parser.parse_args()


def parse_log_metrics(log_path: Path) -> dict:
    result = {
        "pass2_query_ms": None,
        "pass2_physics_ms": None,
        "ray_tests": None,
        "hits": None,
        "hit_rate_pct": None,
        "proto_caustic_pass": None,
        "low_value_budget_pass": None,
    }
    if not log_path.exists():
        return result

    text = log_path.read_text(encoding="utf-8", errors="replace")
    perf = PERF_RE.findall(text)
    if perf:
        last = perf[-1]
        ray_tests = int(last[5])
        hits = int(last[6])
        result["ray_tests"] = ray_tests
        result["hits"] = hits
        result["hit_rate_pct"] = (hits / ray_tests * 100.0) if ray_tests > 0 else 0.0
        result["pass2_physics_ms"] = float(last[9])
        result["pass2_query_ms"] = float(last[12])

    proto = PROTO_RE.findall(text)
    if proto:
        result["proto_caustic_pass"] = proto[-1] == "true"

    budget = BUDGET_RE.findall(text)
    if budget:
        result["low_value_budget_pass"] = budget[-1] == "true"

    return result


def analyze_case_images(run_root: Path) -> dict:
    images_dir = run_root / "images"
    logs_dir = run_root / "logs"
    baseline_path = images_dir / "wormhole_clean_curved.png"
    if not baseline_path.exists():
        raise FileNotFoundError(f"Missing clean baseline image: {baseline_path}")

    rows = []
    for case_name, description in CASES:
        image_path = images_dir / f"{case_name}.png"
        log_path = logs_dir / f"{case_name}.log"
        if not image_path.exists():
            raise FileNotFoundError(f"Missing case image: {image_path}")

        image_metrics = {
            "ssim_vs_clean": 1.0,
            "mad_vs_clean": 0.0,
            "resized_to_clean": False,
        }
        if case_name != "wormhole_clean_curved":
            compare_path = image_path
            baseline_size = Image.open(baseline_path).size
            image_size = Image.open(image_path).size
            if image_size != baseline_size:
                analysis_dir = run_root / "analysis"
                analysis_dir.mkdir(parents=True, exist_ok=True)
                resized_path = analysis_dir / f"{case_name}__resized_to_clean.png"
                resized_image = Image.open(image_path).convert("RGB").resize(baseline_size, Image.Resampling.BILINEAR)
                resized_image.save(resized_path)
                compare_path = resized_path
                image_metrics["resized_to_clean"] = True

            score, mad = compare_metrics(str(baseline_path), str(compare_path))
            image_metrics["ssim_vs_clean"] = float(score)
            image_metrics["mad_vs_clean"] = float(mad)

        row = {
            "case": case_name,
            "description": description,
            "image": str(image_path),
            **image_metrics,
            **parse_log_metrics(log_path),
        }
        rows.append(row)

    return {
        "run_root": str(run_root),
        "cases": rows,
        "reused_scripts": [
            "tools/image_compare.py",
        ],
        "notes": [
            "The clean curved render is the baseline comparator for all stacked DualRealityTransport modes.",
            "SSIM and mean absolute difference are reused from the curved_minimal comparison path.",
            "The wormhole stack differs from curved_minimal because its overlays are composited in-harness rather than through render-test capture modes.",
            "When the composed frame does not match the raw film dimensions, it is resized to the clean-film footprint before scoring.",
        ],
    }


def write_reports(run_root: Path, payload: dict) -> None:
    summary_json = run_root / "summary.json"
    summary_txt = run_root / "summary.txt"

    summary_json.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    lines = [
        "DualRealityTransport wormhole capture matrix",
        f"Run root: {run_root}",
        "",
        "Captures produced:",
    ]
    for row in payload["cases"]:
        lines.append(f"- {row['case']}: {row['description']}")
    lines.extend(
        [
            "",
            "Reused scripts:",
            *[f"- {script}" for script in payload["reused_scripts"]],
            "",
            "Image comparison summary vs clean curved:",
        ]
    )
    for row in payload["cases"]:
        lines.append(
            "- {case}: SSIM={ssim:.4f} MAD={mad:.2f} resized={resized} "
            "pass2.query={p2q} pass2.physics={p2p} proto={proto} budget={budget}".format(
                case=row["case"],
                ssim=row["ssim_vs_clean"],
                mad=row["mad_vs_clean"],
                resized="yes" if row["resized_to_clean"] else "no",
                p2q="na" if row["pass2_query_ms"] is None else f"{row['pass2_query_ms']:.2f} ms",
                p2p="na" if row["pass2_physics_ms"] is None else f"{row['pass2_physics_ms']:.2f} ms",
                proto="na" if row["proto_caustic_pass"] is None else ("PASS" if row["proto_caustic_pass"] else "FAIL"),
                budget="na" if row["low_value_budget_pass"] is None else ("PASS" if row["low_value_budget_pass"] else "FAIL"),
            )
        )

    lines.extend(
        [
            "",
            "Stacked overlay interpretation:",
            "- wormhole_reference_only isolates the literal straight-path Reference Reality inset against the unchanged curved main render.",
            "- wormhole_reference_plus_semantic adds semantic field / BLV / portal glyphs for structural interpretation.",
            "- wormhole_reference_plus_curvature adds a camera-space curvature heat map derived from per-pixel turn accumulation.",
            "- wormhole_reference_plus_collision adds camera-space collision radar labels for visible collision objects.",
            "- wormhole_full_stack_curvature shows the most information-dense composition and is the primary clutter/readability stress case.",
            "",
            "Limitations / mismatches vs curved_minimal:",
            "- curved_minimal uses render-test capture modes directly; the wormhole workflow reuses in-harness composite capture paths instead.",
            "- comparison metrics score the whole composed frame, so expected HUD differences are treated as signal rather than masked out.",
        ]
    )
    summary_txt.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    args = parse_args()
    payload = analyze_case_images(args.run_root)
    write_reports(args.run_root, payload)
    print(f"[wormhole_dual_reality_analysis] summary_saved path={args.run_root / 'summary.txt'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
