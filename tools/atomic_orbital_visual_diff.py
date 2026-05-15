#!/usr/bin/env python3
"""Build visual-only atomic observatory diffs and a contact sheet."""

from __future__ import annotations

import json
import math
import os
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
        "contours",
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


def read_bool_env(name: str, fallback: bool) -> bool:
    value = os.environ.get(name)
    if value is None:
        return fallback
    return value.strip().lower() in {"1", "true", "yes", "on"}


def read_int_env(name: str, fallback: int, min_value: int, max_value: int) -> int:
    try:
        value = int(os.environ.get(name, str(fallback)))
    except ValueError:
        value = fallback
    return max(min_value, min(max_value, value))


def read_contour_config() -> dict:
    mode = os.environ.get("ATOMIC_ORBITAL_VISUAL_CONTOUR_MODE", "density").strip().lower()
    if mode not in {"density", "curvature"}:
        mode = "density"
    levels = read_int_env("ATOMIC_ORBITAL_VISUAL_CONTOUR_LEVELS", 6, 4, 8)
    return {
        "enabled": read_bool_env("ATOMIC_ORBITAL_VISUAL_CONTOURS", False),
        "mode": mode,
        "levels": levels,
        "labels": read_bool_env("ATOMIC_ORBITAL_VISUAL_CONTOUR_LABELS", False),
        "line_alpha": 58,
        "legend_alpha": 150,
        "line_width_px": 1,
        "mapping": "v1_image_center_min_dimension_div_5",
        "safety_note": "Contours are analytic postprocess guides derived from fixture metadata, not sampled render telemetry.",
    }


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
        "contours": read_contour_config(),
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

    contour_overlays: dict[str, dict[str, dict]] = {shading: {} for shading in SHADINGS}
    if summary["contours"]["enabled"]:
        contour_overlays = write_contour_overlays(root, images, metadata, summary["contours"], Image, ImageDraw, ImageFont)
        summary["contours"]["overlays"] = contour_overlays

    contact_sheet = build_contact_sheet(root, images, metadata, diffs, Image, ImageDraw, ImageFont, contour_overlays)
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


def write_contour_overlays(root: Path, images: dict[str, dict[str, Path]], metadata: dict[str, dict[str, dict]], config: dict, Image, ImageDraw, ImageFont) -> dict[str, dict[str, dict]]:
    overlays: dict[str, dict[str, dict]] = {shading: {} for shading in SHADINGS}
    for shading in SHADINGS:
        for cell, image_path in images.get(shading, {}).items():
            meta = metadata.get(shading, {}).get(cell, {})
            overlay = write_contour_overlay(image_path, meta, shading, config, Image, ImageDraw, ImageFont)
            if overlay:
                overlays[shading][cell] = overlay
    return overlays


def write_contour_overlay(image_path: Path, meta: dict, shading: str, config: dict, Image, ImageDraw, ImageFont) -> dict:
    electron_count = safe_float(meta.get("electron_count", 0.0))
    if electron_count <= 0.0:
        return {}

    image = Image.open(image_path).convert("RGBA")
    width, height = image.size
    center_px = [width / 2.0, height / 2.0]
    orbital_radius_px = max(1.0, min(width, height) / 5.0)
    scalar_max = scalar_at_radius_px(0.0, orbital_radius_px, meta, config["mode"])
    if scalar_max <= 0.0:
        return {}

    outer_px = min(width, height) * 0.48
    scalar_outer = scalar_at_radius_px(outer_px, orbital_radius_px, meta, config["mode"])
    inner_scalar = scalar_at_radius_px(orbital_radius_px * 0.18, orbital_radius_px, meta, config["mode"])
    low = max(scalar_outer, scalar_max * 0.035)
    high = min(inner_scalar, scalar_max * 0.82)
    if high <= low:
        high = scalar_max * 0.82
        low = scalar_max * 0.08
    level_values = [
        high - ((high - low) * i / max(1, config["levels"] - 1))
        for i in range(config["levels"])
    ]
    radii_px = [
        radius_px_for_scalar(value, orbital_radius_px, meta, config["mode"])
        for value in level_values
    ]
    radii_px = [r for r in radii_px if math.isfinite(r) and 1.0 <= r <= max(width, height)]
    if not radii_px:
        return {}

    scale = 3
    overlay = Image.new("RGBA", (width * scale, height * scale), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    cx = center_px[0] * scale
    cy = center_px[1] * scale
    line_alpha = int(config.get("line_alpha", 58))
    line_color = (236, 244, 255, line_alpha) if shading == "normal_rgb" else (255, 244, 190, line_alpha)
    for radius in radii_px:
        r = radius * scale
        draw.ellipse([cx - r, cy - r, cx + r, cy + r], outline=line_color, width=scale)

    overlay = overlay.resize((width, height), resample=Image.Resampling.LANCZOS)
    composed = Image.alpha_composite(image, overlay)
    draw_final = ImageDraw.Draw(composed)
    font, font_small, font_bold = load_fonts(ImageFont)
    draw_contour_legend(draw_final, config, font_small, font_bold)
    if config.get("labels"):
        draw_contour_labels(draw_final, center_px, orbital_radius_px, font_small)

    out = image_path.with_name(f"{image_path.stem}__{config['mode']}_contours.png")
    composed.convert("RGB").save(out)
    return {
        "image": str(out),
        "source_image": str(image_path),
        "mode": config["mode"],
        "levels": len(radii_px),
        "level_values": level_values[: len(radii_px)],
        "radii_px": radii_px,
        "center_px": center_px,
        "orbital_radius_px": orbital_radius_px,
        "line_alpha": line_alpha,
        "line_width_px": config.get("line_width_px", 1),
        "postprocess_only": True,
        "sampled_render_telemetry": False,
    }


def safe_float(value, fallback: float = 0.0) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return fallback


def effective_temporal_modulation(meta: dict) -> float:
    if int(safe_float(meta.get("time_enabled", 0.0))) == 0:
        return 1.0
    depth = max(0.0, min(1.0, safe_float(meta.get("modulation_depth", 0.0))))
    phase = safe_float(meta.get("phase", 0.0))
    return max(0.0, 1.0 + (depth * math.sin(phase)))


def scalar_at_radius_px(radius_px: float, orbital_radius_px: float, meta: dict, mode: str) -> float:
    density = math.exp((-2.0 * max(0.0, radius_px)) / max(1e-6, orbital_radius_px))
    density = max(0.0, min(1.0, density * effective_temporal_modulation(meta)))
    if mode == "curvature":
        return max(0.0, safe_float(meta.get("curvature_strength", 0.0))) * density
    return density


def radius_px_for_scalar(value: float, orbital_radius_px: float, meta: dict, mode: str) -> float:
    base = effective_temporal_modulation(meta)
    if mode == "curvature":
        base *= max(0.0, safe_float(meta.get("curvature_strength", 0.0)))
    base = max(base, 1e-9)
    normalized = max(1e-9, min(1.0, value / base))
    return -0.5 * orbital_radius_px * math.log(normalized)


def load_fonts(ImageFont):
    try:
        return (
            ImageFont.truetype("DejaVuSans.ttf", 12),
            ImageFont.truetype("DejaVuSans.ttf", 10),
            ImageFont.truetype("DejaVuSans-Bold.ttf", 13),
        )
    except Exception:
        return None, None, None


def draw_contour_legend(draw, config: dict, font_small, font_bold) -> None:
    lines = [
        f"mode: {config['mode']}",
        f"levels: {config['levels']}",
        "postprocess overlay only",
    ]
    x, y = 10, 10
    box_w = 178
    box_h = 50
    draw.rectangle([x - 4, y - 4, x + box_w, y + box_h], fill=(5, 7, 12, int(config.get("legend_alpha", 150))))
    for idx, line in enumerate(lines):
        draw.text((x, y + idx * 15), line, fill=(235, 241, 250, 218), font=font_bold if idx == 0 else font_small)


def draw_contour_labels(draw, center_px: list[float], orbital_radius_px: float, font_small) -> None:
    cx, cy = center_px
    marker = (246, 240, 178, 185)
    draw.line([cx - 7, cy, cx + 7, cy], fill=marker, width=1)
    draw.line([cx, cy - 7, cx, cy + 7], fill=marker, width=1)
    draw.text((cx + 9, cy + 5), "field center", fill=marker, font=font_small)
    draw.text((cx + orbital_radius_px + 6, cy - 8), "r_orbital", fill=marker, font=font_small)


def build_contact_sheet(root: Path, images: dict[str, dict[str, Path]], metadata: dict[str, dict[str, dict]], diffs: dict[str, dict], Image, ImageDraw, ImageFont, contour_overlays: dict[str, dict[str, dict]] | None = None) -> str:
    panels: list[tuple[str, Path, dict, str]] = []
    for cell in CELLS:
        if cell in images["normal_rgb"]:
            path = Path(contour_overlays.get("normal_rgb", {}).get(cell, {}).get("image", images["normal_rgb"][cell])) if contour_overlays else images["normal_rgb"][cell]
            row_label = "normal_rgb contours" if contour_overlays and cell in contour_overlays.get("normal_rgb", {}) else "normal_rgb"
            panels.append((cell, path, metadata["normal_rgb"].get(cell, {}), row_label))
    for cell in CELLS:
        if cell in images["depth_heatmap"]:
            path = Path(contour_overlays.get("depth_heatmap", {}).get(cell, {}).get("image", images["depth_heatmap"][cell])) if contour_overlays else images["depth_heatmap"][cell]
            row_label = "depth_heatmap contours" if contour_overlays and cell in contour_overlays.get("depth_heatmap", {}) else "depth_heatmap"
            panels.append((cell, path, metadata["depth_heatmap"].get(cell, {}), row_label))
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
    font, font_small, font_bold = load_fonts(ImageFont)

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
    contours = summary.get("contours", {})
    if contours.get("enabled"):
        lines.extend([
            "## Contour Overlays",
            "",
            "Contours are analytic postprocess guides derived from fixture metadata, not sampled render telemetry.",
            "",
            f"- mode: `{contours.get('mode', 'density')}`",
            f"- levels: {contours.get('levels', 'na')}",
            "- note: postprocess overlay only",
            "",
        ])
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
