#!/usr/bin/env python3
"""Draw human-visible hit-normal vector glyphs over an xPRIMEray capture."""

from __future__ import annotations

import argparse
import csv
import json
import math
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont


def parse_bool(value: Any) -> bool:
    return str(value).strip().lower() in {"1", "true", "yes"}


def parse_int(value: Any, default: int = 0) -> int:
    try:
        if value in ("", None):
            return default
        return int(round(float(value)))
    except Exception:
        return default


def parse_float(value: Any, default: float = 0.0) -> float:
    try:
        if value in ("", None, "nan", "NaN"):
            return default
        return float(value)
    except Exception:
        return default


def parse_optional_float(value: Any) -> float | None:
    try:
        if value in ("", None, "nan", "NaN"):
            return None
        value = float(value)
        return value if math.isfinite(value) else None
    except Exception:
        return None


def parse_bbox(text: str | None) -> tuple[int, int, int, int] | None:
    if not text:
        return None
    parts = [int(round(float(part))) for part in text.replace(";", ",").split(",") if part.strip()]
    if len(parts) != 4:
        raise argparse.ArgumentTypeError("--roi-bbox must be x0,y0,x1,y1")
    x0, y0, x1, y1 = parts
    return min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)


def in_bbox(x: int, y: int, bbox: tuple[int, int, int, int] | None) -> bool:
    if bbox is None:
        return True
    x0, y0, x1, y1 = bbox
    return x0 <= x <= x1 and y0 <= y <= y1


def load_hit_packet(path: Path) -> tuple[dict[tuple[int, int], dict[str, Any]], list[str]]:
    rows: dict[tuple[int, int], dict[str, Any]] = {}
    with path.open(newline="", encoding="utf-8-sig") as handle:
        reader = csv.DictReader(handle)
        fieldnames = list(reader.fieldnames or [])
        for row in reader:
            x = parse_int(row.get("x"), -1)
            y = parse_int(row.get("y"), -1)
            if x < 0 or y < 0:
                continue
            nx = parse_optional_float(row.get("normal_x"))
            ny = parse_optional_float(row.get("normal_y"))
            nz = parse_optional_float(row.get("normal_z"))
            normal_valid = nx is not None and ny is not None and nz is not None
            rows[(x, y)] = {
                "x": x,
                "y": y,
                "had_hit": parse_bool(row.get("had_hit", "0")),
                "hit_class": row.get("hit_class", ""),
                "collider_id": row.get("collider_id", row.get("current_collider_id", "")),
                "normal_x": nx if nx is not None else 0.0,
                "normal_y": ny if ny is not None else 0.0,
                "normal_z": nz if nz is not None else 0.0,
                "normal_valid": normal_valid,
            }
    return rows, fieldnames


def load_hit_rows(path: Path) -> dict[tuple[int, int], dict[str, Any]]:
    rows, _ = load_hit_packet(path)
    return rows


def draw_label(draw: ImageDraw.ImageDraw, xy: tuple[int, int], text: str, fill=(255, 255, 255, 255)) -> None:
    font = ImageFont.load_default()
    x, y = xy
    box = draw.textbbox((x, y), text, font=font)
    draw.rectangle((box[0] - 2, box[1] - 2, box[2] + 2, box[3] + 2), fill=(0, 0, 0, 190))
    draw.text((x, y), text, fill=fill, font=font)


def draw_arrow(draw: ImageDraw.ImageDraw, start: tuple[float, float], end: tuple[float, float], fill: tuple[int, int, int, int], width: int = 1) -> None:
    sx, sy = start
    ex, ey = end
    draw.line((sx, sy, ex, ey), fill=fill, width=width)
    dx, dy = ex - sx, ey - sy
    length = math.hypot(dx, dy)
    if length <= 1e-6:
        return
    ux, uy = dx / length, dy / length
    size = max(3.0, min(7.0, length * 0.35))
    px, py = -uy, ux
    p1 = (ex - ux * size + px * size * 0.55, ey - uy * size + py * size * 0.55)
    p2 = (ex - ux * size - px * size * 0.55, ey - uy * size - py * size * 0.55)
    draw.polygon((end, p1, p2), fill=fill)


def projected_components(row: dict[str, Any], projection: str) -> tuple[float, float]:
    if projection == "xz":
        return row["normal_x"], row["normal_z"]
    if projection == "yz":
        return row["normal_y"], row["normal_z"]
    return row["normal_x"], row["normal_y"]


def screen_vector(row: dict[str, Any], scale: float, mode: str, projection: str, flip_y: bool, min_projected_magnitude: float) -> tuple[float, float, float]:
    sx, sy = projected_components(row, projection)
    mag_xy = math.hypot(sx, sy)
    screen_y = -sy if flip_y else sy
    if mag_xy < min_projected_magnitude:
        return 0.0, 0.0, mag_xy
    if mode == "fixed":
        return (sx / mag_xy) * scale, (screen_y / mag_xy) * scale, mag_xy
    return sx * scale, screen_y * scale, mag_xy


def draw_no_hit(draw: ImageDraw.ImageDraw, x: int, y: int) -> None:
    color = (180, 180, 190, 155)
    draw.line((x - 2, y - 2, x + 2, y + 2), fill=color, width=1)
    draw.line((x - 2, y + 2, x + 2, y - 2), fill=color, width=1)


def draw_hit_sample(draw: ImageDraw.ImageDraw, x: int, y: int) -> None:
    draw.ellipse((x - 1, y - 1, x + 1, y + 1), fill=(80, 220, 255, 210))


def draw_degenerate_normal(draw: ImageDraw.ImageDraw, x: int, y: int) -> None:
    draw.ellipse((x - 3, y - 3, x + 3, y + 3), outline=(255, 230, 60, 240), fill=(255, 230, 60, 150), width=1)


def normal_stats(rows: dict[tuple[int, int], dict[str, Any]], projection: str) -> dict[str, Any]:
    valid = [row for row in rows.values() if row.get("normal_valid")]
    valid_hits = [row for row in valid if row.get("had_hit")]
    stats: dict[str, Any] = {
        "row_count": len(rows),
        "rows_with_valid_normal_xyz": len(valid),
        "hit_rows_with_valid_normal_xyz": len(valid_hits),
        "normal_x": axis_stats([row["normal_x"] for row in valid]),
        "normal_y": axis_stats([row["normal_y"] for row in valid]),
        "normal_z": axis_stats([row["normal_z"] for row in valid]),
        "hit_normal_x": axis_stats([row["normal_x"] for row in valid_hits]),
        "hit_normal_y": axis_stats([row["normal_y"] for row in valid_hits]),
        "hit_normal_z": axis_stats([row["normal_z"] for row in valid_hits]),
    }
    mags = [math.hypot(*projected_components(row, projection)) for row in valid]
    hit_mags = [math.hypot(*projected_components(row, projection)) for row in valid_hits]
    stats["projected_xy_magnitude_histogram"] = projected_magnitude_histogram(mags)
    stats["projected_magnitude_histogram"] = stats["projected_xy_magnitude_histogram"]
    stats["hit_projected_xy_magnitude_histogram"] = projected_magnitude_histogram(hit_mags)
    return stats


def axis_stats(values: list[float]) -> dict[str, Any]:
    if not values:
        return {"min": "", "max": "", "mean": ""}
    return {
        "min": round(min(values), 6),
        "max": round(max(values), 6),
        "mean": round(sum(values) / len(values), 6),
    }


def projected_magnitude_histogram(values: list[float]) -> dict[str, int]:
    bins = {
        "[0,1e-6)": 0,
        "[1e-6,1e-4)": 0,
        "[1e-4,1e-3)": 0,
        "[1e-3,1e-2)": 0,
        "[1e-2,1e-1)": 0,
        "[1e-1,0.5)": 0,
        "[0.5,1.0]": 0,
        ">1.0": 0,
    }
    for value in values:
        if value < 1e-6:
            bins["[0,1e-6)"] += 1
        elif value < 1e-4:
            bins["[1e-6,1e-4)"] += 1
        elif value < 1e-3:
            bins["[1e-4,1e-3)"] += 1
        elif value < 1e-2:
            bins["[1e-3,1e-2)"] += 1
        elif value < 1e-1:
            bins["[1e-2,1e-1)"] += 1
        elif value < 0.5:
            bins["[1e-1,0.5)"] += 1
        elif value <= 1.0:
            bins["[0.5,1.0]"] += 1
        else:
            bins[">1.0"] += 1
    return bins


def analyze(args: argparse.Namespace) -> int:
    image_path = args.image
    hit_csv = args.hit_csv
    out = args.out
    out.mkdir(parents=True, exist_ok=True)
    bbox = args.roi_bbox

    image = Image.open(image_path).convert("RGBA")
    overlay = image.copy()
    draw = ImageDraw.Draw(overlay)
    rows, fieldnames = load_hit_packet(hit_csv)
    compare_rows = load_hit_rows(args.compare_csv) if args.compare_csv else {}
    normal_field_names_found = [name for name in fieldnames if "normal" in name.lower()]
    csv_normal_stats = normal_stats(rows, args.projection)

    sample_count = 0
    hit_count = 0
    no_hit_count = 0
    sampled_hit_pixels_marked = 0
    compare_count = 0
    degenerate_projected_normals = 0
    vectors_drawn = 0
    valid_sampled_hit_normals = 0
    max_delta_angle = 0.0
    delta_angle_sum = 0.0
    sampled_projected_magnitudes: list[float] = []

    for (x, y), row in sorted(rows.items()):
        if not in_bbox(x, y, bbox):
            continue
        if (x % args.stride) != 0 or (y % args.stride) != 0:
            continue
        sample_count += 1
        if not row["had_hit"]:
            no_hit_count += 1
            if args.mark_no_hit:
                draw_no_hit(draw, x, y)
            continue
        hit_count += 1
        draw_hit_sample(draw, x, y)
        sampled_hit_pixels_marked += 1
        if row.get("normal_valid"):
            valid_sampled_hit_normals += 1
        dx, dy, mag_xy = screen_vector(row, args.scale, args.mode, args.projection, bool(args.flip_y), args.min_projected_magnitude)
        sampled_projected_magnitudes.append(mag_xy)
        if mag_xy < args.min_projected_magnitude:
            degenerate_projected_normals += 1
            draw_degenerate_normal(draw, x, y)
        else:
            draw_arrow(draw, (x, y), (x + dx, y + dy), (40, 245, 120, 220), width=args.width)
            vectors_drawn += 1

        other = compare_rows.get((x, y))
        if other and other["had_hit"]:
            compare_count += 1
            bdx, bdy, _ = screen_vector(other, args.scale, args.mode, args.projection, bool(args.flip_y), args.min_projected_magnitude)
            ddx, ddy = bdx - dx, bdy - dy
            if math.hypot(ddx, ddy) > 1e-6:
                draw_arrow(draw, (x, y), (x + ddx, y + ddy), (255, 80, 220, 225), width=max(1, args.width))
            angle = normal_angle(row, other)
            max_delta_angle = max(max_delta_angle, angle)
            delta_angle_sum += angle

    if bbox:
        x0, y0, x1, y1 = bbox
        draw.rectangle((x0, y0, x1, y1), outline=(255, 230, 60, 220), width=1)
    draw_legend(draw, image.size, args, compare=bool(args.compare_csv))
    overlay.save(out / "hit_normal_vector_overlay.png")

    summary = {
        "image": str(image_path),
        "hit_csv": str(hit_csv),
        "compare_csv": str(args.compare_csv) if args.compare_csv else "",
        "sample_count": sample_count,
        "hit_count": hit_count,
        "no_hit_count": no_hit_count,
        "sampled_hit_pixels_marked": sampled_hit_pixels_marked,
        "compare_sample_count": compare_count,
        "normal_field_names_found": normal_field_names_found,
        "rows_with_valid_normal_xyz": csv_normal_stats["rows_with_valid_normal_xyz"],
        "hit_rows_with_valid_normal_xyz": csv_normal_stats["hit_rows_with_valid_normal_xyz"],
        "sampled_hit_rows_with_valid_normal_xyz": valid_sampled_hit_normals,
        "normal_x_stats": csv_normal_stats["normal_x"],
        "normal_y_stats": csv_normal_stats["normal_y"],
        "normal_z_stats": csv_normal_stats["normal_z"],
        "hit_normal_x_stats": csv_normal_stats["hit_normal_x"],
        "hit_normal_y_stats": csv_normal_stats["hit_normal_y"],
        "hit_normal_z_stats": csv_normal_stats["hit_normal_z"],
        "projected_xy_magnitude_histogram": csv_normal_stats["projected_xy_magnitude_histogram"],
        "hit_projected_xy_magnitude_histogram": csv_normal_stats["hit_projected_xy_magnitude_histogram"],
        "sampled_projected_xy_magnitude_histogram": projected_magnitude_histogram(sampled_projected_magnitudes),
        "projection_degenerate_normal_count": degenerate_projected_normals,
        "camera_facing_or_projection_degenerate_count": degenerate_projected_normals,
        "vectors_drawn": vectors_drawn,
        "stride": args.stride,
        "scale": args.scale,
        "mode": args.mode,
        "projection": args.projection,
        "flip_y": bool(args.flip_y),
        "min_projected_magnitude": args.min_projected_magnitude,
        "roi_bbox": list(bbox) if bbox else "",
        "max_compare_normal_angle_delta_deg": round(max_delta_angle, 6),
        "mean_compare_normal_angle_delta_deg": round(delta_angle_sum / compare_count, 6) if compare_count else "",
        "post_process_only": True,
    }
    (out / "hit_normal_vector_overlay_summary.json").write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n")
    write_legend(out / "hit_normal_vector_overlay_legend.md", summary)
    if args.debug_normals:
        print(json.dumps({
            "normal_field_names_found": normal_field_names_found,
            "rows_with_valid_normal_xyz": csv_normal_stats["rows_with_valid_normal_xyz"],
            "hit_rows_with_valid_normal_xyz": csv_normal_stats["hit_rows_with_valid_normal_xyz"],
            "normal_x_stats": csv_normal_stats["normal_x"],
            "normal_y_stats": csv_normal_stats["normal_y"],
            "normal_z_stats": csv_normal_stats["normal_z"],
            "hit_normal_x_stats": csv_normal_stats["hit_normal_x"],
            "hit_normal_y_stats": csv_normal_stats["hit_normal_y"],
            "hit_normal_z_stats": csv_normal_stats["hit_normal_z"],
            "projected_xy_magnitude_histogram": csv_normal_stats["projected_xy_magnitude_histogram"],
            "hit_projected_xy_magnitude_histogram": csv_normal_stats["hit_projected_xy_magnitude_histogram"],
            "sampled_projected_xy_magnitude_histogram": summary["sampled_projected_xy_magnitude_histogram"],
            "vectors_drawn": vectors_drawn,
            "projection_degenerate_normal_count": degenerate_projected_normals,
        }, indent=2, sort_keys=True))
    print(f"[hit-normal-vector-overlay] samples={sample_count} hits={hit_count} no_hit={no_hit_count} vectors={vectors_drawn} degenerate={degenerate_projected_normals} out={out}")
    return 0


def normal_angle(a: dict[str, Any], b: dict[str, Any]) -> float:
    ax, ay, az = a["normal_x"], a["normal_y"], a["normal_z"]
    bx, by, bz = b["normal_x"], b["normal_y"], b["normal_z"]
    la = math.sqrt(ax * ax + ay * ay + az * az)
    lb = math.sqrt(bx * bx + by * by + bz * bz)
    if la <= 1e-9 or lb <= 1e-9:
        return 0.0
    dot = max(-1.0, min(1.0, (ax * bx + ay * by + az * bz) / (la * lb)))
    return math.degrees(math.acos(dot))


def draw_legend(draw: ImageDraw.ImageDraw, size: tuple[int, int], args: argparse.Namespace, compare: bool) -> None:
    x, y = 8, 8
    draw_label(draw, (x, y), f"hit normal vectors: stride={args.stride} scale={args.scale:g} mode={args.mode} projection={args.projection}")
    y += 18
    draw_arrow(draw, (x + 4, y + 8), (x + 28, y + 8), (40, 245, 120, 220), width=args.width)
    draw_label(draw, (x + 36, y), "green: drawn normal vector")
    y += 18
    draw.ellipse((x + 11, y + 5, x + 17, y + 11), outline=(255, 230, 60, 240), fill=(255, 230, 60, 150), width=1)
    draw_label(draw, (x + 36, y), "yellow dot: projection-degenerate normal")
    y += 18
    draw_no_hit(draw, x + 14, y + 8)
    draw_label(draw, (x + 36, y), "gray x: no hit sample")
    if compare:
        y += 18
        draw_arrow(draw, (x + 4, y + 8), (x + 28, y + 8), (255, 80, 220, 225), width=args.width)
        draw_label(draw, (x + 36, y), "magenta: compare normal delta")


def write_legend(path: Path, summary: dict[str, Any]) -> None:
    lines = [
        "# Hit Normal Vector Overlay Legend",
        "",
        "Post-process inspection overlay only. This does not modify renderer behavior and does not replace RGB normal shading.",
        "",
        "- Cyan dots: sampled hit pixels.",
        "- Green arrows: drawn screen-projected normal vectors.",
        "- Gray X markers: sampled no-hit pixels, when enabled.",
        "- Yellow dots: hit pixels whose selected projected normal magnitude is below `min_projected_magnitude`.",
        "- Yellow rectangle: ROI bbox, when provided.",
        "- Magenta arrows: before/after projected normal delta when `--compare-csv` is provided.",
        "",
        "## Summary",
        "",
    ]
    for key, value in summary.items():
        lines.append(f"- {key}: {value}")
    path.write_text("\n".join(lines) + "\n")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--image", type=Path, required=True)
    parser.add_argument("--hit-csv", type=Path, required=True)
    parser.add_argument("--compare-csv", type=Path, default=None)
    parser.add_argument("--roi-bbox", type=parse_bbox, default=None)
    parser.add_argument("--stride", type=int, default=8)
    parser.add_argument("--scale", type=float, default=12.0)
    parser.add_argument("--mode", choices=["fixed", "magnitude"], default="fixed")
    parser.add_argument("--projection", choices=["xy", "xz", "yz"], default="xy")
    parser.add_argument("--flip-y", type=int, default=1)
    parser.add_argument("--min-projected-magnitude", type=float, default=1e-6)
    parser.add_argument("--debug-normals", type=int, default=0)
    parser.add_argument("--width", type=int, default=1)
    parser.add_argument("--mark-no-hit", type=int, default=1)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    if args.stride <= 0:
        raise SystemExit("--stride must be > 0")
    if args.scale <= 0:
        raise SystemExit("--scale must be > 0")
    if args.min_projected_magnitude < 0:
        raise SystemExit("--min-projected-magnitude must be >= 0")
    return analyze(args)


if __name__ == "__main__":
    raise SystemExit(main())
