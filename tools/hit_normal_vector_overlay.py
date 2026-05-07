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
                "hit_class": row.get("hit_class", ""),
                "collider_id": row.get("collider_id", row.get("current_collider_id", "")),
                "normal_x": parse_float(row.get("normal_x"), 0.0),
                "normal_y": parse_float(row.get("normal_y"), 0.0),
                "normal_z": parse_float(row.get("normal_z"), 0.0),
            }
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


def screen_vector(nx: float, ny: float, scale: float, mode: str) -> tuple[float, float, float]:
    mag_xy = math.hypot(nx, ny)
    if mode == "fixed":
        if mag_xy <= 1e-9:
            return 0.0, 0.0, 0.0
        return (nx / mag_xy) * scale, (-ny / mag_xy) * scale, mag_xy
    return nx * scale, -ny * scale, mag_xy


def draw_no_hit(draw: ImageDraw.ImageDraw, x: int, y: int) -> None:
    color = (180, 180, 190, 155)
    draw.line((x - 2, y - 2, x + 2, y + 2), fill=color, width=1)
    draw.line((x - 2, y + 2, x + 2, y - 2), fill=color, width=1)


def analyze(args: argparse.Namespace) -> int:
    image_path = args.image
    hit_csv = args.hit_csv
    out = args.out
    out.mkdir(parents=True, exist_ok=True)
    bbox = args.roi_bbox

    image = Image.open(image_path).convert("RGBA")
    overlay = image.copy()
    draw = ImageDraw.Draw(overlay)
    rows = load_hit_rows(hit_csv)
    compare_rows = load_hit_rows(args.compare_csv) if args.compare_csv else {}

    sample_count = 0
    hit_count = 0
    no_hit_count = 0
    compare_count = 0
    zero_projected_normals = 0
    max_delta_angle = 0.0
    delta_angle_sum = 0.0

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
        dx, dy, mag_xy = screen_vector(row["normal_x"], row["normal_y"], args.scale, args.mode)
        if mag_xy <= 1e-9:
            zero_projected_normals += 1
            draw.ellipse((x - 2, y - 2, x + 2, y + 2), outline=(255, 230, 60, 190), width=1)
        else:
            draw_arrow(draw, (x, y), (x + dx, y + dy), (40, 245, 120, 220), width=args.width)

        other = compare_rows.get((x, y))
        if other and other["had_hit"]:
            compare_count += 1
            bdx, bdy, _ = screen_vector(other["normal_x"], other["normal_y"], args.scale, args.mode)
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
        "compare_sample_count": compare_count,
        "zero_projected_normal_count": zero_projected_normals,
        "stride": args.stride,
        "scale": args.scale,
        "mode": args.mode,
        "roi_bbox": list(bbox) if bbox else "",
        "max_compare_normal_angle_delta_deg": round(max_delta_angle, 6),
        "mean_compare_normal_angle_delta_deg": round(delta_angle_sum / compare_count, 6) if compare_count else "",
        "post_process_only": True,
    }
    (out / "hit_normal_vector_overlay_summary.json").write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n")
    write_legend(out / "hit_normal_vector_overlay_legend.md", summary)
    print(f"[hit-normal-vector-overlay] samples={sample_count} hits={hit_count} no_hit={no_hit_count} out={out}")
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
    draw_label(draw, (x, y), f"hit normal vectors: stride={args.stride} scale={args.scale:g} mode={args.mode}")
    y += 18
    draw_arrow(draw, (x + 4, y + 8), (x + 28, y + 8), (40, 245, 120, 220), width=args.width)
    draw_label(draw, (x + 36, y), "green: normal x/y projection")
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
        "- Green arrows: screen-projected `(normal_x, normal_y)` at sampled hit pixels.",
        "- Gray X markers: sampled no-hit pixels, when enabled.",
        "- Yellow dots: hit pixels whose projected x/y normal length is near zero.",
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
    parser.add_argument("--width", type=int, default=1)
    parser.add_argument("--mark-no-hit", type=int, default=1)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    if args.stride <= 0:
        raise SystemExit("--stride must be > 0")
    if args.scale <= 0:
        raise SystemExit("--scale must be > 0")
    return analyze(args)


if __name__ == "__main__":
    raise SystemExit(main())
