#!/usr/bin/env python3
"""Build passive diagnostic overlay packets for xPRIMEray render-test captures."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
from collections import deque
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image, ImageDraw, ImageFont


SCHEMA_VERSION = 1
DEFAULT_MANUAL_ROIS = "40,35;280,35;40,145;280,145"
GENERATED_NAMES = {
    "layer0_beauty.png",
    "layer1_cartesian_wireframe.png",
    "layer2_transport_ownership.png",
    "layer3_risk_probe_markers.png",
    "layer4_spacetime_transport_diagram.png",
    "combined_diagnostic_overlay.png",
    "diagnostic_overlay_contact_sheet.png",
    "transport_shape_regions_overlay.png",
}
DIAGNOSTIC_SUFFIXES = (
    ".boundary_confidence.png",
    ".domain_confidence.png",
    ".domain_id.png",
    ".normal_discontinuity.png",
    ".selection_flip.png",
    ".step_convergence_confidence.png",
    ".step_sensitivity.png",
    ".precision_required.png",
    ".probe_hit_distance_delta.png",
    ".probe_normal_delta.png",
    ".probe_collider_mismatch.png",
    ".corner_required_precision_map.png",
    ".corner_hit_distance_delta.png",
    ".corner_normal_delta.png",
    ".corner_collider_flip_map.png",
    ".corner_convergence_profile.png",
)


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def load_json(path: Path | None) -> dict[str, Any]:
    if not path or not path.exists():
        return {}
    try:
        return json.loads(path.read_text())
    except Exception:
        return {}


def find_beauty_png(folder: Path) -> Path | None:
    candidates: list[Path] = []
    for path in sorted(folder.glob("*.png")):
        if path.name in GENERATED_NAMES:
            continue
        if path.name.startswith("corner_"):
            continue
        if path.name.endswith(DIAGNOSTIC_SUFFIXES):
            continue
        candidates.append(path)
    if not candidates:
        return None
    candidates.sort(key=lambda p: p.stat().st_size if p.exists() else 0, reverse=True)
    return candidates[0]


def parse_rois(raw: str | None) -> list[tuple[int, int]]:
    rois: list[tuple[int, int]] = []
    for item in (raw or "").split(";"):
        parts = [p.strip() for p in item.split(",")]
        if len(parts) != 2:
            continue
        try:
            rois.append((int(parts[0]), int(parts[1])))
        except ValueError:
            continue
    return rois


def primitive_rois(primitives: dict[str, Any], fallback: str) -> list[tuple[int, int]]:
    items = primitives.get("manual_rois") or []
    rois: list[tuple[int, int]] = []
    for item in items:
        try:
            rois.append((int(item["x"]), int(item["y"])))
        except Exception:
            pass
    return rois or parse_rois(fallback)


def draw_label(draw: ImageDraw.ImageDraw, xy: tuple[int, int], text: str, fill: tuple[int, int, int, int]) -> None:
    if not text:
        return
    x, y = xy
    font = ImageFont.load_default()
    bbox = draw.textbbox((x, y), text, font=font)
    pad = 2
    draw.rectangle((bbox[0] - pad, bbox[1] - pad, bbox[2] + pad, bbox[3] + pad), fill=(0, 0, 0, 180))
    draw.text((x, y), text, fill=fill, font=font)


def circle(draw: ImageDraw.ImageDraw, x: int, y: int, r: int, outline: tuple[int, int, int, int], fill: tuple[int, int, int, int] | None = None, width: int = 1) -> None:
    draw.ellipse((x - r, y - r, x + r, y + r), outline=outline, fill=fill, width=width)


def line_layer(base: Image.Image) -> Image.Image:
    return base.convert("RGBA")


def load_hit_csv(path: Path | None, width: int, height: int) -> tuple[np.ndarray, np.ndarray]:
    had = np.zeros((height, width), dtype=bool)
    collider = np.zeros((height, width), dtype=np.uint64)
    if not path or not path.exists():
        return had, collider
    with path.open(newline="", encoding="utf-8-sig") as handle:
        for row in csv.DictReader(handle):
            try:
                x = int(row.get("x", -1))
                y = int(row.get("y", -1))
                if not (0 <= x < width and 0 <= y < height):
                    continue
                h = row.get("had_hit", "0") in {"1", "true", "True"}
                had[y, x] = h
                collider[y, x] = int(row.get("collider_id", "0") or 0)
            except Exception:
                continue
    return had, collider


def extract_regions(had: np.ndarray, collider: np.ndarray) -> list[dict[str, Any]]:
    height, width = had.shape
    seen = np.zeros_like(had, dtype=bool)
    regions: list[dict[str, Any]] = []
    region_id = 0
    for y0 in range(height):
        for x0 in range(width):
            cid = int(collider[y0, x0])
            if seen[y0, x0] or not had[y0, x0] or cid == 0:
                continue
            q: deque[tuple[int, int]] = deque([(x0, y0)])
            seen[y0, x0] = True
            pts: list[tuple[int, int]] = []
            contour = 0
            while q:
                x, y = q.popleft()
                pts.append((x, y))
                is_boundary = False
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if not (0 <= nx < width and 0 <= ny < height):
                        is_boundary = True
                        continue
                    if not had[ny, nx] or int(collider[ny, nx]) != cid:
                        is_boundary = True
                        continue
                    if not seen[ny, nx]:
                        seen[ny, nx] = True
                        q.append((nx, ny))
                if is_boundary:
                    contour += 1
            xs = [p[0] for p in pts]
            ys = [p[1] for p in pts]
            area = len(pts)
            regions.append({
                "region_id": region_id,
                "collider_id": cid,
                "area": area,
                "centroid_x": round(float(sum(xs)) / max(1, area), 3),
                "centroid_y": round(float(sum(ys)) / max(1, area), 3),
                "bbox_x0": min(xs),
                "bbox_y0": min(ys),
                "bbox_x1": max(xs),
                "bbox_y1": max(ys),
                "contour_pixel_count": contour,
            })
            region_id += 1
    return regions


def draw_regions(base: Image.Image, had: np.ndarray, collider: np.ndarray, regions: list[dict[str, Any]]) -> Image.Image:
    img = base.convert("RGBA")
    overlay = Image.new("RGBA", img.size, (0, 0, 0, 0))
    px = overlay.load()
    height, width = had.shape
    for y in range(height):
        for x in range(width):
            cid = int(collider[y, x])
            if not had[y, x] or cid == 0:
                continue
            color_seed = cid % 255
            px[x, y] = (20, 120 + color_seed // 4, 220, 45)
    img = Image.alpha_composite(img, overlay)
    draw = ImageDraw.Draw(img)
    for r in regions:
        x0, y0, x1, y1 = int(r["bbox_x0"]), int(r["bbox_y0"]), int(r["bbox_x1"]), int(r["bbox_y1"])
        draw.rectangle((x0, y0, x1, y1), outline=(0, 220, 255, 170), width=1)
        circle(draw, int(float(r["centroid_x"])), int(float(r["centroid_y"])), 2, (0, 255, 255, 220), fill=(0, 255, 255, 160))
    return img


def draw_cartesian(base: Image.Image, primitives: dict[str, Any], labels: bool) -> Image.Image:
    img = line_layer(base)
    draw = ImageDraw.Draw(img)
    cart = (primitives.get("primitives") or {}).get("cartesian") or {}
    for line in cart.get("lines") or []:
        draw.line((line["x0"], line["y0"], line["x1"], line["y1"]), fill=(255, 40, 30, 235), width=2)
    for pt in cart.get("points") or []:
        x, y = int(pt.get("x", -1)), int(pt.get("y", -1))
        kind = str(pt.get("kind", "anchor"))
        r = 4 if kind == "corner" else 3
        fill = (255, 40, 30, 210) if kind in {"corner", "centroid"} else (255, 120, 80, 210)
        circle(draw, x, y, r, (255, 30, 20, 255), fill=fill, width=1)
        if labels and pt.get("label"):
            draw_label(draw, (x + 5, y + 4), str(pt.get("label")), (255, 255, 255, 255))
    return img


def find_optional_csv(folder: Path, suffix: str) -> Path | None:
    matches = sorted(folder.glob(f"*{suffix}"))
    if matches:
        return matches[0]
    return None


def draw_risk(base: Image.Image, folder: Path, rois: list[tuple[int, int]]) -> Image.Image:
    img = line_layer(base)
    draw = ImageDraw.Draw(img)
    for idx, (x, y) in enumerate(rois):
        circle(draw, x, y, 7, (255, 230, 20, 255), width=2)
        draw.line((x - 9, y, x + 9, y), fill=(255, 230, 20, 230), width=1)
        draw.line((x, y - 9, x, y + 9), fill=(255, 230, 20, 230), width=1)
        draw_label(draw, (x + 8, y - 8), f"ROI{idx}", (255, 255, 255, 255))
    for path in [find_optional_csv(folder, ".corner_transport_probe.csv"), folder / "corner_transport_probe.csv"]:
        if not path or not path.exists():
            continue
        try:
            with path.open(newline="") as handle:
                for row in csv.DictReader(handle):
                    x, y = int(row.get("x", -1)), int(row.get("y", -1))
                    if x < 0 or y < 0:
                        continue
                    risk = float(row.get("max_decision_risk", row.get("decision_risk", 0)) or 0)
                    flip = str(row.get("collider_flip_any", "False")).lower() == "true" or str(row.get("hit_ownership_change_any", "False")).lower() == "true"
                    color = (255, 0, 220, 230) if flip else (255, 180, 0, 190)
                    circle(draw, x, y, 2 if risk < 1 else 3, color, fill=color)
        except Exception:
            pass
        break
    return img


def draw_spacetime(base: Image.Image, primitives: dict[str, Any]) -> Image.Image | None:
    space = (primitives.get("primitives") or {}).get("spacetime") or {}
    if not space.get("polylines") and not space.get("events"):
        return None
    img = line_layer(base)
    draw = ImageDraw.Draw(img)
    for poly in space.get("polylines") or []:
        pts = [(int(p["x"]), int(p["y"])) for p in poly.get("points", []) if "x" in p and "y" in p]
        if len(pts) >= 2:
            order = int(poly.get("order_index", 0))
            color = (40, min(255, 120 + order * 7), 80, 220)
            draw.line(pts, fill=color, width=1)
            mid = pts[len(pts) // 2]
            draw_label(draw, (mid[0] + 2, mid[1] + 2), str(order), (255, 255, 255, 230))
    for ev in space.get("events") or []:
        x, y = int(ev.get("x", -1)), int(ev.get("y", -1))
        circle(draw, x, y, 4, (255, 145, 20, 255), fill=(255, 145, 20, 160), width=1)
    return img


def add_legend(img: Image.Image, labels: list[tuple[str, tuple[int, int, int, int]]]) -> Image.Image:
    out = img.convert("RGBA")
    draw = ImageDraw.Draw(out)
    font = ImageFont.load_default()
    widths = [draw.textbbox((0, 0), text, font=font)[2] for text, _ in labels]
    w = max(widths or [0]) + 32
    h = 16 * len(labels) + 8
    x = max(6, (out.width - w) // 2)
    y = 6
    draw.rectangle((x, y, x + w, y + h), fill=(0, 0, 0, 150))
    yy = y + 6
    for text, color in labels:
        draw.rectangle((x + 6, yy + 3, x + 16, yy + 10), fill=color)
        draw.text((x + 22, yy), text, fill=(255, 255, 255, 240), font=font)
        yy += 16
    return out


def save_contact_sheet(folder: Path, images: list[tuple[str, Path]]) -> None:
    thumbs: list[tuple[str, Image.Image]] = []
    for title, path in images:
        if not path.exists():
            continue
        img = Image.open(path).convert("RGB")
        img.thumbnail((240, 135), Image.Resampling.LANCZOS)
        thumbs.append((title, img))
    if not thumbs:
        return
    w, h, label_h = 240, 135, 24
    sheet = Image.new("RGB", (w * len(thumbs), h + label_h), "white")
    draw = ImageDraw.Draw(sheet)
    for i, (title, img) in enumerate(thumbs):
        x = i * w
        draw.text((x + 6, 6), title, fill=(0, 0, 0))
        sheet.paste(img, (x, label_h))
    sheet.save(folder / "diagnostic_overlay_contact_sheet.png")


def write_regions_csv(path: Path, regions: list[dict[str, Any]]) -> None:
    cols = ["region_id", "collider_id", "area", "centroid_x", "centroid_y", "bbox_x0", "bbox_y0", "bbox_x1", "bbox_y1", "contour_pixel_count"]
    with path.open("w", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=cols)
        writer.writeheader()
        for row in regions:
            writer.writerow({c: row.get(c, "") for c in cols})


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("capture_folder", type=Path)
    parser.add_argument("--use-opencv-contours", type=int, default=0)
    parser.add_argument("--manual-rois", default=DEFAULT_MANUAL_ROIS)
    args = parser.parse_args()

    folder = args.capture_folder
    beauty_path = find_beauty_png(folder)
    if beauty_path is None:
        raise SystemExit(f"no beauty png found in {folder}")
    beauty = Image.open(beauty_path).convert("RGBA")
    width, height = beauty.size
    stem = beauty_path.stem
    primitives_path = folder / f"{stem}.diagnostic_wireframe_primitives.json"
    if not primitives_path.exists():
        found = sorted(folder.glob("*.diagnostic_wireframe_primitives.json"))
        primitives_path = found[0] if found else primitives_path
    primitives = load_json(primitives_path if primitives_path.exists() else None)
    hit_csv = folder / f"{stem}.hit_diagnostics.csv"
    if not hit_csv.exists():
        found = sorted(folder.glob("*.hit_diagnostics.csv"))
        hit_csv = found[0] if found else hit_csv
    metadata_path = folder / "metadata.json"
    metadata = load_json(metadata_path if metadata_path.exists() else None)

    enabled = primitives.get("enabled_layers") or {}
    labels_enabled = bool(enabled.get("labels", True))
    rois = primitive_rois(primitives, args.manual_rois or metadata.get("diagnostic_wireframe_manual_rois") or DEFAULT_MANUAL_ROIS)
    missing: list[str] = []
    if not primitives_path.exists():
        missing.append("diagnostic_wireframe_primitives_json")
    if not hit_csv.exists():
        missing.append("hit_diagnostics_csv")

    beauty.save(folder / "layer0_beauty.png")

    layer1 = draw_cartesian(beauty, primitives, labels_enabled)
    layer1 = add_legend(layer1, [("Cartesian object projection", (255, 40, 30, 230))])
    layer1.save(folder / "layer1_cartesian_wireframe.png")

    had, collider = load_hit_csv(hit_csv if hit_csv.exists() else None, width, height)
    regions = extract_regions(had, collider) if hit_csv.exists() else []
    write_regions_csv(folder / "transport_shape_regions.csv", regions)
    layer2 = draw_regions(beauty, had, collider, regions)
    layer2 = add_legend(layer2, [("Transport ownership", (0, 220, 255, 200))])
    layer2.save(folder / "layer2_transport_ownership.png")
    layer2.save(folder / "transport_shape_regions_overlay.png")

    layer3 = draw_risk(beauty, folder, rois)
    layer3 = add_legend(layer3, [("Manual ROI", (255, 230, 20, 230)), ("Risk/probe sample", (255, 0, 220, 230))])
    layer3.save(folder / "layer3_risk_probe_markers.png")

    layer4 = draw_spacetime(beauty, primitives)
    if layer4 is not None:
        layer4 = add_legend(layer4, [("Symbolic path", (40, 220, 80, 220)), ("First-hit event", (255, 145, 20, 220))])
        layer4.save(folder / "layer4_spacetime_transport_diagram.png")

    combined = beauty.convert("RGBA")
    for layer_path in ("layer1_cartesian_wireframe.png", "layer2_transport_ownership.png", "layer3_risk_probe_markers.png", "layer4_spacetime_transport_diagram.png"):
        p = folder / layer_path
        if p.exists():
            layer = Image.open(p).convert("RGBA")
            # Blend layer overlays softly with the original beauty to preserve context.
            combined = Image.blend(combined, layer, 0.55 if "transport" in layer_path else 0.72)
    combined = add_legend(combined, [
        ("Cartesian", (255, 40, 30, 230)),
        ("Transport", (0, 220, 255, 200)),
        ("Risk/probe", (255, 230, 20, 230)),
    ])
    combined.save(folder / "combined_diagnostic_overlay.png")

    contact_items = [
        ("beauty", folder / "layer0_beauty.png"),
        ("Cartesian", folder / "layer1_cartesian_wireframe.png"),
        ("transport", folder / "layer2_transport_ownership.png"),
        ("risk/probe", folder / "layer3_risk_probe_markers.png"),
        ("combined", folder / "combined_diagnostic_overlay.png"),
    ]
    save_contact_sheet(folder, contact_items)

    counts = primitives.get("primitive_count_by_layer") or {}
    object_count = int(primitives.get("object_count", 0) or 0)
    metadata_out = {
        "schema_version": SCHEMA_VERSION,
        "capture_stem": stem,
        "enabled_layers": {
            "cartesian": True,
            "transport": True,
            "risk": True,
            "spacetime": (folder / "layer4_spacetime_transport_diagram.png").exists(),
            "labels": labels_enabled,
        },
        "missing_inputs": missing,
        "object_count": object_count,
        "primitive_count_by_layer": counts,
        "hit_region_count": len(regions),
        "manual_rois": [{"x": x, "y": y} for x, y in rois],
        "beauty_hash": sha256_file(beauty_path),
        "beauty_path": str(beauty_path),
        "primitive_path": str(primitives_path) if primitives_path.exists() else "",
        "hit_diagnostics_csv": str(hit_csv) if hit_csv.exists() else "",
        "post_process_only": True,
        "opencv_requested": bool(args.use_opencv_contours),
        "opencv_used": False,
    }
    (folder / "overlay_metadata.json").write_text(json.dumps(metadata_out, indent=2, sort_keys=True) + "\n")
    print(f"[diagnostic-wireframe-overlay] folder={folder} stem={stem} objects={object_count} regions={len(regions)} missing={','.join(missing) or 'none'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
