#!/usr/bin/env python3
"""Build Observatory Story sheets for canonical xPRIMEray fixture outputs.

This is a post-processing/reporting tool only. It reads existing output
artifacts and does not feed rendering, scheduling, hit selection, shading,
resolver decisions, traversal, or adaptive precision.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import shutil
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont


PANEL_CANVAS = (320, 180)
TITLE_H = 30
CAPTION_H = 112
PAD = 8
CELL_W = PANEL_CANVAS[0] + PAD * 2
CELL_H = TITLE_H + PANEL_CANVAS[1] + CAPTION_H + PAD * 2
ROOT = Path(__file__).resolve().parents[1]


FIXTURE_ORDER = [
    "hermetic_curved_room",
    "curved_minimal",
    "object_island",
    "cathedral_probe",
    "oracle_closure",
]


FIXTURE_NOTES = {
    "hermetic_curved_room": "Sealed-room closure and curvature FPS fixture. Proves every evaluated ray can hit a receiver under a curvature ramp.",
    "curved_minimal": "Canonical curved-minimal/backdrop ladder output. Proves field-sensitive traversal and transport diagnostics on a compact curved scene.",
    "object_island": "ReferenceTransportOracle unresolved-island output. Proves island/convergence diagnostics around ambiguous or unresolved transport regions.",
    "cathedral_probe": "Mapped to existing first-pass corner/reference probe outputs; no literal cathedral_probe run folder was discovered.",
    "oracle_closure": "ReferenceTransportOracle/closure comparison output. Proves passive oracle diagnostics are available without feeding renderer decisions.",
}


PANELS = [
    ("Raw visual", "raw_visual", "Question: What did the camera actually see?\nAcademic: final beauty/render output.\nAnalogy: lab camera photo of the fixture."),
    ("Scene geometry", "geometry", "Question: What objects exist in the scene?\nAcademic: Cartesian object/receiver geometry.\nAnalogy: blueprint of the test chamber."),
    ("Curvature field", "field", "Question: What field is bending the rays?\nAcademic: field-source volume or field diagnostic when available.\nAnalogy: wind/weather map inside the chamber."),
    ("Transport ownership", "ownership", "Question: Where did each ray end up?\nAcademic: receiver/domain ownership.\nAnalogy: territory map for photon delivery zones."),
    ("Hit/miss map", "hit_miss", "Question: Did every ray find a target?\nAcademic: hit/miss or closure validation.\nAnalogy: target board hit test."),
    ("Traversal steps", "traversal", "Question: How hard was the trip?\nAcademic: per-pixel integration/traversal cost.\nAnalogy: traffic/congestion map."),
    ("Budget stress", "budget", "Question: Which rays nearly ran out of budget?\nAcademic: max-step / overrun-step / precision stress.\nAnalogy: fuel warning light."),
    ("Combined diagnostic", "combined", "Question: What do all diagnostics look like together?\nAcademic: composite diagnostic overlay.\nAnalogy: mission-control dashboard."),
    ("Curvature signature", "signature", "Difference relative to baseline when available.\nQuestion: What changed when the field/probe was activated?\nAcademic: delta/island/oracle signature diagnostic.\nAnalogy: weather-change map."),
]


def load_json(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {}
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return {}


def load_font(size: int) -> Any:
    for path in (
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
    ):
        try:
            return ImageFont.truetype(path, size)
        except Exception:
            continue
    return ImageFont.load_default()


def latest_dir(pattern: str) -> Path | None:
    matches = [p for p in ROOT.glob(pattern) if p.exists()]
    if not matches:
        return None
    matches.sort(key=lambda p: (p.stat().st_mtime, str(p)))
    return matches[-1]


def find_first(folder: Path | None, patterns: list[str]) -> Path | None:
    if not folder or not folder.exists():
        return None
    for pattern in patterns:
        matches = sorted(folder.glob(pattern))
        if matches:
            return matches[0]
    return None


def find_any(folder: Path | None, patterns: list[str]) -> Path | None:
    if not folder or not folder.exists():
        return None
    for pattern in patterns:
        matches = sorted(folder.glob(pattern))
        if matches:
            matches.sort(key=lambda p: (p.stat().st_size if p.is_file() else 0, str(p)), reverse=True)
            return matches[0]
    return None


def discover_sources() -> dict[str, dict[str, Any]]:
    latest_curvature = latest_dir("output/curvature_fps_benchmark/*")
    hermetic_cell = latest_curvature / "cells" / "curvature_100" / "row" if latest_curvature else None

    curved_cell = latest_dir("output/curved_field_validation_ladder/*/curved/steps/step_0.015")
    object_island_cell = latest_dir("output/reference_transport_oracle_unresolved_island/*/cells/unresolved_island")
    cathedral_cell = latest_dir("output/first_pass_traversal_comparison/*/step_0.015/row")
    oracle_cell = latest_dir("output/reference_transport_oracle_roi_sweep/*/cells/row_stride_1")
    if oracle_cell is None:
        oracle_cell = latest_dir("output/curved_field_validation_ladder/*/curved/oracle")

    return {
        "hermetic_curved_room": {
            "source": hermetic_cell,
            "study": "curvature_fps_benchmark",
            "selection": "latest curvature_fps_benchmark 100% cell",
        },
        "curved_minimal": {
            "source": curved_cell,
            "study": "curved_field_validation_ladder",
            "selection": "latest curved/steps/step_0.015 cell",
        },
        "object_island": {
            "source": object_island_cell,
            "study": "reference_transport_oracle_unresolved_island",
            "selection": "latest unresolved_island cell",
        },
        "cathedral_probe": {
            "source": cathedral_cell,
            "study": "first_pass_traversal_comparison",
            "selection": "latest step_0.015 row probe cell; no literal cathedral_probe output discovered",
        },
        "oracle_closure": {
            "source": oracle_cell,
            "study": "reference_transport_oracle_roi_sweep",
            "selection": "latest reference oracle ROI row_stride_1 cell, or curved oracle fallback",
        },
    }


def image_for_panel(source: Path | None, key: str) -> Path | None:
    patterns = {
        "raw_visual": [
            "layer0_beauty.png",
            "*__runid-*.png",
            "*.png",
        ],
        "geometry": [
            "cartesian_scene_geometry.png",
            "layer1_cartesian_wireframe.png",
            "diagnostic_quad_panel.png",
            "camera_cross_section_*/*.png",
        ],
        "field": [
            "curvature_field_view.png",
            "curvature_signature.png",
            "curved_vs_straight_difference.png",
            "depth_heatmap.png",
            "corner_required_precision_map.png",
        ],
        "ownership": [
            "layer2_transport_ownership.png",
            "transport_shape_regions_overlay.png",
            "ownership_graph_node_map.png",
        ],
        "hit_miss": [
            "hit_miss_map.png",
            "hermetic_hit_closure_heatmap.png",
            "corner_collider_flip_map.png",
        ],
        "traversal": [
            "traversal_step_heatmap.png",
            "curvature_signature.png",
            "curved_vs_straight_difference.png",
            "corner_convergence_profile.png",
        ],
        "budget": [
            "budget_exhaustion_heatmap.png",
            "budget_exhaustion_overlay.png",
            "corner_required_precision_map.png",
            "precision_required.png",
        ],
        "combined": [
            "combined_diagnostic_overlay.png",
            "diagnostic_storyboard.png",
            "diagnostic_overlay_contact_sheet.png",
            "island_convergence_ladder.png",
            "parent_trajectory_contact_sheet.png",
        ],
        "signature": [
            "curvature_signature.png",
            "curved_vs_straight_difference.png",
            "island_convergence_ladder.png",
            "production_vs_oracle_diff.png",
            "oracle_path_overlay.png",
            "corner_required_precision_map.png",
        ],
    }
    return find_any(source, patterns.get(key, []))


def parse_hit_metrics(source: Path | None) -> dict[str, Any]:
    hit_csv = find_first(source, ["*.hit_diagnostics.csv"]) if source else None
    if not hit_csv:
        return {}
    total = 0
    hits = 0
    steps: list[int] = []
    budget = 0
    try:
        with hit_csv.open(newline="", encoding="utf-8-sig") as handle:
            for row in csv.DictReader(handle):
                sampled = (
                    str(row.get("had_hit", "")).lower() in {"1", "true"}
                    or row.get("step_count", "") not in {"", "0"}
                    or row.get("segment_count", "") not in {"", "0"}
                )
                if not sampled:
                    continue
                total += 1
                if str(row.get("had_hit", "")).lower() in {"1", "true"}:
                    hits += 1
                try:
                    step = int(float(row.get("final_step_count") or row.get("step_count") or 0))
                    if step > 0:
                        steps.append(step)
                except Exception:
                    pass
                if str(row.get("budget_exhausted_without_hit", "")).lower() in {"1", "true"}:
                    budget += 1
    except Exception:
        return {}
    misses = total - hits
    return {
        "hit_diagnostics_csv": str(hit_csv),
        "total_pixels_rays_evaluated": total,
        "hit_count": hits,
        "miss_count": misses,
        "hit_percent": round(100.0 * hits / total, 6) if total else None,
        "average_traversal_steps": round(sum(steps) / len(steps), 6) if steps else None,
        "max_traversal_steps": max(steps) if steps else None,
        "budget_exhausted_without_hit_count": budget,
    }


def make_heatmaps(source: Path | None, out_dir: Path) -> dict[str, Path]:
    hit_csv = find_first(source, ["*.hit_diagnostics.csv"]) if source else None
    if not hit_csv:
        return {}
    rows = []
    try:
        with hit_csv.open(newline="", encoding="utf-8-sig") as handle:
            rows = list(csv.DictReader(handle))
    except Exception:
        return {}
    xs = [int(float(r.get("x", -1) or -1)) for r in rows]
    ys = [int(float(r.get("y", -1) or -1)) for r in rows]
    width = max(xs) + 1 if xs else 0
    height = max(ys) + 1 if ys else 0
    if width <= 0 or height <= 0:
        return {}
    hit = Image.new("RGB", (width, height), (28, 30, 40))
    step = Image.new("RGB", (width, height), (6, 8, 18))
    hit_px = hit.load()
    step_values = []
    parsed = []
    for row in rows:
        try:
            x = int(float(row.get("x", -1) or -1))
            y = int(float(row.get("y", -1) or -1))
            s = int(float(row.get("final_step_count") or row.get("step_count") or 0))
        except Exception:
            continue
        if 0 <= x < width and 0 <= y < height:
            parsed.append((x, y, s, str(row.get("had_hit", "")).lower() in {"1", "true"}))
            if s > 0:
                step_values.append(s)
    max_step = max(step_values) if step_values else 1
    step_px = step.load()
    for x, y, s, had in parsed:
        hit_px[x, y] = (35, 190, 120) if had else (240, 50, 95)
        t = min(1.0, s / max(1, max_step))
        step_px[x, y] = (int(30 + 225 * t), int(70 + 120 * (1.0 - t)), int(210 * (1.0 - t)))
    outputs = {}
    for key, name, img in (("hit_miss", "hit_miss_map", hit), ("traversal", "traversal_step_heatmap", step)):
        path = out_dir / f"generated_{name}.png"
        scale = max(1, min(8, 640 // max(1, width)))
        img.resize((width * scale, height * scale), Image.Resampling.NEAREST).save(path)
        outputs[key] = path
    return outputs


def wrap_text(draw: ImageDraw.ImageDraw, text: str, max_width: int, font: Any) -> list[str]:
    lines: list[str] = []
    for source_line in str(text or "").splitlines():
        words = source_line.split()
        current = ""
        for word in words:
            candidate = word if not current else f"{current} {word}"
            if draw.textbbox((0, 0), candidate, font=font)[2] <= max_width:
                current = candidate
            else:
                if current:
                    lines.append(current)
                current = word
        if current:
            lines.append(current)
    return lines or [""]


def fit_image(image: Image.Image) -> Image.Image:
    source = image.convert("RGB")
    resample = Image.Resampling.NEAREST if source.width < PANEL_CANVAS[0] or source.height < PANEL_CANVAS[1] else Image.Resampling.LANCZOS
    source.thumbnail(PANEL_CANVAS, resample)
    canvas = Image.new("RGB", PANEL_CANVAS, (248, 248, 248))
    canvas.paste(source, ((PANEL_CANVAS[0] - source.width) // 2, (PANEL_CANVAS[1] - source.height) // 2))
    return canvas


def placeholder(title: str, status: str, detail: str) -> Image.Image:
    img = Image.new("RGB", PANEL_CANVAS, (24, 26, 34))
    draw = ImageDraw.Draw(img)
    font = load_font(14)
    small = load_font(12)
    draw.rectangle((0, 0, PANEL_CANVAS[0] - 1, PANEL_CANVAS[1] - 1), outline=(190, 70, 70), width=2)
    draw.text((14, 16), title, fill=(225, 228, 238), font=font)
    draw.text((14, 70), status, fill=(255, 215, 80), font=font)
    for i, line in enumerate(wrap_text(draw, detail, PANEL_CANVAS[0] - 28, small)[:4]):
        draw.text((14, 102 + i * 16), line, fill=(235, 235, 245), font=small)
    return img


def make_cell(title: str, image: Image.Image, caption: str) -> Image.Image:
    font = load_font(13)
    title_font = load_font(14)
    cell = Image.new("RGB", (CELL_W, CELL_H), (246, 247, 250))
    draw = ImageDraw.Draw(cell)
    draw.rectangle((0, 0, CELL_W - 1, CELL_H - 1), outline=(205, 209, 218))
    draw.rectangle((0, 0, CELL_W - 1, TITLE_H - 1), fill=(232, 235, 242))
    draw.text((8, 7), title, fill=(18, 22, 32), font=title_font)
    cell.paste(fit_image(image), (PAD, TITLE_H + PAD))
    cap_y = TITLE_H + PAD + PANEL_CANVAS[1] + 6
    for idx, line in enumerate(wrap_text(draw, caption, CELL_W - 16, font)[:6]):
        draw.text((8, cap_y + idx * 16), line, fill=(35, 39, 50), font=font)
    return cell


def build_sheet(panels: list[Image.Image], output: Path) -> None:
    sheet = Image.new("RGB", (CELL_W * 3, CELL_H * 3), "white")
    for idx, panel in enumerate(panels):
        row = idx // 3
        col = idx % 3
        sheet.paste(panel, (col * CELL_W, row * CELL_H))
    sheet.save(output)


def build_fixture(fixture: str, source_info: dict[str, Any], out_root: Path) -> dict[str, Any]:
    fixture_dir = out_root / fixture
    assets_dir = fixture_dir / "assets"
    if assets_dir.exists():
        shutil.rmtree(assets_dir)
    assets_dir.mkdir(parents=True, exist_ok=True)
    source = source_info.get("source")
    generated = make_heatmaps(source, assets_dir)
    panel_records = []
    cells = []
    for title, key, caption in PANELS:
        found = generated.get(key) or image_for_panel(source, key)
        status = "available" if found and Path(found).exists() else "missing"
        if found and Path(found).exists():
            try:
                img = Image.open(found).convert("RGB")
            except Exception:
                img = placeholder(title, "INVALID", Path(found).name)
                status = "invalid"
        else:
            detail = "No existing artifact matched this Observatory panel for the selected fixture output."
            if source is None:
                detail = "No source output folder was discovered for this fixture."
            img = placeholder(title, "MISSING / N.A.", detail)
        cells.append(make_cell(title, img, caption))
        panel_records.append({
            "title": title,
            "key": key,
            "status": status,
            "path": str(found) if found else "",
        })
    sheet_path = fixture_dir / "diagnostic_contact_sheet.png"
    build_sheet(cells, sheet_path)

    metadata = load_json(source / "metadata.json") if source else {}
    summary = {
        "fixture": fixture,
        "study": source_info.get("study", ""),
        "source_dir": str(source) if source else "",
        "selection": source_info.get("selection", ""),
        "note": FIXTURE_NOTES.get(fixture, ""),
        "contact_sheet": str(sheet_path),
        "layout": {"selected": "square", "columns": 3, "rows": 3, "row_major_order": True},
        "panels": panel_records,
        "available_panel_count": sum(1 for p in panel_records if p["status"] == "available"),
        "missing_panel_count": sum(1 for p in panel_records if p["status"] != "available"),
        "hit_metrics": parse_hit_metrics(source),
        "metadata": metadata,
    }
    summary_path = fixture_dir / "summary.json"
    summary_path.write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    report_path = fixture_dir / "fixture_observatory_report.md"
    lines = [
        f"# {fixture} Observatory Report",
        "",
        FIXTURE_NOTES.get(fixture, ""),
        "",
        f"![diagnostic contact sheet](diagnostic_contact_sheet.png)",
        "",
        "## Source",
        "",
        f"- study: `{summary['study']}`",
        f"- source_dir: `{summary['source_dir'] or 'not discovered'}`",
        f"- selection: {summary['selection']}",
        "",
        "## Panel Availability",
        "",
        "| # | panel | status | artifact |",
        "|---:|---|---|---|",
    ]
    for idx, p in enumerate(panel_records, 1):
        artifact = Path(p["path"]).name if p["path"] else ""
        lines.append(f"| {idx} | {p['title']} | {p['status']} | `{artifact}` |")
    if summary["hit_metrics"]:
        hm = summary["hit_metrics"]
        lines += [
            "",
            "## Hit Metrics",
            "",
            f"- evaluated rays/pixels: `{hm.get('total_pixels_rays_evaluated')}`",
            f"- hit count: `{hm.get('hit_count')}`",
            f"- miss count: `{hm.get('miss_count')}`",
            f"- hit percent: `{hm.get('hit_percent')}`",
            f"- average traversal steps: `{hm.get('average_traversal_steps')}`",
            f"- max traversal steps: `{hm.get('max_traversal_steps')}`",
        ]
    report_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return summary


def write_index(out_root: Path, summaries: list[dict[str, Any]]) -> None:
    index = ROOT / "reports" / "observatory_fixture_index.md"
    lines = [
        "# Observatory Fixture Index",
        "",
        "Square 3x3 Observatory Story sheets generated from existing xPRIMEray outputs. Missing panels are explicit placeholders; renderer logic was not changed.",
        "",
        "| fixture | what it proves | panels | contact sheet | report | source |",
        "|---|---|---:|---|---|---|",
    ]
    for s in summaries:
        fixture = s["fixture"]
        rel_sheet = Path("observatory_fixtures") / fixture / "diagnostic_contact_sheet.png"
        rel_report = Path("observatory_fixtures") / fixture / "fixture_observatory_report.md"
        source = s.get("source_dir") or "not discovered"
        lines.append(
            f"| `{fixture}` | {s.get('note', '')} | {s.get('available_panel_count', 0)}/9 | "
            f"[sheet]({rel_sheet.as_posix()}) | [report]({rel_report.as_posix()}) | `{source}` |"
        )
    index.write_text("\n".join(lines) + "\n", encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, default=ROOT / "reports" / "observatory_fixtures")
    parser.add_argument("--fixtures", nargs="*", default=FIXTURE_ORDER)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    sources = discover_sources()
    args.out.mkdir(parents=True, exist_ok=True)
    summaries = []
    for fixture in args.fixtures:
        summaries.append(build_fixture(fixture, sources.get(fixture, {"source": None}), args.out))
    write_index(args.out, summaries)
    print(f"[observatory-fixture-report] fixtures={len(summaries)} out={args.out}")
    print(f"[observatory-fixture-report] index={ROOT / 'reports' / 'observatory_fixture_index.md'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
