#!/usr/bin/env python3
"""Analyze focused corner transport probe CSV artifacts."""

from __future__ import annotations

import argparse
import csv
import json
import math
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable

try:
    from PIL import Image, ImageDraw
except Exception as exc:
    raise SystemExit(f"Pillow is required for PNG output: {exc}") from exc


def discover_inputs(paths: Iterable[Path]) -> list[Path]:
    found: list[Path] = []
    for path in paths:
        if path.is_dir():
            found.extend(sorted(path.glob("*.corner_transport_probe.csv")))
            if not found:
                found.extend(sorted(path.glob("*.reference_geodesic_probe.csv")))
        elif path.is_file():
            found.append(path)
    return found


def f(value: Any, default: float = math.nan) -> float:
    try:
        return float(str(value).strip())
    except Exception:
        return default


def i(value: Any, default: int = 0) -> int:
    try:
        return int(float(str(value).strip()))
    except Exception:
        return default


def b(value: Any) -> bool:
    return str(value).strip().lower() in {"1", "true", "yes", "on"}


def fmt(value: float, digits: int = 6) -> str:
    if not math.isfinite(value):
        return ""
    return f"{value:.{digits}f}".rstrip("0").rstrip(".")


def load_rows(paths: list[Path]) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for path in paths:
        with path.open(newline="") as handle:
            for raw in csv.DictReader(handle):
                rows.append({
                    "source": str(path),
                    "roi_id": raw.get("anchor_id", ""),
                    "object_id": raw.get("object_id", ""),
                    "step_length": f(raw.get("step_length")),
                    "reference_step_length": f(raw.get("reference_step_length")),
                    "x": i(raw.get("projected_x")),
                    "y": i(raw.get("projected_y")),
                    "tile": raw.get("projected_tile", ""),
                    "offset_x": i(raw.get("radial_offset_x")),
                    "offset_y": i(raw.get("radial_offset_y")),
                    "nearest_anchor_kind": raw.get("nearest_anchor_kind", ""),
                    "hit": b(raw.get("hit")),
                    "collider_id": raw.get("collider_id", ""),
                    "domain_id": raw.get("domain_id", ""),
                    "boundary_events": i(raw.get("boundary_events")),
                    "portal_events": i(raw.get("portal_events")),
                    "hit_distance": f(raw.get("hit_distance"), -1.0),
                    "path_length": f(raw.get("path_length"), -1.0),
                    "normal_x": f(raw.get("normal_x"), 0.0),
                    "normal_y": f(raw.get("normal_y"), 1.0),
                    "normal_z": f(raw.get("normal_z"), 0.0),
                    "decision_risk": f(raw.get("decision_risk"), 0.0),
                    "matched_reference": b(raw.get("matched_reference_decision")),
                    "required_precision": raw.get("required_precision_label", ""),
                })
    return [row for row in rows if math.isfinite(row["step_length"]) and math.isfinite(row["reference_step_length"])]


def sample_key(row: dict[str, Any]) -> tuple[str, str, int, int]:
    return (row["roi_id"], row["object_id"], row["x"], row["y"])


def mean(vals: list[float]) -> float:
    return sum(vals) / len(vals) if vals else math.nan


def normal_angle(a: dict[str, Any], b: dict[str, Any]) -> float:
    av = (a["normal_x"], a["normal_y"], a["normal_z"])
    bv = (b["normal_x"], b["normal_y"], b["normal_z"])
    al = math.sqrt(sum(v * v for v in av))
    bl = math.sqrt(sum(v * v for v in bv))
    if al <= 1e-8 or bl <= 1e-8:
        return 0.0
    dot = sum((av[n] / al) * (bv[n] / bl) for n in range(3))
    return math.acos(max(-1.0, min(1.0, dot)))


def build_samples(rows: list[dict[str, Any]], epsilon: float) -> list[dict[str, Any]]:
    groups: dict[tuple[str, str, int, int], list[dict[str, Any]]] = defaultdict(list)
    for row in rows:
        groups[sample_key(row)].append(row)
    samples: list[dict[str, Any]] = []
    for key, group in groups.items():
        ref_step = min(row["step_length"] for row in group)
        refs = [row for row in group if abs(row["step_length"] - ref_step) <= 1e-9]
        if not refs:
            continue
        ref = refs[0]
        passing = []
        step_rows = []
        for row in sorted(group, key=lambda r: r["step_length"], reverse=True):
            hit_delta = abs(row["hit_distance"] - ref["hit_distance"]) if row["hit"] and ref["hit"] else math.nan
            path_delta = abs(row["path_length"] - ref["path_length"]) if row["path_length"] >= 0 and ref["path_length"] >= 0 else math.nan
            normal_delta = normal_angle(row, ref)
            collider_flip = row["collider_id"] != ref["collider_id"]
            ownership_change = collider_flip or row["hit"] != ref["hit"] or row["domain_id"] != ref["domain_id"]
            stable = row["decision_risk"] <= epsilon and not ownership_change
            if stable:
                passing.append(row["step_length"])
            step_rows.append({**row, "hit_distance_delta": hit_delta, "path_length_delta": path_delta, "normal_angle_delta": normal_delta, "collider_flip": collider_flip, "ownership_change": ownership_change})
        required = max(passing) if passing else ref_step
        samples.append({
            "roi_id": key[0],
            "object_id": key[1],
            "x": key[2],
            "y": key[3],
            "reference_step_length": ref_step,
            "required_precision": required,
            "max_decision_risk": max(row["decision_risk"] for row in group),
            "collider_flip_any": any(row["collider_id"] != ref["collider_id"] for row in group),
            "hit_ownership_change_any": any(row["hit"] != ref["hit"] or row["collider_id"] != ref["collider_id"] for row in group),
            "max_hit_distance_delta": max([r["hit_distance_delta"] for r in step_rows if math.isfinite(r["hit_distance_delta"])] or [0.0]),
            "max_normal_angle_delta": max([r["normal_angle_delta"] for r in step_rows] or [0.0]),
            "_rows": step_rows,
        })
    return samples


def color_by_precision(step: float) -> tuple[int, int, int]:
    palette = {
        0.03: (45, 160, 75),
        0.025: (70, 175, 90),
        0.02: (70, 150, 210),
        0.018: (85, 135, 220),
        0.016: (105, 120, 220),
        0.015: (125, 110, 210),
        0.014: (150, 100, 200),
        0.013: (180, 90, 170),
        0.0125: (210, 110, 80),
        0.011: (220, 140, 55),
        0.010: (235, 170, 40),
        0.0075: (235, 90, 60),
        0.00625: (210, 50, 70),
        0.003125: (190, 50, 180),
    }
    nearest = min(palette, key=lambda k: abs(k - step))
    return palette[nearest]


def heat_color(value: float, max_value: float) -> tuple[int, int, int]:
    t = 0 if max_value <= 0 else max(0.0, min(1.0, value / max_value))
    return (int(255 * t), int(180 * (1 - abs(t - 0.5) * 2)), int(255 * (1 - t)))


def canvas(samples: list[dict[str, Any]]) -> tuple[int, int]:
    return max(320, max((s["x"] for s in samples), default=319) + 40), max(180, max((s["y"] for s in samples), default=179) + 40)


def draw_maps(out: Path, samples: list[dict[str, Any]]) -> None:
    w, h = canvas(samples)
    max_hit = max((s["max_hit_distance_delta"] for s in samples), default=1.0) or 1.0
    max_normal = max((s["max_normal_angle_delta"] for s in samples), default=1.0) or 1.0
    images = {
        "corner_required_precision_map.png": Image.new("RGB", (w, h), (0, 0, 0)),
        "corner_hit_distance_delta.png": Image.new("RGB", (w, h), (0, 0, 0)),
        "corner_normal_delta.png": Image.new("RGB", (w, h), (0, 0, 0)),
        "corner_collider_flip_map.png": Image.new("RGB", (w, h), (0, 0, 0)),
    }
    draws = {name: ImageDraw.Draw(img) for name, img in images.items()}
    for s in samples:
        x, y = s["x"], s["y"]
        draws["corner_required_precision_map.png"].rectangle((x - 1, y - 1, x + 1, y + 1), fill=color_by_precision(float(s["required_precision"])))
        draws["corner_hit_distance_delta.png"].rectangle((x - 1, y - 1, x + 1, y + 1), fill=heat_color(s["max_hit_distance_delta"], max_hit))
        draws["corner_normal_delta.png"].rectangle((x - 1, y - 1, x + 1, y + 1), fill=heat_color(s["max_normal_angle_delta"], max_normal))
        draws["corner_collider_flip_map.png"].rectangle((x - 1, y - 1, x + 1, y + 1), fill=(255, 40, 60) if s["collider_flip_any"] else (40, 170, 90))
    for name, img in images.items():
        img.save(out / name)


def draw_profile(out: Path, samples: list[dict[str, Any]], epsilon: float) -> None:
    by_step: dict[float, list[float]] = defaultdict(list)
    for s in samples:
        for row in s["_rows"]:
            by_step[row["step_length"]].append(row["decision_risk"])
    steps = sorted(by_step, reverse=True)
    width, height = 1050, 620
    ml, mr, mt, mb = 80, 40, 48, 80
    pw, ph = width - ml - mr, height - mt - mb
    img = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(img)
    max_risk = max([epsilon * 1.25, *(max(vals) for vals in by_step.values()), 1e-6])
    draw.rectangle((ml, mt, ml + pw, mt + ph), outline=(30, 30, 30))
    pts = []
    for idx, step in enumerate(steps):
        x = ml + int((idx / max(1, len(steps) - 1)) * pw)
        risk = mean(by_step[step])
        y = mt + int((1 - min(risk / max_risk, 1)) * ph)
        pts.append((x, y))
        draw.text((x - 18, mt + ph + 12), fmt(step), fill=(40, 40, 40))
    if len(pts) >= 2:
        draw.line(pts, fill=(40, 110, 190), width=3)
    for p in pts:
        draw.ellipse((p[0] - 4, p[1] - 4, p[0] + 4, p[1] + 4), fill=(40, 110, 190))
    y_eps = mt + int((1 - min(epsilon / max_risk, 1)) * ph)
    draw.line((ml, y_eps, ml + pw, y_eps), fill=(190, 40, 40), width=2)
    draw.text((ml, 18), "Corner transport convergence profile", fill=(0, 0, 0))
    img.save(out / "corner_convergence_profile.png")


def write_outputs(out: Path, rows: list[dict[str, Any]], samples: list[dict[str, Any]], epsilon: float) -> None:
    with (out / "corner_transport_probe.csv").open("w", newline="") as handle:
        cols = ["roi_id", "object_id", "x", "y", "reference_step_length", "required_precision", "max_decision_risk", "collider_flip_any", "hit_ownership_change_any", "max_hit_distance_delta", "max_normal_angle_delta"]
        writer = csv.DictWriter(handle, fieldnames=cols)
        writer.writeheader()
        for s in samples:
            writer.writerow({k: fmt(s[k]) if isinstance(s[k], float) else s[k] for k in cols})
    roi_groups: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for s in samples:
        roi_groups[s["roi_id"]].append(s)
    roi_summary = []
    for roi, vals in sorted(roi_groups.items()):
        precision = max([v["required_precision"] for v in vals])
        roi_summary.append({
            "roi_id": roi,
            "sample_count": len(vals),
            "required_precision": precision,
            "collider_flip_samples": sum(1 for v in vals if v["collider_flip_any"]),
            "ownership_change_samples": sum(1 for v in vals if v["hit_ownership_change_any"]),
            "mean_max_risk": mean([v["max_decision_risk"] for v in vals]),
        })
    payload = {
        "epsilon": epsilon,
        "row_count": len(rows),
        "sample_count": len(samples),
        "roi_summary": roi_summary,
    }
    (out / "corner_transport_probe.json").write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n")
    draw_maps(out, samples)
    draw_profile(out, samples, epsilon)
    write_report(out / "corner_threshold_report.md", roi_summary, samples)


def write_report(path: Path, roi_summary: list[dict[str, Any]], samples: list[dict[str, Any]]) -> None:
    lines = [
        "# Corner Transport Threshold Report",
        "",
        "This is a passive corner/edge microscope pass. It does not alter beauty rendering, scheduling, hit selection, shading, resolver decisions, or precision stepping.",
        "",
        "## ROI Summary",
        "",
        "| roi | samples | required_precision | collider_flip_samples | ownership_change_samples | mean_max_risk | interpretation |",
        "|---|---:|---:|---:|---:|---:|---|",
    ]
    for r in roi_summary:
        flips = int(r["collider_flip_samples"])
        ownership = int(r["ownership_change_samples"])
        interp = "accuracy_improves" if flips == 0 and ownership == 0 else "hit_ownership_changes"
        lines.append(f"| `{r['roi_id']}` | {r['sample_count']} | {fmt(float(r['required_precision']))} | {flips} | {ownership} | {fmt(float(r['mean_max_risk']))} | {interp} |")
    lines.extend(["", "## Global Notes", ""])
    unstable = sum(1 for s in samples if float(s["required_precision"]) <= 0.003125 + 1e-9 or s["collider_flip_any"])
    lines.append(f"- Samples requiring reference/fine precision or ownership changes: {unstable} / {len(samples)}")
    lines.append("- Corner transitions are local in this probe unless corroborated by separate scheduler row-mod-stride DOE overlays.")
    path.write_text("\n".join(lines) + "\n")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("inputs", nargs="+", type=Path)
    parser.add_argument("--out", type=Path)
    parser.add_argument("--epsilon", type=float, default=0.05)
    args = parser.parse_args()
    files = discover_inputs(args.inputs)
    if not files:
        raise SystemExit("No corner/reference probe CSV inputs found")
    out = args.out or (args.inputs[0] if args.inputs[0].is_dir() else args.inputs[0].parent)
    out.mkdir(parents=True, exist_ok=True)
    rows = load_rows(files)
    samples = build_samples(rows, args.epsilon)
    write_outputs(out, rows, samples, args.epsilon)
    print(f"[corner-probe] inputs={len(files)} rows={len(rows)} samples={len(samples)} out={out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
