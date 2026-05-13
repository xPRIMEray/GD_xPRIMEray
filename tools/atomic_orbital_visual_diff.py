#!/usr/bin/env python3
"""Build visual-only atomic observatory diffs and a contact sheet."""

from __future__ import annotations

import json
import sys
from pathlib import Path


CELLS = [
    "V0_baseline_no_field",
    "V1_static_hydrogen",
    "V2_exaggerated_hydrogen",
    "V3_tick0",
    "V4_tick1",
]
SHADINGS = ["normal_rgb", "depth_heatmap"]


def read_json(path: Path) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return {}


def find_beauty(cell: Path) -> Path | None:
    excluded = (
        "diff",
        "contact_sheet",
        "budget_exhaustion",
        "diagnostic_overlay",
        "layer",
        "full_frame_hit_normals",
        "transport_shape",
    )
    for png in sorted(cell.glob("*.png")):
        name = png.name.lower()
        if any(token in name for token in excluded):
            continue
        return png
    return None


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: atomic_orbital_visual_diff.py <output_dir>", file=sys.stderr)
        return 2

    root = Path(sys.argv[1])
    cells_root = root / "cells"
    images: dict[str, dict[str, Path]] = {shading: {} for shading in SHADINGS}
    metadata: dict[str, dict[str, dict]] = {shading: {} for shading in SHADINGS}
    for shading in SHADINGS:
        for cell in CELLS:
            cell_dir = cells_root / shading / cell
            metadata[shading][cell] = read_json(cell_dir / "metadata.json")
            beauty = find_beauty(cell_dir)
            if beauty:
                images[shading][cell] = beauty

    summary = {
        "purpose": "Interpretation only; not closure validation.",
        "shadings": {
            shading: {
                cell: {
                    "image": str(images[shading][cell]) if cell in images[shading] else "",
                    "image_present": cell in images[shading],
                    "settings": metadata[shading].get(cell, {}),
                }
                for cell in CELLS
            }
            for shading in SHADINGS
        },
    }

    try:
        from PIL import Image, ImageChops, ImageDraw, ImageFont
    except Exception as exc:
        summary["pillow_available"] = False
        summary["pillow_error"] = type(exc).__name__
        write_markdown(root, images, metadata, summary, contact_sheet="")
        (root / "atomic_visual_diff_summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
        print(f"[atomic-visual-diff] report={root / 'atomic_visual_observatory_report.md'} pillow=missing")
        return 0

    summary["pillow_available"] = True
    normal = images["normal_rgb"]
    diffs: dict[str, dict] = {}
    if "V0_baseline_no_field" in normal and "V1_static_hydrogen" in normal:
        diffs["V0_vs_V1"] = write_scaled_diff(
            normal["V0_baseline_no_field"],
            normal["V1_static_hydrogen"],
            root / "V0_vs_V1_diff.png",
            root / "V0_vs_V1_scaled_diff.png",
            Image,
            ImageChops,
        )
    if "V0_baseline_no_field" in normal and "V2_exaggerated_hydrogen" in normal:
        diffs["V0_vs_V2"] = write_scaled_diff(
            normal["V0_baseline_no_field"],
            normal["V2_exaggerated_hydrogen"],
            root / "V0_vs_V2_diff.png",
            root / "V0_vs_V2_scaled_diff.png",
            Image,
            ImageChops,
        )
    if "V3_tick0" in normal and "V4_tick1" in normal:
        diffs["tick0_vs_tick1"] = write_scaled_diff(
            normal["V3_tick0"],
            normal["V4_tick1"],
            root / "tick0_vs_tick1_diff.png",
            root / "tick0_vs_tick1_scaled_diff.png",
            Image,
            ImageChops,
        )
    if diffs:
        summary["normal_rgb_diffs"] = diffs

    contact_sheet = build_contact_sheet(root, images, metadata, diffs, Image, ImageDraw, ImageFont)
    summary["contact_sheet"] = contact_sheet
    write_markdown(root, images, metadata, summary, contact_sheet=contact_sheet)
    (root / "atomic_visual_diff_summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(f"[atomic-visual-diff] report={root / 'atomic_visual_observatory_report.md'} contact_sheet={contact_sheet or 'missing'}")
    return 0


def write_scaled_diff(image_a: Path, image_b: Path, raw_path: Path, scaled_path: Path, Image, ImageChops) -> dict:
    a = Image.open(image_a).convert("RGB")
    b = Image.open(image_b).convert("RGB")
    if a.size != b.size:
        b = b.resize(a.size)
    diff = ImageChops.difference(a, b)
    diff.save(raw_path)
    if hasattr(diff, "get_flattened_data"):
        diff_pixels = list(diff.get_flattened_data())
    else:
        diff_pixels = list(diff.getdata())
    max_channel = max(max(px) for px in diff_pixels) if diff_pixels else 0
    changed = sum(1 for px in diff_pixels if px != (0, 0, 0))
    total = diff.size[0] * diff.size[1]
    mean_abs = sum((px[0] + px[1] + px[2]) / 3.0 for px in diff_pixels) / max(1, total)
    scaled = diff
    if max_channel > 0:
        scale = 255.0 / max_channel
        scaled = diff.point(lambda v: min(255, int(v * scale)))
    scaled.save(scaled_path)
    return {
        "diff": str(raw_path),
        "scaled_diff": str(scaled_path),
        "changed_pixels": changed,
        "total_pixels": total,
        "changed_fraction": changed / total if total else 0.0,
        "mean_abs_channel_delta": mean_abs,
        "max_channel_delta": max_channel,
    }


def build_contact_sheet(root: Path, images: dict[str, dict[str, Path]], metadata: dict[str, dict[str, dict]], diffs: dict[str, dict], Image, ImageDraw, ImageFont) -> str:
    panels: list[tuple[str, Path, dict, str]] = []
    for cell in CELLS:
        if cell in images["normal_rgb"]:
            panels.append((cell, images["normal_rgb"][cell], metadata["normal_rgb"].get(cell, {}), "normal_rgb"))
    for cell in CELLS:
        if cell in images["depth_heatmap"]:
            panels.append((cell, images["depth_heatmap"][cell], metadata["depth_heatmap"].get(cell, {}), "depth_heatmap"))
    for label in ["V0_vs_V1", "V0_vs_V2", "tick0_vs_tick1"]:
        if label in diffs:
            panels.append((f"{label}_scaled_diff", Path(diffs[label]["scaled_diff"]), {}, "normal_rgb diff"))
    if not panels:
        return ""

    thumb_w, thumb_h = 320, 180
    label_h = 88
    columns = 5
    rows = (len(panels) + columns - 1) // columns
    sheet = Image.new("RGB", (thumb_w * columns, (thumb_h + label_h) * rows), (18, 21, 30))
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("DejaVuSans.ttf", 12)
        font_small = ImageFont.truetype("DejaVuSans.ttf", 10)
        font_bold = ImageFont.truetype("DejaVuSans-Bold.ttf", 13)
    except Exception:
        font = font_small = font_bold = None

    for idx, (label, path, meta, row_label) in enumerate(panels):
        col = idx % columns
        row = idx // columns
        x = col * thumb_w
        y = row * (thumb_h + label_h)
        img = Image.open(path).convert("RGB")
        img.thumbnail((thumb_w, thumb_h))
        ox = x + (thumb_w - img.width) // 2
        oy = y + (thumb_h - img.height) // 2
        sheet.paste(img, (ox, oy))
        draw_center_overlay(draw, ox, oy, img.width, img.height, row_label, font_small)
        label_y = y + thumb_h
        draw.rectangle([x, label_y, x + thumb_w - 1, label_y + label_h], fill=(16, 18, 26))
        draw.text((x + 8, label_y + 6), label, fill=(238, 242, 248), font=font_bold)
        draw.text((x + 8, label_y + 24), row_label, fill=(128, 206, 255), font=font)
        if meta:
            settings = (
                f"e={meta.get('electron_count', 'na')} r={meta.get('orbital_radius', 'na')} "
                f"k={meta.get('curvature_strength', 'na')} mod={meta.get('modulation_depth', 'na')}\n"
                f"tick={meta.get('field_tick_index', 'na')} phase={meta.get('phase', 'na')}"
            )
        else:
            settings = "scaled descriptive difference"
        draw.text((x + 8, label_y + 43), settings, fill=(202, 211, 224), font=font)

    draw.text((10, 10), "Interpretation only; not closure validation.", fill=(245, 240, 190), font=font_bold)
    out = root / "atomic_visual_contact_sheet.png"
    sheet.save(out)
    return str(out)


def draw_center_overlay(draw, x: int, y: int, w: int, h: int, row_label: str, font) -> None:
    if "diff" in row_label:
        return
    cx = x + w // 2
    cy = y + h // 2
    ring = max(18, min(w, h) // 5)
    marker = (250, 245, 170)
    draw.ellipse([cx - ring, cy - ring, cx + ring, cy + ring], outline=marker, width=1)
    draw.line([cx - 10, cy, cx + 10, cy], fill=marker, width=1)
    draw.line([cx, cy - 10, cx, cy + 10], fill=marker, width=1)
    draw.text((x + 8, y + 8), row_label, fill=(230, 235, 245), font=font)


def write_markdown(root: Path, images: dict[str, dict[str, Path]], metadata: dict[str, dict[str, dict]], summary: dict, contact_sheet: str) -> None:
    lines = [
        "# Atomic Orbital Visual Observatory Report",
        "",
        "Interpretation only; not closure validation.",
        "",
    ]
    if contact_sheet:
        lines.extend(["![contact sheet](atomic_visual_contact_sheet.png)", ""])
    lines.extend([
        "| shading | panel | image | electron_count | radius | strength | modulation | tick | phase |",
        "| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |",
    ])
    for shading in SHADINGS:
        for cell in CELLS:
            meta = metadata.get(shading, {}).get(cell, {})
            img = images.get(shading, {}).get(cell)
            lines.append(
                f"| `{shading}` | `{cell}` | {img.name if img else 'missing'} | {meta.get('electron_count', 'na')} | "
                f"{meta.get('orbital_radius', 'na')} | {meta.get('curvature_strength', 'na')} | "
                f"{meta.get('modulation_depth', 'na')} | {meta.get('field_tick_index', 'na')} | {meta.get('phase', 'na')} |"
            )
    if "normal_rgb_diffs" in summary:
        lines.extend([
            "",
            "## NormalRGB Beauty Diffs",
            "",
            "These are descriptive image differences from the primary normal_rgb beauty captures.",
            "",
        ])
        for label, diff in summary["normal_rgb_diffs"].items():
            scaled = Path(diff.get("scaled_diff", "")).name
            lines.extend([
                f"### {label}",
                "",
                f"![{label}]({scaled})" if scaled else "",
                "",
                f"- changed_pixels: {diff.get('changed_pixels')}",
                f"- changed_fraction: {diff.get('changed_fraction'):.6f}",
                f"- mean_abs_channel_delta: {diff.get('mean_abs_channel_delta'):.6f}",
                f"- max_channel_delta: {diff.get('max_channel_delta')}",
                "- metrics are descriptive only; they are not pass/fail gates.",
                "",
            ])
    (root / "atomic_visual_observatory_report.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


if __name__ == "__main__":
    raise SystemExit(main())
