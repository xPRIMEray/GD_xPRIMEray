#!/usr/bin/env python3
"""Draw camera-centered Cartesian cross-section minimap overlays for xPRIMEray captures."""

from __future__ import annotations

import argparse
import csv
import json
import math
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont, ImageOps


def parse_bool(value: Any) -> bool:
    return str(value).strip().lower() in {"1", "true", "yes"}


def parse_int(value: Any, default: int = 0) -> int:
    try:
        if value in ("", None):
            return default
        return int(round(float(value)))
    except Exception:
        return default


def parse_float(value: Any, default: float = math.nan) -> float:
    try:
        if value in ("", None, "nan", "NaN"):
            return default
        return float(value)
    except Exception:
        return default


def parse_bbox(text: str | None) -> tuple[int, int, int, int] | None:
    if not text:
        return None
    parts = [int(round(float(p))) for p in text.replace(";", ",").split(",") if p.strip()]
    if len(parts) != 4:
        raise argparse.ArgumentTypeError("--roi-bbox must be x0,y0,x1,y1")
    x0, y0, x1, y1 = parts
    return min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)


def parse_panel_size(text: str, image_size: tuple[int, int]) -> tuple[int, int]:
    if not text or text == "auto":
        return image_size
    parts = text.lower().replace(",", "x").split("x")
    if len(parts) != 2:
        raise argparse.ArgumentTypeError("--panel-size must be WxH or auto")
    try:
        w = int(parts[0])
        h = int(parts[1])
    except ValueError as exc:
        raise argparse.ArgumentTypeError("--panel-size must be WxH or auto") from exc
    if w < 80 or h < 80:
        raise argparse.ArgumentTypeError("--panel-size dimensions must be >= 80")
    return w, h


def load_json(path: Path | None) -> dict[str, Any]:
    if path is None or not path.exists():
        return {}
    try:
        return json.loads(path.read_text())
    except Exception:
        return {}


def load_hit_rows(path: Path) -> dict[tuple[int, int], dict[str, Any]]:
    rows: dict[tuple[int, int], dict[str, Any]] = {}
    with path.open(newline="", encoding="utf-8-sig") as handle:
        for row in csv.DictReader(handle):
            x = parse_int(row.get("x"), -1)
            y = parse_int(row.get("y"), -1)
            if x < 0 or y < 0:
                continue
            rows[(x, y)] = {
                "x": x,
                "y": y,
                "had_hit": parse_bool(row.get("had_hit", "0")),
                "hit_pos_x": parse_float(row.get("hit_pos_x")),
                "hit_pos_y": parse_float(row.get("hit_pos_y")),
                "hit_pos_z": parse_float(row.get("hit_pos_z")),
                "normal_x": parse_float(row.get("normal_x"), 0.0),
                "normal_y": parse_float(row.get("normal_y"), 0.0),
                "normal_z": parse_float(row.get("normal_z"), 0.0),
                "collider_id": row.get("collider_id", row.get("current_collider_id", "")),
            }
    return rows


def draw_label(draw: ImageDraw.ImageDraw, xy: tuple[int, int], text: str, fill=(245, 248, 255, 255)) -> None:
    font = ImageFont.load_default()
    x, y = xy
    box = draw.textbbox((x, y), text, font=font)
    draw.rectangle((box[0] - 2, box[1] - 2, box[2] + 2, box[3] + 2), fill=(0, 0, 0, 180))
    draw.text((x, y), text, fill=fill, font=font)


def draw_panel_title(draw: ImageDraw.ImageDraw, title: str, subtitle: str = "") -> None:
    draw_label(draw, (8, 8), title)
    if subtitle:
        draw_label(draw, (8, 24), subtitle, (210, 220, 235, 255))


def in_bbox(x: int, y: int, bbox: tuple[int, int, int, int] | None) -> bool:
    if bbox is None:
        return True
    x0, y0, x1, y1 = bbox
    return x0 <= x <= x1 and y0 <= y <= y1


def finite(value: float) -> bool:
    return math.isfinite(value)


def select_slice_axes(
    rows: dict[tuple[int, int], dict[str, Any]],
    width: int,
    height: int,
    bbox: tuple[int, int, int, int] | None,
    target: str,
) -> tuple[int, int, dict[str, Any]]:
    if bbox:
        x0, y0, x1, y1 = bbox
        center_x = (x0 + x1) // 2
        center_y = (y0 + y1) // 2
    else:
        x0, y0, x1, y1 = 0, 0, width - 1, height - 1
        center_x = width // 2
        center_y = height // 2
    hit_pixels = [
        (x, y) for (x, y), row in rows.items()
        if row.get("had_hit") and x0 <= x <= x1 and y0 <= y <= y1
    ]
    selected_x, selected_y = center_x, center_y
    note = "center fallback"
    if target == "center":
        note = "image center" if bbox is None else "ROI center"
    elif target == "roi_centroid":
        note = "ROI center" if bbox else "image center fallback because no ROI was supplied"
    elif target == "hit_centroid" and hit_pixels:
        selected_x = int(round(sum(p[0] for p in hit_pixels) / len(hit_pixels)))
        selected_y = int(round(sum(p[1] for p in hit_pixels) / len(hit_pixels)))
        note = "centroid of hit pixels within ROI/image"
    elif target == "max_hit_row" and hit_pixels:
        counts: dict[int, int] = {}
        for _, y in hit_pixels:
            counts[y] = counts.get(y, 0) + 1
        selected_y = max(sorted(counts), key=lambda y: counts[y])
        note = "row with most hit pixels; column remains center/ROI center"
    elif target == "max_hit_col" and hit_pixels:
        counts: dict[int, int] = {}
        for x, _ in hit_pixels:
            counts[x] = counts.get(x, 0) + 1
        selected_x = max(sorted(counts), key=lambda x: counts[x])
        note = "column with most hit pixels; row remains center/ROI center"
    elif target in {"hit_centroid", "max_hit_row", "max_hit_col"}:
        note = f"{target} requested but no hit pixels were available; using center fallback"
    return selected_x, selected_y, {
        "slice_target": target,
        "selected_col": selected_x,
        "selected_row": selected_y,
        "hit_pixel_count_for_targeting": len(hit_pixels),
        "slice_target_note": note,
    }


def sample_slices(
    rows: dict[tuple[int, int], dict[str, Any]],
    width: int,
    height: int,
    bbox: tuple[int, int, int, int] | None,
    sample_stride: int,
    selected_col: int,
    selected_row: int,
) -> dict[str, list[dict[str, Any]]]:
    if bbox:
        x0, y0, x1, y1 = bbox
        xs = range(x0, x1 + 1)
        ys = range(y0, y1 + 1)
    else:
        xs = range(width)
        ys = range(height)
    horizontal: list[dict[str, Any]] = []
    vertical: list[dict[str, Any]] = []
    for x in xs:
        if x % sample_stride == 0:
            row = rows.get((x, selected_row))
            if row:
                horizontal.append(row)
    for y in ys:
        if y % sample_stride == 0:
            row = rows.get((selected_col, y))
            if row:
                vertical.append(row)
    return {"horizontal": horizontal, "vertical": vertical}


def slice_point(row: dict[str, Any], mode: str) -> tuple[float, float] | None:
    if not row.get("had_hit"):
        return None
    z = row["hit_pos_z"]
    if mode == "horizontal":
        a = row["hit_pos_x"]
    else:
        a = row["hit_pos_y"]
    if not finite(a) or not finite(z):
        return None
    return a, z


def normal_vector(row: dict[str, Any], mode: str) -> tuple[float, float]:
    if mode == "horizontal":
        return row["normal_x"], row["normal_z"]
    return row["normal_y"], row["normal_z"]


def compute_bounds(samples: dict[str, list[dict[str, Any]]], slice_mode: str) -> tuple[float, float, float, float]:
    points: list[tuple[float, float]] = [(0.0, 0.0)]
    modes = ["horizontal", "vertical"] if slice_mode == "both" else [slice_mode]
    for mode in modes:
        for row in samples[mode]:
            point = slice_point(row, mode)
            if point:
                points.append(point)
    if len(points) == 1:
        points.extend([(-1.0, 1.0), (1.0, 4.0)])
    min_a = min(p[0] for p in points)
    max_a = max(p[0] for p in points)
    min_z = min(p[1] for p in points)
    max_z = max(p[1] for p in points)
    pad_a = max(0.5, (max_a - min_a) * 0.15)
    pad_z = max(0.5, (max_z - min_z) * 0.15)
    return min_a - pad_a, max_a + pad_a, min_z - pad_z, max_z + pad_z


def map_point(a: float, z: float, bounds: tuple[float, float, float, float], size: int, margin: int) -> tuple[int, int]:
    min_a, max_a, min_z, max_z = bounds
    w = size - margin * 2
    h = size - margin * 2
    x = margin + int((a - min_a) / max(1e-9, max_a - min_a) * w)
    y = size - margin - int((z - min_z) / max(1e-9, max_z - min_z) * h)
    return x, y


def draw_grid(draw: ImageDraw.ImageDraw, size: int, margin: int) -> None:
    draw.rectangle((0, 0, size - 1, size - 1), fill=(10, 14, 20, 230), outline=(220, 230, 245, 180))
    for i in range(1, 6):
        x = margin + int((size - margin * 2) * i / 6)
        y = margin + int((size - margin * 2) * i / 6)
        draw.line((x, margin, x, size - margin), fill=(70, 82, 100, 120))
        draw.line((margin, y, size - margin, y), fill=(70, 82, 100, 120))
    draw.line((margin, size - margin, size - margin, size - margin), fill=(150, 160, 175, 160))
    draw.line((size // 2, margin, size // 2, size - margin), fill=(100, 130, 170, 130))


def draw_frustum(draw: ImageDraw.ImageDraw, bounds: tuple[float, float, float, float], size: int, margin: int) -> None:
    min_a, max_a, _, max_z = bounds
    origin = map_point(0.0, 0.0, bounds, size, margin)
    spread = max(abs(min_a), abs(max_a), 1.0)
    far_z = max(max_z, 1.0)
    left = map_point(-spread, far_z, bounds, size, margin)
    right = map_point(spread, far_z, bounds, size, margin)
    draw.polygon((origin, left, right), outline=(255, 220, 70, 185), fill=(255, 220, 70, 28))
    draw.ellipse((origin[0] - 3, origin[1] - 3, origin[0] + 3, origin[1] + 3), fill=(255, 240, 120, 230))


def draw_normal_glyph(draw: ImageDraw.ImageDraw, row: dict[str, Any], mode: str, center: tuple[int, int], color: tuple[int, int, int, int]) -> None:
    na, nz = normal_vector(row, mode)
    mag = math.hypot(na, nz)
    if mag <= 1e-8:
        return
    scale = 10.0
    ex = center[0] + int(na / mag * scale)
    ey = center[1] - int(nz / mag * scale)
    draw.line((center[0], center[1], ex, ey), fill=color, width=1)


def draw_minimap(samples: dict[str, list[dict[str, Any]]], args: argparse.Namespace, target_info: dict[str, Any]) -> tuple[Image.Image, dict[str, Any]]:
    size = args.minimap_size
    margin = max(24, int(size * 0.12))
    modes = ["horizontal", "vertical"] if args.slice_mode == "both" else [args.slice_mode]
    bounds = compute_bounds(samples, args.slice_mode)
    minimap = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(minimap)
    draw_grid(draw, size, margin)
    if args.draw_frustum:
        draw_frustum(draw, bounds, size, margin)
    colors = {
        "horizontal": (70, 220, 255, 230),
        "vertical": (255, 90, 210, 230),
    }
    hit_counts: dict[str, int] = {}
    sample_counts: dict[str, int] = {}
    for mode in modes:
        pts: list[tuple[int, int]] = []
        hit_counts[mode] = 0
        sample_counts[mode] = len(samples[mode])
        for row in samples[mode]:
            point = slice_point(row, mode)
            if not point:
                continue
            hit_counts[mode] += 1
            px = map_point(point[0], point[1], bounds, size, margin)
            pts.append(px)
            draw.ellipse((px[0] - 2, px[1] - 2, px[0] + 2, px[1] + 2), fill=colors[mode])
            if args.draw_hit_normals:
                draw_normal_glyph(draw, row, mode, px, (255, 255, 255, 200))
        if len(pts) >= 2:
            draw.line(pts, fill=colors[mode], width=1)
    draw_label(draw, (8, 8), f"cross-section: {target_info['slice_target']}")
    y = 24
    if "horizontal" in modes:
        draw_label(draw, (8, y), f"cyan: row {target_info['selected_row']} x/z", (70, 220, 255, 255))
        y += 16
    if "vertical" in modes:
        draw_label(draw, (8, y), f"magenta: col {target_info['selected_col']} y/z", (255, 90, 210, 255))
    summary = {
        "slice_mode": args.slice_mode,
        "minimap_size": size,
        "bounds": {
            "axis_min": round(bounds[0], 6),
            "axis_max": round(bounds[1], 6),
            "z_min": round(bounds[2], 6),
            "z_max": round(bounds[3], 6),
        },
        "sample_counts": sample_counts,
        "hit_counts": hit_counts,
        "slice_target": target_info["slice_target"],
        "selected_row": target_info["selected_row"],
        "selected_col": target_info["selected_col"],
        "hit_pixel_count_for_targeting": target_info["hit_pixel_count_for_targeting"],
        "slice_target_note": target_info["slice_target_note"],
        "camera_metadata_used": bool(args.camera_metadata),
        "camera_metadata_note": "camera metadata parsed for provenance only; minimap uses hit diagnostics coordinate values" if args.camera_metadata else "no camera metadata supplied; camera origin assumed at inset origin",
    }
    return minimap, summary


def composite_position(image_size: tuple[int, int], minimap_size: int, position: str, pad: int = 12) -> tuple[int, int]:
    w, h = image_size
    if position == "top-left":
        return pad, pad
    if position == "bottom-left":
        return pad, h - minimap_size - pad
    if position == "bottom-right":
        return w - minimap_size - pad, h - minimap_size - pad
    return w - minimap_size - pad, pad


def fit_panel(image: Image.Image, panel_size: tuple[int, int], background=(6, 8, 12, 255)) -> Image.Image:
    base = Image.new("RGBA", panel_size, background)
    fitted = ImageOps.contain(image.convert("RGBA"), panel_size)
    pos = ((panel_size[0] - fitted.width) // 2, (panel_size[1] - fitted.height) // 2)
    base.alpha_composite(fitted, pos)
    return base


def make_placeholder_panel(panel_size: tuple[int, int], title: str, message: str) -> Image.Image:
    panel = Image.new("RGBA", panel_size, (12, 16, 22, 255))
    draw = ImageDraw.Draw(panel)
    draw.rectangle((0, 0, panel_size[0] - 1, panel_size[1] - 1), outline=(80, 92, 112, 255))
    draw_panel_title(draw, title)
    lines = [message[i:i + 42] for i in range(0, len(message), 42)] or [message]
    y = panel_size[1] // 2 - len(lines) * 8
    for line in lines:
        draw_label(draw, (16, y), line, (220, 225, 235, 255))
        y += 18
    return panel


def normal_xy_for_panel(row: dict[str, Any]) -> tuple[float, float] | None:
    nx = parse_float(row.get("normal_x"), math.nan)
    nz = parse_float(row.get("normal_z"), math.nan)
    if not finite(nx) or not finite(nz):
        return None
    mag = math.hypot(nx, nz)
    if mag <= 1e-8:
        return None
    return nx / mag, -nz / mag


def make_hit_normal_panel(
    image: Image.Image,
    rows: dict[tuple[int, int], dict[str, Any]],
    panel_size: tuple[int, int],
    sample_stride: int,
    roi_bbox: tuple[int, int, int, int] | None,
) -> tuple[Image.Image, dict[str, Any]]:
    panel = fit_panel(image, panel_size)
    draw = ImageDraw.Draw(panel)
    fit_scale = min(panel_size[0] / max(1, image.width), panel_size[1] / max(1, image.height))
    fitted_w = int(round(image.width * fit_scale))
    fitted_h = int(round(image.height * fit_scale))
    offset_x = (panel_size[0] - fitted_w) // 2
    offset_y = (panel_size[1] - fitted_h) // 2
    scale = max(8.0, min(panel_size) * 0.055)
    sampled = hits = no_hits = vectors = degenerate = 0
    for (x, y), row in rows.items():
        if x % sample_stride != 0 or y % sample_stride != 0 or not in_bbox(x, y, roi_bbox):
            continue
        sampled += 1
        px = offset_x + int(round(x * fit_scale))
        py = offset_y + int(round(y * fit_scale))
        if not row.get("had_hit"):
            no_hits += 1
            draw.line((px - 3, py - 3, px + 3, py + 3), fill=(170, 170, 170, 180), width=1)
            draw.line((px - 3, py + 3, px + 3, py - 3), fill=(170, 170, 170, 180), width=1)
            continue
        hits += 1
        unit = normal_xy_for_panel(row)
        if not unit:
            degenerate += 1
            draw.ellipse((px - 2, py - 2, px + 2, py + 2), fill=(70, 255, 120, 210))
            continue
        ex = int(round(px + unit[0] * scale))
        ey = int(round(py + unit[1] * scale))
        draw.line((px, py, ex, ey), fill=(60, 255, 120, 235), width=2)
        draw.ellipse((px - 1, py - 1, px + 1, py + 1), fill=(255, 255, 255, 220))
        vectors += 1
    if roi_bbox:
        x0, y0, x1, y1 = roi_bbox
        draw.rectangle(
            (
                offset_x + int(x0 * fit_scale),
                offset_y + int(y0 * fit_scale),
                offset_x + int(x1 * fit_scale),
                offset_y + int(y1 * fit_scale),
            ),
            outline=(255, 230, 70, 220),
            width=1,
        )
    draw_panel_title(draw, "B: hit normals", "green: normal x/z projection")
    return panel, {
        "sampled": sampled,
        "hit_count": hits,
        "no_hit_count": no_hits,
        "vectors_drawn": vectors,
        "projection_degenerate_count": degenerate,
        "sample_stride": sample_stride,
    }


def find_transport_graph_overlay(args: argparse.Namespace) -> Path | None:
    names = [
        "ownership_graph_seam_map.png",
        "ownership_graph_node_map.png",
        "layer2_transport_ownership.png",
        "transport_shape_regions_overlay.png",
        "combined_diagnostic_overlay.png",
        "unstable_subgraph_overlay.png",
    ]
    search_roots = []
    for root in [args.out, args.image.parent, args.hit_csv.parent]:
        if root not in search_roots:
            search_roots.append(root)
    for root in search_roots:
        for name in names:
            candidate = root / name
            if candidate.exists():
                return candidate
    return None


def make_transport_panel(args: argparse.Namespace, panel_size: tuple[int, int]) -> tuple[Image.Image, dict[str, Any]]:
    if args.include_field_arrows and args.include_field_arrows.exists():
        panel = make_placeholder_panel(
            panel_size,
            "D: transport / field",
            f"field arrows input loaded: {args.include_field_arrows.name}",
        )
        return panel, {"source": str(args.include_field_arrows), "mode": "field_arrows_placeholder"}
    graph_overlay = find_transport_graph_overlay(args)
    if graph_overlay:
        with Image.open(graph_overlay) as graph_image:
            panel = fit_panel(graph_image.convert("RGBA"), panel_size)
        draw = ImageDraw.Draw(panel)
        draw_panel_title(draw, "D: transport / field", graph_overlay.name)
        return panel, {"source": str(graph_overlay), "mode": "ownership_or_seam_overlay"}
    return make_placeholder_panel(panel_size, "D: transport / field", "field arrows / GRIN overlay unavailable"), {
        "source": "",
        "mode": "placeholder",
    }


def make_quad_panel(
    image: Image.Image,
    minimap: Image.Image,
    rows: dict[tuple[int, int], dict[str, Any]],
    args: argparse.Namespace,
) -> tuple[Image.Image, dict[str, Any]]:
    panel_size = parse_panel_size(args.panel_size, image.size)
    panels: list[Image.Image] = []
    summary: dict[str, Any] = {"panel_size": list(panel_size)}

    if args.include_original:
        panel_a = fit_panel(image, panel_size)
        draw_panel_title(ImageDraw.Draw(panel_a), "A: rendered frame")
    else:
        panel_a = make_placeholder_panel(panel_size, "A: rendered frame", "disabled")
    panels.append(panel_a)

    if args.include_hit_normals:
        panel_b, normal_summary = make_hit_normal_panel(image, rows, panel_size, max(1, args.sample_stride), args.roi_bbox)
        summary["hit_normal_panel"] = normal_summary
    else:
        panel_b = make_placeholder_panel(panel_size, "B: hit normals", "disabled")
        summary["hit_normal_panel"] = {"enabled": False}
    panels.append(panel_b)

    if args.include_cross_section:
        panel_c = fit_panel(minimap, panel_size)
        draw_panel_title(ImageDraw.Draw(panel_c), "C: cross-section minimap")
    else:
        panel_c = make_placeholder_panel(panel_size, "C: cross-section minimap", "disabled")
    panels.append(panel_c)

    if args.include_transport_graph:
        panel_d, transport_summary = make_transport_panel(args, panel_size)
        summary["transport_panel"] = transport_summary
    else:
        panel_d = make_placeholder_panel(panel_size, "D: transport / field", "disabled")
        summary["transport_panel"] = {"enabled": False}
    panels.append(panel_d)

    sheet = Image.new("RGBA", (panel_size[0] * 2, panel_size[1] * 2), (0, 0, 0, 255))
    positions = [(0, 0), (panel_size[0], 0), (0, panel_size[1]), (panel_size[0], panel_size[1])]
    for panel, pos in zip(panels, positions):
        sheet.alpha_composite(panel, pos)
    return sheet, summary


def analyze(args: argparse.Namespace) -> int:
    out = args.out
    out.mkdir(parents=True, exist_ok=True)
    image = Image.open(args.image).convert("RGBA")
    rows = load_hit_rows(args.hit_csv)
    selected_col, selected_row, target_info = select_slice_axes(rows, image.width, image.height, args.roi_bbox, args.slice_target)
    samples = sample_slices(rows, image.width, image.height, args.roi_bbox, args.sample_stride, selected_col, selected_row)
    minimap, summary = draw_minimap(samples, args, target_info)
    minimap.save(out / "camera_cross_section_minimap.png")
    frame = image.copy()
    if args.minimap:
        pos = composite_position(image.size, args.minimap_size, args.minimap_position)
        frame.alpha_composite(minimap, pos)
    if args.roi_bbox:
        draw = ImageDraw.Draw(frame)
        x0, y0, x1, y1 = args.roi_bbox
        draw.rectangle((x0, y0, x1, y1), outline=(255, 230, 70, 220), width=1)
    frame.save(out / "diagnostic_frame_with_minimap.png")
    quad_summary: dict[str, Any] = {}
    if args.layout == "quad_panel":
        quad_panel, quad_summary = make_quad_panel(image, minimap, rows, args)
        quad_panel.save(out / "diagnostic_quad_panel.png")
    summary.update({
        "image": str(args.image),
        "hit_csv": str(args.hit_csv),
        "camera_metadata": str(args.camera_metadata) if args.camera_metadata else "",
        "roi_bbox": list(args.roi_bbox) if args.roi_bbox else "",
        "layout": args.layout,
        "quad_panel_output": str(out / "diagnostic_quad_panel.png") if args.layout == "quad_panel" else "",
        "quad_panel": quad_summary,
        "include_original": bool(args.include_original),
        "include_hit_normals": bool(args.include_hit_normals),
        "include_cross_section": bool(args.include_cross_section),
        "include_transport_graph": bool(args.include_transport_graph),
        "include_field_arrows": str(args.include_field_arrows) if args.include_field_arrows else "",
        "sample_stride": args.sample_stride,
        "minimap_position": args.minimap_position,
        "draw_frustum": bool(args.draw_frustum),
        "draw_hit_normals": bool(args.draw_hit_normals),
        "post_process_only": True,
    })
    if args.camera_metadata:
        summary["camera_metadata_keys"] = sorted(load_json(args.camera_metadata).keys())
    (out / "camera_cross_section_summary.json").write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n")
    print(f"[camera-cross-section-minimap] out={out} samples={summary['sample_counts']} hits={summary['hit_counts']}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--image", type=Path, required=True)
    parser.add_argument("--hit-csv", type=Path, required=True)
    parser.add_argument("--camera-metadata", type=Path, default=None)
    parser.add_argument("--roi-bbox", type=parse_bbox, default=None)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--minimap", type=int, default=1)
    parser.add_argument("--minimap-position", choices=["top-right", "top-left", "bottom-right", "bottom-left"], default="top-right")
    parser.add_argument("--minimap-size", type=int, default=140)
    parser.add_argument("--layout", choices=["overlay", "quad_panel"], default="overlay")
    parser.add_argument("--panel-size", default="auto")
    parser.add_argument("--include-original", type=int, default=1)
    parser.add_argument("--include-hit-normals", type=int, default=1)
    parser.add_argument("--include-cross-section", type=int, default=1)
    parser.add_argument("--include-transport-graph", type=int, default=1)
    parser.add_argument("--include-field-arrows", type=Path, default=None)
    parser.add_argument("--slice-mode", choices=["horizontal", "vertical", "both"], default="both")
    parser.add_argument("--slice-target", choices=["center", "hit_centroid", "roi_centroid", "max_hit_row", "max_hit_col"], default="center")
    parser.add_argument("--grid-scale", default="auto")
    parser.add_argument("--draw-frustum", type=int, default=1)
    parser.add_argument("--draw-hit-normals", type=int, default=1)
    parser.add_argument("--sample-stride", type=int, default=4)
    args = parser.parse_args()
    if args.minimap_size < 80:
        raise SystemExit("--minimap-size must be >= 80")
    if args.sample_stride <= 0:
        raise SystemExit("--sample-stride must be > 0")
    if args.grid_scale != "auto":
        raise SystemExit("Only --grid-scale auto is supported in this post-process minimap.")
    parse_panel_size(args.panel_size, (320, 180))
    return analyze(args)


if __name__ == "__main__":
    raise SystemExit(main())
