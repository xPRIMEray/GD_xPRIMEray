#!/usr/bin/env python3
"""Analyze and visualize xPRIMEray ReferenceTransportOracle packets."""

from __future__ import annotations

import argparse
import csv
import json
import math
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont


GENERATED_NAMES = {
    "oracle_path_overlay.png",
    "production_vs_oracle_diff.png",
    "epsilon_stability_map.png",
    "parent_trajectory_contact_sheet.png",
    "convergence_ladder_contact_sheet.png",
    "precision_cost_curves.png",
}


def load_json(path: Path) -> dict[str, Any]:
    try:
        return json.loads(path.read_text())
    except Exception:
        return {}


def load_jsonl(path: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    if not path.exists():
        return rows
    with path.open() as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            try:
                rows.append(json.loads(line))
            except Exception:
                pass
    return rows


def load_csv(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        return []
    with path.open(newline="", encoding="utf-8-sig") as handle:
        return list(csv.DictReader(handle))


def parse_float(value: Any, default: float = 0.0) -> float:
    try:
        if value in ("", None, "nan", "NaN"):
            return default
        return float(value)
    except Exception:
        return default


def parse_int(value: Any, default: int = 0) -> int:
    try:
        if value in ("", None):
            return default
        return int(float(value))
    except Exception:
        return default


def find_beauty(folder: Path) -> Path | None:
    candidates: list[Path] = []
    for path in sorted(folder.glob("*.png")):
        if path.name in GENERATED_NAMES or path.name.startswith("layer") or path.name.startswith("corner_"):
            continue
        if "overlay" in path.name or "heat_" in path.name or "diagnostic" in path.name:
            continue
        candidates.append(path)
    if not candidates:
        return None
    candidates.sort(key=lambda p: p.stat().st_size, reverse=True)
    return candidates[0]


def draw_label(draw: ImageDraw.ImageDraw, xy: tuple[int, int], text: str, fill=(255, 255, 255, 255)) -> None:
    font = ImageFont.load_default()
    x, y = xy
    box = draw.textbbox((x, y), text, font=font)
    draw.rectangle((box[0] - 2, box[1] - 2, box[2] + 2, box[3] + 2), fill=(0, 0, 0, 180))
    draw.text((x, y), text, fill=fill, font=font)


def circle(draw: ImageDraw.ImageDraw, x: int, y: int, r: int, outline, fill=None, width: int = 1) -> None:
    draw.ellipse((x - r, y - r, x + r, y + r), outline=outline, fill=fill, width=width)


def class_color(label: str) -> tuple[int, int, int, int]:
    label = (label or "").lower()
    if label == "stable":
        return (40, 230, 110, 235)
    if label == "threshold_snap":
        return (255, 210, 40, 235)
    if label == "multi_solution":
        return (170, 80, 255, 235)
    return (255, 60, 80, 235)


def risk_color(risk: float) -> tuple[int, int, int, int]:
    t = max(0.0, min(1.0, risk / 3.0))
    return (int(40 + 215 * t), int(230 * (1.0 - t)), int(255 * (1.0 - 0.4 * t)), 235)


def draw_oracle_paths(base: Image.Image, parent_rows: list[dict[str, Any]]) -> Image.Image:
    img = base.convert("RGBA")
    draw = ImageDraw.Draw(img)
    for row in parent_rows:
        if parse_int(row.get("repeat_index"), 0) != 0:
            continue
        points = row.get("screen_polyline") or []
        pts = [(parse_int(p.get("x"), -1), parse_int(p.get("y"), -1)) for p in points]
        pts = [(x, y) for x, y in pts if x >= 0 and y >= 0]
        if len(pts) >= 2:
            draw.line(pts, fill=(40, 255, 120, 220), width=1)
        px = parse_int(row.get("pixel_x"), -1)
        py = parse_int(row.get("pixel_y"), -1)
        if px >= 0 and py >= 0:
            circle(draw, px, py, 3, (255, 255, 255, 230), fill=(40, 255, 120, 180))
    draw_label(draw, (8, 8), "ReferenceTransportOracle parent paths", (255, 255, 255, 255))
    return img


def draw_diff(base: Image.Image, comparisons: list[dict[str, str]]) -> Image.Image:
    img = base.convert("RGBA")
    draw = ImageDraw.Draw(img)
    by_pixel: dict[tuple[int, int], float] = {}
    for row in comparisons:
        x = parse_int(row.get("pixel_x"), -1)
        y = parse_int(row.get("pixel_y"), -1)
        if x < 0 or y < 0:
            continue
        by_pixel[(x, y)] = max(by_pixel.get((x, y), 0.0), parse_float(row.get("decision_risk"), 0.0))
    for (x, y), risk in by_pixel.items():
        color = risk_color(risk)
        circle(draw, x, y, 4 if risk >= 1.0 else 3, color, fill=color)
    draw_label(draw, (8, 8), "Production vs renderer-reference risk", (255, 255, 255, 255))
    return img


def draw_stability(base: Image.Image, comparisons: list[dict[str, str]]) -> Image.Image:
    img = base.convert("RGBA")
    draw = ImageDraw.Draw(img)
    rank = {"stable": 0, "threshold_snap": 1, "unresolved": 2, "multi_solution": 3}
    by_pixel: dict[tuple[int, int], str] = {}
    for row in comparisons:
        x = parse_int(row.get("pixel_x"), -1)
        y = parse_int(row.get("pixel_y"), -1)
        label = row.get("epsilon_stability_class", "unresolved")
        if x < 0 or y < 0:
            continue
        prior = by_pixel.get((x, y))
        if prior is None or rank.get(label, 2) > rank.get(prior, 2):
            by_pixel[(x, y)] = label
    for (x, y), label in by_pixel.items():
        color = class_color(label)
        circle(draw, x, y, 4, color, fill=color)
    y0 = 8
    for label in ("stable", "threshold_snap", "unresolved", "multi_solution"):
        color = class_color(label)
        draw.rectangle((8, y0, 18, y0 + 10), fill=color)
        draw_label(draw, (24, y0 - 1), label, (255, 255, 255, 255))
        y0 += 18
    return img


def draw_cost_curves(size: tuple[int, int], cost_rows: list[dict[str, str]]) -> Image.Image:
    w, h = size
    img = Image.new("RGBA", size, (18, 20, 24, 255))
    draw = ImageDraw.Draw(img)
    draw_label(draw, (8, 8), "Precision cost curves", (255, 255, 255, 255))
    rows = [r for r in cost_rows if parse_float(r.get("step_length"), 0) > 0]
    if not rows:
        draw_label(draw, (24, h // 2), "No cost curve data", (255, 255, 255, 255))
        return img
    by_step: dict[float, list[dict[str, str]]] = defaultdict(list)
    for row in rows:
        by_step[parse_float(row.get("step_length"))].append(row)
    steps = sorted(by_step.keys(), reverse=True)
    mean_steps = [sum(parse_float(r.get("step_count")) for r in by_step[s]) / max(1, len(by_step[s])) for s in steps]
    mean_risk = [sum(parse_float(r.get("decision_risk")) for r in by_step[s]) / max(1, len(by_step[s])) for s in steps]
    left, top, right, bottom = 48, 36, w - 16, h - 30
    draw.rectangle((left, top, right, bottom), outline=(180, 180, 180, 180))
    max_step_count = max(mean_steps) if mean_steps else 1.0
    max_risk = max(mean_risk) if mean_risk else 1.0

    def xy(i: int, value: float, max_value: float) -> tuple[int, int]:
        x = left + int((right - left) * (i / max(1, len(steps) - 1)))
        y = bottom - int((bottom - top) * (value / max(1e-6, max_value)))
        return x, y

    step_pts = [xy(i, v, max_step_count) for i, v in enumerate(mean_steps)]
    risk_pts = [xy(i, v, max_risk) for i, v in enumerate(mean_risk)]
    if len(step_pts) >= 2:
        draw.line(step_pts, fill=(0, 220, 255, 230), width=2)
    if len(risk_pts) >= 2:
        draw.line(risk_pts, fill=(255, 90, 120, 230), width=2)
    for i, s in enumerate(steps):
        x, _ = xy(i, 0, 1)
        draw.text((x - 12, bottom + 4), f"{s:g}", fill=(220, 220, 220, 230), font=ImageFont.load_default())
    draw_label(draw, (left + 6, top + 8), "cyan: mean step_count", (0, 220, 255, 230))
    draw_label(draw, (left + 6, top + 26), "red: mean decision_risk", (255, 90, 120, 230))
    return img


def make_contact_sheet(folder: Path, items: list[tuple[str, Path]]) -> None:
    existing = [(label, path) for label, path in items if path.exists()]
    if not existing:
        return
    thumbs: list[tuple[str, Image.Image]] = []
    for label, path in existing:
        img = Image.open(path).convert("RGBA")
        img.thumbnail((260, 180))
        thumbs.append((label, img.copy()))
    pad = 12
    label_h = 18
    w = sum(img.width for _, img in thumbs) + pad * (len(thumbs) + 1)
    h = max(img.height for _, img in thumbs) + pad * 2 + label_h
    sheet = Image.new("RGBA", (w, h), (18, 20, 24, 255))
    draw = ImageDraw.Draw(sheet)
    x = pad
    for label, img in thumbs:
        draw.text((x, pad), label, fill=(255, 255, 255, 255), font=ImageFont.load_default())
        sheet.alpha_composite(img, (x, pad + label_h))
        x += img.width + pad
    sheet.save(folder / "parent_trajectory_contact_sheet.png")


def make_convergence_ladder(folder: Path, base: Image.Image, comparisons: list[dict[str, str]]) -> None:
    steps = sorted({parse_float(r.get("production_step_length")) for r in comparisons if r.get("production_step_length")}, reverse=True)
    if not steps:
        return
    panels: list[tuple[str, Image.Image]] = []
    for step in steps:
        panel_rows = [r for r in comparisons if abs(parse_float(r.get("production_step_length")) - step) <= 1e-9]
        panel = draw_stability(base, panel_rows)
        panel.thumbnail((180, 120))
        panels.append((f"{step:g}", panel.copy()))
    oracle_panel = draw_oracle_paths(base, [])
    oracle_panel.thumbnail((180, 120))
    panels.append(("oracle", oracle_panel.copy()))
    pad, label_h = 10, 18
    w = sum(img.width for _, img in panels) + pad * (len(panels) + 1)
    h = max(img.height for _, img in panels) + pad * 2 + label_h
    sheet = Image.new("RGBA", (w, h), (18, 20, 24, 255))
    draw = ImageDraw.Draw(sheet)
    x = pad
    for label, img in panels:
        draw.text((x, pad), label, fill=(255, 255, 255, 255), font=ImageFont.load_default())
        sheet.alpha_composite(img, (x, pad + label_h))
        x += img.width + pad
    sheet.save(folder / "convergence_ladder_contact_sheet.png")


def write_report(folder: Path, metadata: dict[str, Any], comparisons: list[dict[str, str]], family: list[dict[str, str]], cost_rows: list[dict[str, str]]) -> None:
    class_counts = Counter(r.get("epsilon_stability_class", "unresolved") for r in comparisons)
    tag_counts: Counter[str] = Counter()
    repeat_fail = 0
    risks = []
    for row in comparisons:
        risks.append(parse_float(row.get("decision_risk"), 0.0))
        if row.get("oracle_repeat_match") not in {"1", "true", "True"}:
            repeat_fail += 1
        for tag in (row.get("secondary_tags") or "").split("|"):
            if tag:
                tag_counts[tag] += 1
    family_counts = Counter(r.get("family_class", "") for r in family)
    min_step_by_stable: dict[str, float] = {}
    for row in comparisons:
        if row.get("epsilon_stability_class") == "stable":
            sid = row.get("sample_id", "")
            step = parse_float(row.get("production_step_length"), math.inf)
            min_step_by_stable[sid] = min(min_step_by_stable.get(sid, math.inf), step)
    lines = [
        "# ReferenceTransportOracle Report",
        "",
        "Diagnostic-only renderer validation. This packet records best-known renderer-reference transport paths; it is not physical GR validation.",
        "",
        f"- Samples: {metadata.get('sample_count', 0)}",
        f"- Oracle runs: {metadata.get('oracle_run_count', 0)}",
        f"- Comparisons: {len(comparisons)}",
        f"- Family rows: {len(family)}",
        f"- Cost rows: {len(cost_rows)}",
        f"- Runtime ms: {metadata.get('runtime_ms', '')}",
        f"- Oracle replay failures: {repeat_fail}",
        f"- Mean decision risk: {(sum(risks) / max(1, len(risks))):.6f}",
        f"- Max decision risk: {(max(risks) if risks else 0):.6f}",
        "",
        "## Epsilon Stability Classes",
        "",
    ]
    for key, value in sorted(class_counts.items()):
        lines.append(f"- {key}: {value}")
    lines.extend(["", "## Secondary Pathology Tags", ""])
    for key, value in sorted(tag_counts.items()):
        lines.append(f"- {key}: {value}")
    lines.extend(["", "## Trajectory Family Classes", ""])
    for key, value in sorted(family_counts.items()):
        lines.append(f"- {key or 'unknown'}: {value}")
    lines.extend(["", "## Precision Cost", ""])
    lines.append(f"- Samples with at least one stable production step: {len(min_step_by_stable)}")
    lines.append("- Minimum precision means minimum tested production step satisfying topology/epsilon stability, not smallest possible step.")
    lines.extend(["", "## Guardrail", ""])
    lines.append("- Oracle outputs must not feed rendering, scheduling, hit selection, shading, resolver decisions, traversal, or adaptive precision.")
    (folder / "reference_transport_oracle_report.md").write_text("\n".join(lines) + "\n")


def analyze(folder: Path) -> int:
    metadata_path = next(iter(sorted(folder.glob("*.reference_transport_oracle.json"))), None)
    if metadata_path is None:
        raise SystemExit(f"No *.reference_transport_oracle.json found in {folder}")
    metadata = load_json(metadata_path)
    stem = str(metadata.get("capture_stem") or metadata_path.name.replace(".reference_transport_oracle.json", ""))
    parent_path = folder / f"{stem}.reference_transport_oracle_parent_paths.jsonl"
    comparisons_path = folder / f"{stem}.reference_transport_oracle_comparisons.csv"
    family_path = folder / f"{stem}.reference_transport_oracle_family.csv"
    cost_path = folder / f"{stem}.reference_transport_oracle_cost_curves.csv"
    parent_rows = load_jsonl(parent_path)
    comparisons = load_csv(comparisons_path)
    family = load_csv(family_path)
    cost_rows = load_csv(cost_path)
    beauty_path = find_beauty(folder)
    if beauty_path and beauty_path.exists():
        beauty = Image.open(beauty_path).convert("RGBA")
    else:
        width = parse_int(metadata.get("image_width"), 320)
        height = parse_int(metadata.get("image_height"), 180)
        beauty = Image.new("RGBA", (width, height), (20, 22, 28, 255))

    path_overlay = draw_oracle_paths(beauty, parent_rows)
    path_overlay.save(folder / "oracle_path_overlay.png")
    diff = draw_diff(beauty, comparisons)
    diff.save(folder / "production_vs_oracle_diff.png")
    stability = draw_stability(beauty, comparisons)
    stability.save(folder / "epsilon_stability_map.png")
    cost = draw_cost_curves(beauty.size, cost_rows)
    cost.save(folder / "precision_cost_curves.png")
    make_contact_sheet(folder, [
        ("beauty", beauty_path if beauty_path else folder / "missing.png"),
        ("oracle paths", folder / "oracle_path_overlay.png"),
        ("prod vs oracle", folder / "production_vs_oracle_diff.png"),
        ("epsilon", folder / "epsilon_stability_map.png"),
        ("cost", folder / "precision_cost_curves.png"),
    ])
    make_convergence_ladder(folder, beauty, comparisons)
    write_report(folder, metadata, comparisons, family, cost_rows)
    print(f"[reference-transport-oracle-analysis] folder={folder} comparisons={len(comparisons)} family={len(family)}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("folder", type=Path)
    args = parser.parse_args()
    return analyze(args.folder)


if __name__ == "__main__":
    raise SystemExit(main())
