#!/usr/bin/env python3
"""Compare final stored hits with first accepted hits across normal-band boundaries."""

from __future__ import annotations

import argparse
import csv
import json
import math
import struct
import zlib
from pathlib import Path

try:
    from PIL import Image
except ImportError:  # pragma: no cover - optional diagnostic dependency
    Image = None


def f(row: dict[str, str], key: str, default: float = 0.0) -> float:
    try:
        return float(row.get(key, default))
    except ValueError:
        return default


def i(row: dict[str, str], key: str, default: int = 0) -> int:
    try:
        return int(float(row.get(key, default)))
    except ValueError:
        return default


def vec(row: dict[str, str], prefix: str) -> tuple[float, float, float]:
    return (f(row, f"{prefix}_x"), f(row, f"{prefix}_y"), f(row, f"{prefix}_z"))


def norm(v: tuple[float, float, float]) -> tuple[float, float, float]:
    length = math.sqrt(sum(c * c for c in v))
    if length <= 1e-12:
        return (0.0, 0.0, 0.0)
    return tuple(c / length for c in v)  # type: ignore[return-value]


def normal_delta(a: tuple[float, float, float], b: tuple[float, float, float]) -> float:
    an = norm(a)
    bn = norm(b)
    dot = max(-1.0, min(1.0, sum(x * y for x, y in zip(an, bn))))
    return 1.0 - dot


def load_csv(path: Path) -> tuple[int, int, dict[tuple[int, int], dict[str, str]]]:
    rows: dict[tuple[int, int], dict[str, str]] = {}
    with path.open(newline="", encoding="utf-8-sig") as fh:
        for row in csv.DictReader(fh):
            x = i(row, "x", -1)
            y = i(row, "y", -1)
            if x >= 0 and y >= 0:
                rows[(x, y)] = row
    width = max((x for x, _ in rows), default=-1) + 1
    height = max((y for _, y in rows), default=-1) + 1
    return width, height, rows


def pair_metrics(a: dict[str, str], b: dict[str, str]) -> dict[str, float | int]:
    final_delta = normal_delta(vec(a, "normal"), vec(b, "normal"))
    first_delta = normal_delta(vec(a, "first_accepted_normal"), vec(b, "first_accepted_normal"))
    a_final_first = normal_delta(vec(a, "normal"), vec(a, "first_accepted_normal"))
    b_final_first = normal_delta(vec(b, "normal"), vec(b, "first_accepted_normal"))
    return {
        "final_normal_delta": final_delta,
        "first_normal_delta": first_delta,
        "first_step_jump": abs(i(a, "first_accepted_segment_index", -1) - i(b, "first_accepted_segment_index", -1)),
        "final_distance_jump": abs(f(a, "hit_distance", -1.0) - f(b, "hit_distance", -1.0)),
        "first_distance_jump": abs(f(a, "first_accepted_hit_distance", -1.0) - f(b, "first_accepted_hit_distance", -1.0)),
        "final_collider_switch": int(i(a, "collider_id") != i(b, "collider_id")),
        "first_collider_switch": int(i(a, "first_accepted_collider_id") != i(b, "first_accepted_collider_id")),
        "a_final_vs_first_normal_delta": a_final_first,
        "b_final_vs_first_normal_delta": b_final_first,
        "candidate_count_a": i(a, "first_accepted_candidate_count", -1),
        "candidate_count_b": i(b, "first_accepted_candidate_count", -1),
    }


def summarize(values: list[float]) -> dict[str, float]:
    if not values:
        return {"mean": 0.0, "median": 0.0, "max": 0.0}
    vals = sorted(values)
    return {
        "mean": sum(vals) / len(vals),
        "median": vals[len(vals) // 2],
        "max": vals[-1],
    }


def otsu_threshold(values: list[float]) -> float:
    if not values:
        return 0.0
    lo = min(values)
    hi = max(values)
    if hi <= lo:
        return hi

    bins = [0] * 256
    scale = 255.0 / (hi - lo)
    for value in values:
        index = max(0, min(255, int((value - lo) * scale)))
        bins[index] += 1

    total = len(values)
    sum_total = sum(index * count for index, count in enumerate(bins))
    weight_bg = 0
    sum_bg = 0.0
    best_index = 0
    best_between = -1.0
    for index, count in enumerate(bins):
        weight_bg += count
        if weight_bg == 0:
            continue
        weight_fg = total - weight_bg
        if weight_fg == 0:
            break
        sum_bg += index * count
        mean_bg = sum_bg / weight_bg
        mean_fg = (sum_total - sum_bg) / weight_fg
        between = weight_bg * weight_fg * (mean_bg - mean_fg) ** 2
        if between > best_between:
            best_between = between
            best_index = index
    return lo + (best_index / 255.0) * (hi - lo)


def pearson(a: list[float], b: list[float]) -> float:
    if len(a) != len(b) or not a:
        return 0.0
    mean_a = sum(a) / len(a)
    mean_b = sum(b) / len(b)
    num = sum((x - mean_a) * (y - mean_b) for x, y in zip(a, b))
    den_a = math.sqrt(sum((x - mean_a) ** 2 for x in a))
    den_b = math.sqrt(sum((y - mean_b) ** 2 for y in b))
    den = den_a * den_b
    return num / den if den > 1e-12 else 0.0


def iou(a: list[bool], b: list[bool]) -> float:
    if len(a) != len(b) or not a:
        return 0.0
    intersection = sum(1 for x, y in zip(a, b) if x and y)
    union = sum(1 for x, y in zip(a, b) if x or y)
    return intersection / union if union else 0.0


def build_neighbor_delta_map(
    width: int,
    height: int,
    rows: dict[tuple[int, int], dict[str, str]],
    prefix: str,
) -> list[float]:
    values = [0.0] * (width * height)
    for y in range(height):
        for x in range(width):
            row = rows.get((x, y))
            if not row:
                continue
            best = 0.0
            for dx, dy in ((1, 0), (0, 1)):
                other = rows.get((x + dx, y + dy))
                if other:
                    best = max(best, normal_delta(vec(row, prefix), vec(other, prefix)))
            values[(y * width) + x] = best
    return values


def load_visible_signal(debug_image_path: Path, width: int, height: int) -> list[float]:
    if Image is not None:
        with Image.open(debug_image_path) as image:
            rgb = image.convert("RGB")
            if rgb.size != (width, height):
                rgb = rgb.resize((width, height), Image.Resampling.BILINEAR)
            pixels = list(rgb.getdata())
    else:
        png_width, png_height, pixels = load_png_rgb_stdlib(debug_image_path)
        if (png_width, png_height) != (width, height):
            return []

    values = [0.0] * (width * height)
    for y in range(height):
        for x in range(width):
            idx = (y * width) + x
            r, g, b = pixels[idx]
            best = 0.0
            for dx, dy in ((1, 0), (0, 1)):
                nx = x + dx
                ny = y + dy
                if nx >= width or ny >= height:
                    continue
                nr, ng, nb = pixels[(ny * width) + nx]
                delta = math.sqrt((r - nr) ** 2 + (g - ng) ** 2 + (b - nb) ** 2) / (255.0 * math.sqrt(3.0))
                best = max(best, delta)
            values[idx] = best
    return values


def load_png_rgb_stdlib(path: Path) -> tuple[int, int, list[tuple[int, int, int]]]:
    data = path.read_bytes()
    if not data.startswith(b"\x89PNG\r\n\x1a\n"):
        return 0, 0, []

    offset = 8
    width = 0
    height = 0
    bit_depth = 0
    color_type = 0
    idat = bytearray()
    while offset + 8 <= len(data):
        length = struct.unpack(">I", data[offset : offset + 4])[0]
        chunk_type = data[offset + 4 : offset + 8]
        chunk_data = data[offset + 8 : offset + 8 + length]
        offset += 12 + length
        if chunk_type == b"IHDR":
            width, height, bit_depth, color_type, compression, filter_method, interlace = struct.unpack(">IIBBBBB", chunk_data)
            if bit_depth != 8 or compression != 0 or filter_method != 0 or interlace != 0:
                return 0, 0, []
        elif chunk_type == b"IDAT":
            idat.extend(chunk_data)
        elif chunk_type == b"IEND":
            break

    channels_by_type = {0: 1, 2: 3, 4: 2, 6: 4}
    channels = channels_by_type.get(color_type)
    if width <= 0 or height <= 0 or channels is None:
        return 0, 0, []

    raw = zlib.decompress(bytes(idat))
    stride = width * channels
    rows: list[bytearray] = []
    pos = 0
    prev = bytearray(stride)
    for _ in range(height):
        if pos >= len(raw):
            return 0, 0, []
        filter_type = raw[pos]
        pos += 1
        row = bytearray(raw[pos : pos + stride])
        pos += stride
        recon = bytearray(stride)
        for i, value in enumerate(row):
            left = recon[i - channels] if i >= channels else 0
            up = prev[i]
            up_left = prev[i - channels] if i >= channels else 0
            if filter_type == 0:
                predictor = 0
            elif filter_type == 1:
                predictor = left
            elif filter_type == 2:
                predictor = up
            elif filter_type == 3:
                predictor = (left + up) // 2
            elif filter_type == 4:
                predictor = paeth_predictor(left, up, up_left)
            else:
                return 0, 0, []
            recon[i] = (value + predictor) & 0xFF
        rows.append(recon)
        prev = recon

    pixels: list[tuple[int, int, int]] = []
    for row in rows:
        for x in range(width):
            base = x * channels
            if color_type == 0:
                gray = row[base]
                pixels.append((gray, gray, gray))
            elif color_type == 2:
                pixels.append((row[base], row[base + 1], row[base + 2]))
            elif color_type == 4:
                gray = row[base]
                pixels.append((gray, gray, gray))
            else:
                pixels.append((row[base], row[base + 1], row[base + 2]))
    return width, height, pixels


def paeth_predictor(left: int, up: int, up_left: int) -> int:
    p = left + up - up_left
    pa = abs(p - left)
    pb = abs(p - up)
    pc = abs(p - up_left)
    if pa <= pb and pa <= pc:
        return left
    if pb <= pc:
        return up
    return up_left


def compute_alignment(
    width: int,
    height: int,
    rows: dict[tuple[int, int], dict[str, str]],
    debug_image_path: Path | None,
) -> dict:
    if debug_image_path is None:
        return {}
    visible = load_visible_signal(debug_image_path, width, height)
    if not visible:
        return {}

    neighbor_final = build_neighbor_delta_map(width, height, rows, "normal")
    neighbor_first = build_neighbor_delta_map(width, height, rows, "first_accepted_normal")
    visible_threshold = otsu_threshold(visible)
    final_threshold = otsu_threshold(neighbor_final)
    first_threshold = otsu_threshold(neighbor_first)
    visible_mask = [value > visible_threshold for value in visible]
    final_mask = [value > final_threshold for value in neighbor_final]
    first_mask = [value > first_threshold for value in neighbor_first]
    return {
        "debug_image_path": str(debug_image_path),
        "visible_band_pixels": sum(1 for value in visible_mask if value),
        "visible_band_threshold": visible_threshold,
        "neighbor_normal_delta": {
            "pearson_to_visible_band_signal": pearson(neighbor_final, visible),
            "iou_to_visible_band_mask": iou(final_mask, visible_mask),
            "mean_signal": sum(neighbor_final) / max(1, len(neighbor_final)),
            "threshold": final_threshold,
            "nonzero_pixels": sum(1 for value in final_mask if value),
        },
        "first_hit_neighbor_normal_delta": {
            "pearson_to_visible_band_signal": pearson(neighbor_first, visible),
            "iou_to_visible_band_mask": iou(first_mask, visible_mask),
            "mean_signal": sum(neighbor_first) / max(1, len(neighbor_first)),
            "threshold": first_threshold,
            "nonzero_pixels": sum(1 for value in first_mask if value),
        },
    }


def analyze(csv_path: Path, top: int, sample: int, debug_image_path: Path | None = None) -> dict:
    width, height, rows = load_csv(csv_path)
    pairs = []
    for (x, y), row in rows.items():
        if i(row, "had_hit") == 0 or i(row, "first_accepted_had_hit") == 0:
            continue
        for dx, dy, axis in ((1, 0, "h"), (0, 1, "v")):
            other = rows.get((x + dx, y + dy))
            if not other or i(other, "had_hit") == 0 or i(other, "first_accepted_had_hit") == 0:
                continue
            metrics = pair_metrics(row, other)
            pairs.append({
                "a": [x, y],
                "b": [x + dx, y + dy],
                "axis": axis,
                **metrics,
            })

    pairs.sort(key=lambda p: p["final_normal_delta"], reverse=True)
    selected = pairs[: max(top, sample)]
    stats_source = selected[:sample]
    first = [float(p["first_normal_delta"]) for p in stats_source]
    final = [float(p["final_normal_delta"]) for p in stats_source]
    stored_overwrite = [
        max(float(p["a_final_vs_first_normal_delta"]), float(p["b_final_vs_first_normal_delta"]))
        for p in stats_source
    ]
    boundary_delta_residual = [
        abs(float(p["final_normal_delta"]) - float(p["first_normal_delta"]))
        for p in stats_source
    ]
    first_to_final_ratio = (sum(first) / len(first)) / max(1e-9, (sum(final) / len(final))) if first else 0.0
    overwrite_mean = sum(stored_overwrite) / len(stored_overwrite) if stored_overwrite else 0.0
    residual_mean = sum(boundary_delta_residual) / len(boundary_delta_residual) if boundary_delta_residual else 0.0
    verdict = "divergence begins at first-hit acquisition" if first_to_final_ratio >= 0.80 and residual_mean <= 0.05 else "divergence appears later in stored-hit refinement"

    return {
        "csv_path": str(csv_path),
        "width": width,
        "height": height,
        "neighbor_pairs_considered": len(pairs),
        "boundary_sample_size": len(stats_source),
        "summary": {
            "final_normal_delta": summarize(final),
            "first_normal_delta": summarize(first),
            "first_segment_jump": summarize([float(p["first_step_jump"]) for p in stats_source]),
            "final_distance_jump": summarize([float(p["final_distance_jump"]) for p in stats_source]),
            "first_distance_jump": summarize([float(p["first_distance_jump"]) for p in stats_source]),
            "final_collider_switch_rate": sum(float(p["final_collider_switch"]) for p in stats_source) / max(1, len(stats_source)),
            "first_collider_switch_rate": sum(float(p["first_collider_switch"]) for p in stats_source) / max(1, len(stats_source)),
            "first_to_final_normal_delta_ratio": first_to_final_ratio,
            "boundary_delta_residual_mean": residual_mean,
            "stored_overwrite_normal_delta_mean": overwrite_mean,
        },
        "alignment": compute_alignment(width, height, rows, debug_image_path),
        "top_pairs": pairs[:top],
        "verdict": verdict,
    }


def write_markdown(result: dict, md_path: Path) -> None:
    lines = [
        "# First-Hit Boundary Comparison",
        "",
        f"CSV: `{result['csv_path']}`",
        f"Pairs considered: `{result['neighbor_pairs_considered']}`; boundary sample: `{result['boundary_sample_size']}`",
        f"Verdict: **{result['verdict']}**",
        "",
        "| pair | final normal Δ | first normal Δ | first seg Δ | final dist Δ | first dist Δ | final cid switch | first cid switch | first cand | overwrite normal Δ |",
        "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for p in result["top_pairs"]:
        pair = f"({p['a'][0]},{p['a'][1]}){p['axis']}({p['b'][0]},{p['b'][1]})"
        overwrite = max(float(p["a_final_vs_first_normal_delta"]), float(p["b_final_vs_first_normal_delta"]))
        lines.append(
            f"| {pair} | {p['final_normal_delta']:.6f} | {p['first_normal_delta']:.6f} | "
            f"{p['first_step_jump']} | {p['final_distance_jump']:.6f} | {p['first_distance_jump']:.6f} | "
            f"{p['final_collider_switch']} | {p['first_collider_switch']} | "
            f"{p['candidate_count_a']}/{p['candidate_count_b']} | {overwrite:.6f} |"
        )
    lines.append("")
    lines.append("## Summary")
    for key, value in result["summary"].items():
        lines.append(f"- `{key}`: `{value}`")
    md_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("csv", type=Path)
    parser.add_argument("--top", type=int, default=12)
    parser.add_argument("--sample", type=int, default=256)
    parser.add_argument("--debug-image", type=Path)
    parser.add_argument("--json-out", type=Path)
    parser.add_argument("--md-out", type=Path)
    args = parser.parse_args()

    result = analyze(args.csv, args.top, args.sample, args.debug_image)
    json_text = json.dumps(result, indent=2)
    if args.json_out:
        args.json_out.write_text(json_text + "\n", encoding="utf-8")
    if args.md_out:
        write_markdown(result, args.md_out)
    print(json_text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
