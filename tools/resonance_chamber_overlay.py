#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import json
import math
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter, ImageFont, ImageOps


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SEARCH_ROOT = ROOT / "output"
OUTPUT_NAMES = {
    "overlay": "resonance_chamber_overlay.png",
    "annotated": "resonance_chamber_overlay_annotated.png",
    "summary": "resonance_chamber_summary.md",
    "metrics": "resonance_chamber_metrics.csv",
}


@dataclass
class SignalMask:
    name: str
    label: str
    source: str
    mask: Image.Image
    color: tuple[int, int, int]
    alpha: int
    note: str = ""


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Post-process a wormhole fixture run into a Resonance Chamber Overlay."
    )
    parser.add_argument(
        "--input",
        type=Path,
        default=None,
        help="Wormhole fixture output directory. Defaults to latest relevant directory under output/.",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=None,
        help="Output directory. Defaults to the selected input directory.",
    )
    return parser.parse_args()


def latest_wormhole_output(search_root: Path = DEFAULT_SEARCH_ROOT) -> Path:
    candidates: list[Path] = []
    for path in search_root.rglob("*"):
        if not path.is_dir():
            continue
        name = str(path).lower()
        if "wormhole" not in name:
            continue
        has_fixture_shape = (path / "panels").is_dir() or any(path.glob("*wormhole*_summary.json"))
        if has_fixture_shape:
            candidates.append(path)

    if not candidates:
        raise FileNotFoundError(f"No wormhole fixture output directories found under {search_root}")

    return max(candidates, key=lambda p: p.stat().st_mtime)


def find_first(root: Path, patterns: Iterable[str]) -> Path | None:
    for pattern in patterns:
        matches = sorted(root.rglob(pattern))
        if matches:
            return matches[0]
    return None


def load_image(path: Path | None, size: tuple[int, int] | None = None) -> Image.Image | None:
    if path is None or not path.exists():
        return None
    image = Image.open(path).convert("RGB")
    if size and image.size != size:
        image = image.resize(size, Image.Resampling.BILINEAR)
    return image


def open_gray(path: Path | None, size: tuple[int, int]) -> Image.Image | None:
    image = load_image(path, size)
    if image is None:
        return None
    return ImageOps.grayscale(image)


def normalize_mask(mask: Image.Image, blur: float = 1.0) -> Image.Image:
    gray = ImageOps.grayscale(mask)
    gray = ImageOps.autocontrast(gray)
    if blur > 0:
        gray = gray.filter(ImageFilter.GaussianBlur(blur))
    return gray


def threshold_mask(mask: Image.Image, cutoff: int = 96, blur: float = 1.5) -> Image.Image:
    gray = normalize_mask(mask, blur=0.0)
    out = gray.point(lambda value: 255 if value >= cutoff else 0, mode="L")
    if blur > 0:
        out = out.filter(ImageFilter.GaussianBlur(blur))
    return out


def soft_ellipse(size: tuple[int, int], box: tuple[float, float, float, float], blur: float) -> Image.Image:
    width, height = size
    mask = Image.new("L", size, 0)
    draw = ImageDraw.Draw(mask)
    draw.ellipse(
        (
            int(box[0] * width),
            int(box[1] * height),
            int(box[2] * width),
            int(box[3] * height),
        ),
        fill=210,
    )
    return mask.filter(ImageFilter.GaussianBlur(blur))


def soft_polygon(size: tuple[int, int], points: list[tuple[float, float]], blur: float, fill: int = 190) -> Image.Image:
    width, height = size
    mask = Image.new("L", size, 0)
    draw = ImageDraw.Draw(mask)
    draw.polygon([(int(x * width), int(y * height)) for x, y in points], fill=fill)
    return mask.filter(ImageFilter.GaussianBlur(blur))


def domain_edge_mask(path: Path | None, size: tuple[int, int]) -> Image.Image | None:
    gray = open_gray(path, size)
    if gray is None:
        return None
    shifted_x = ImageChops.offset(gray, 1, 0)
    shifted_y = ImageChops.offset(gray, 0, 1)
    diff = ImageChops.lighter(ImageChops.difference(gray, shifted_x), ImageChops.difference(gray, shifted_y))
    return threshold_mask(diff, cutoff=8, blur=1.0)


def sector_density_mask(report_path: Path | None, size: tuple[int, int]) -> tuple[Image.Image | None, str]:
    if report_path is None or not report_path.exists():
        return None, "missing sector report"

    try:
        payload = json.loads(report_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return None, "sector report JSON parse failed"

    entries = payload.get("entries") or []
    theta_bins = int(payload.get("theta_bins") or 16)
    radial_bins = int(payload.get("radial_bins") or 4)
    if not entries:
        return None, "sector report has no entries"

    max_samples = max(float(entry.get("query_samples") or 0) for entry in entries) or 1.0
    width, height = size
    cx, cy = width * 0.5, height * 0.5
    max_radius = min(width, height) * 0.47
    mask = Image.new("L", size, 0)
    draw = ImageDraw.Draw(mask)

    for entry in entries:
        theta_bin = int(entry.get("theta_bin") or 0)
        radial_bin = int(entry.get("radial_bin") or 0)
        samples = float(entry.get("query_samples") or 0)
        intensity = int(35 + 220 * math.sqrt(samples / max_samples))
        start = 360.0 * theta_bin / theta_bins - 90.0
        end = 360.0 * (theta_bin + 1) / theta_bins - 90.0
        r0 = max_radius * radial_bin / radial_bins
        r1 = max_radius * (radial_bin + 1) / radial_bins
        outer = (cx - r1, cy - r1, cx + r1, cy + r1)
        inner = (cx - r0, cy - r0, cx + r0, cy + r0)
        draw.pieslice(outer, start=start, end=end, fill=intensity)
        if r0 > 1:
            draw.pieslice(inner, start=start, end=end, fill=0)

    return mask.filter(ImageFilter.GaussianBlur(2.0)), f"{report_path}"


def choose_base_image(input_dir: Path) -> tuple[Image.Image, str]:
    base_path = find_first(
        input_dir,
        (
            "panels/clean_curved/*_film.png",
            "panels/clean_curved/*_composed.png",
            "panels/straight_vs_curved/*_composed.png",
            "testbench_preview.png",
            "*contact_sheet.png",
        ),
    )
    if base_path is None:
        base_path = find_first(input_dir, ("*.png",))
    if base_path is None:
        raise FileNotFoundError(f"No PNG fixture image found under {input_dir}")

    image = Image.open(base_path).convert("RGB")
    return ImageEnhance.Contrast(image).enhance(1.05), str(base_path)


def build_signal_masks(input_dir: Path, size: tuple[int, int]) -> tuple[list[SignalMask], list[str]]:
    missing: list[str] = []
    masks: list[SignalMask] = []

    boundary_path = find_first(input_dir, ("*boundary_confidence.png", "*boundary*cross*.png"))
    boundary = open_gray(boundary_path, size)
    if boundary:
        chamber_wall = threshold_mask(boundary, cutoff=64, blur=2.0)
        boundary_source = str(boundary_path)
    else:
        chamber_wall = soft_ellipse(size, (0.31, 0.23, 0.69, 0.77), blur=10)
        boundary_source = "placeholder chamber ellipse"
        missing.append("boundary crossing / boundary confidence map")
    masks.append(
        SignalMask(
            "chamber_wall_interaction",
            "RESONANCE CHAMBER",
            boundary_source,
            chamber_wall,
            (95, 178, 184),
            96,
            "high boundary-crossing density or placeholder wall interaction",
        )
    )

    throat_path = find_first(input_dir, ("*throat*.png", "*ring_density.png", "*portal_ring_density.png"))
    throat = open_gray(throat_path, size)
    if throat:
        throat_mask = threshold_mask(throat, cutoff=80, blur=2.4)
        throat_source = str(throat_path)
    else:
        throat_mask = soft_ellipse(size, (0.43, 0.35, 0.57, 0.65), blur=5)
        throat_source = "placeholder centered core"
        missing.append("throat-event / portal ring-density map")
    masks.append(
        SignalMask(
            "resonant_core",
            "RESONANCE CHAMBER",
            throat_source,
            throat_mask,
            (244, 197, 104),
            104,
            "throat-event pixels or portal ring-density core",
        )
    )

    report_path = find_first(input_dir, ("*wormhole_portal_sector_report.json", "*transport*.json", "*.jsonl", "*.csv"))
    sector_mask, sector_note = sector_density_mask(report_path, size)
    phase_map_path = find_first(input_dir, ("*step_budget*film.png", "*step_budget*composed.png", "*usefulness.png"))
    phase_image = open_gray(phase_map_path, size)
    if sector_mask and phase_image:
        phase_mask = ImageChops.lighter(normalize_mask(sector_mask, blur=1.5), threshold_mask(phase_image, cutoff=72, blur=2.0))
        phase_source = f"{sector_note}; {phase_map_path}"
    elif sector_mask:
        phase_mask = normalize_mask(sector_mask, blur=2.0)
        phase_source = sector_note
    elif phase_image:
        phase_mask = threshold_mask(phase_image, cutoff=72, blur=2.0)
        phase_source = str(phase_map_path)
    else:
        phase_mask = soft_ellipse(size, (0.25, 0.30, 0.75, 0.72), blur=12)
        phase_source = "placeholder accumulated annulus"
        missing.append("transport telemetry / step budget / usefulness map")
    masks.append(
        SignalMask(
            "phase_accumulation",
            "PHASE BUILDUP",
            phase_source,
            phase_mask,
            (176, 135, 206),
            78,
            "repeated path density or high precision / step budget signal",
        )
    )

    domain_path = find_first(input_dir, ("*domain_id.png", "*ownership*.png"))
    domain_edges = domain_edge_mask(domain_path, size)
    flip_path = find_first(input_dir, ("*selection_flip.png",))
    flip_mask = open_gray(flip_path, size)
    if domain_edges and flip_mask:
        transition_mask = ImageChops.lighter(domain_edges, threshold_mask(flip_mask, cutoff=16, blur=1.2))
        transition_source = f"{domain_path}; {flip_path}"
    elif domain_edges:
        transition_mask = domain_edges
        transition_source = str(domain_path)
    else:
        transition_mask = soft_polygon(size, [(0.36, 0.18), (0.48, 0.50), (0.36, 0.82), (0.33, 0.82), (0.44, 0.50), (0.33, 0.18)], blur=3, fill=160)
        transition_source = "placeholder entrance ownership boundary"
        missing.append("domain ownership / selection flip transition map")
    masks.append(
        SignalMask(
            "ownership_transition",
            "OWNERSHIP TRANSITION",
            transition_source,
            transition_mask,
            (120, 194, 132),
            110,
            "outside to chamber to exit ownership transition",
        )
    )

    inbound = soft_polygon(size, [(0.02, 0.22), (0.40, 0.38), (0.40, 0.62), (0.02, 0.78)], blur=10, fill=140)
    masks.append(
        SignalMask(
            "inbound_packet",
            "INBOUND PACKET",
            "fixture-space left-to-throat transport prior",
            inbound,
            (102, 154, 210),
            58,
            "soft visual guide for inbound transport region",
        )
    )

    exit_plume = soft_polygon(size, [(0.56, 0.40), (0.96, 0.22), (0.96, 0.78), (0.56, 0.60)], blur=12, fill=150)
    masks.append(
        SignalMask(
            "tunnel_exit",
            "TUNNEL EXIT",
            "fixture-space throat-to-right plume prior",
            exit_plume,
            (226, 132, 102),
            66,
            "soft visual guide for tunnel plume / exit region",
        )
    )

    return masks, missing


def composite_overlay(base: Image.Image, masks: list[SignalMask]) -> Image.Image:
    composed = base.convert("RGBA")
    shade = Image.new("RGBA", composed.size, (0, 0, 0, 32))
    composed = Image.alpha_composite(composed, shade)

    for signal in masks:
        alpha_mask = signal.mask.point(lambda value, a=signal.alpha: int(value * a / 255), mode="L")
        layer = Image.new("RGBA", composed.size, (*signal.color, 0))
        layer.putalpha(alpha_mask)
        composed = Image.alpha_composite(composed, layer)

    return composed.convert("RGB")


def load_font(size: int, bold: bool = False) -> ImageFont.ImageFont:
    candidates = [
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf" if bold else "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/truetype/liberation2/LiberationSans-Bold.ttf" if bold else "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
    ]
    for candidate in candidates:
        path = Path(candidate)
        if path.exists():
            return ImageFont.truetype(str(path), size=size)
    return ImageFont.load_default()


def label_anchor(name: str, size: tuple[int, int]) -> tuple[int, int]:
    width, height = size
    anchors = {
        "inbound_packet": (int(width * 0.07), int(height * 0.22)),
        "resonant_core": (int(width * 0.38), int(height * 0.31)),
        "phase_accumulation": (int(width * 0.34), int(height * 0.72)),
        "tunnel_exit": (int(width * 0.69), int(height * 0.24)),
        "ownership_transition": (int(width * 0.52), int(height * 0.82)),
    }
    return anchors.get(name, (int(width * 0.04), int(height * 0.08)))


def draw_annotation(image: Image.Image, masks: list[SignalMask]) -> Image.Image:
    annotated = image.convert("RGBA")
    draw = ImageDraw.Draw(annotated)
    width, height = annotated.size
    label_font = load_font(max(11, min(width, height) // 22), bold=True)
    small_font = load_font(max(10, min(width, height) // 32), bold=False)

    title = "RESONANCE CHAMBER OVERLAY"
    draw.text((12, 10), title, font=label_font, fill=(245, 245, 238, 240), stroke_width=2, stroke_fill=(16, 18, 20, 210))
    draw.text(
        (12, 12 + label_font.size),
        "outside -> chamber -> exit",
        font=small_font,
        fill=(225, 229, 224, 220),
        stroke_width=2,
        stroke_fill=(16, 18, 20, 190),
    )

    labels = {
        "inbound_packet": "INBOUND PACKET",
        "resonant_core": "RESONANCE CHAMBER",
        "phase_accumulation": "PHASE BUILDUP",
        "tunnel_exit": "TUNNEL EXIT",
        "ownership_transition": "OWNERSHIP TRANSITION",
    }
    for signal in masks:
        text = labels.get(signal.name)
        if not text:
            continue
        x, y = label_anchor(signal.name, annotated.size)
        swatch = (*signal.color, 210)
        text_box = draw.textbbox((x, y), text, font=small_font, stroke_width=2)
        pad = 5
        draw.rounded_rectangle(
            (text_box[0] - pad, text_box[1] - pad, text_box[2] + pad, text_box[3] + pad),
            radius=4,
            fill=(10, 13, 16, 118),
            outline=swatch,
            width=1,
        )
        draw.text((x, y), text, font=small_font, fill=(248, 249, 242, 235), stroke_width=2, stroke_fill=(5, 7, 9, 185))

    legend_x = max(12, width - 206)
    legend_y = max(46, height - 112)
    for index, signal in enumerate([m for m in masks if m.name in labels][:5]):
        y = legend_y + index * 18
        draw.rectangle((legend_x, y + 3, legend_x + 10, y + 13), fill=(*signal.color, 185))
        draw.text((legend_x + 16, y), labels[signal.name], font=small_font, fill=(236, 238, 230, 225), stroke_width=1, stroke_fill=(0, 0, 0, 150))

    return annotated.convert("RGB")


def mask_stats(signal: SignalMask) -> dict[str, str | float]:
    gray = ImageOps.grayscale(signal.mask)
    hist = gray.histogram()
    total = gray.size[0] * gray.size[1]
    active = sum(hist[16:])
    weighted = sum(value * count for value, count in enumerate(hist)) / max(1, total)
    return {
        "region": signal.name,
        "label": signal.label,
        "source": signal.source,
        "coverage_pct": active / total * 100.0,
        "mean_signal_0_255": weighted,
        "note": signal.note,
    }


def write_metrics(path: Path, masks: list[SignalMask]) -> None:
    rows = [mask_stats(signal) for signal in masks]
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=["region", "label", "source", "coverage_pct", "mean_signal_0_255", "note"])
        writer.writeheader()
        for row in rows:
            writer.writerow(
                {
                    **row,
                    "coverage_pct": f"{row['coverage_pct']:.3f}",
                    "mean_signal_0_255": f"{row['mean_signal_0_255']:.3f}",
                }
            )


def write_summary(path: Path, input_dir: Path, base_source: str, masks: list[SignalMask], missing: list[str], outputs: dict[str, Path]) -> None:
    rows = [mask_stats(signal) for signal in masks]
    lines = [
        "# Resonance Chamber Overlay Summary",
        "",
        "This is a first-pass post-process overlay for xPRIMEray wormhole fixture output. It visualizes chamber-like transport behavior inspired by Schrodinger wave-packet tunneling and double-barrier / double-slit pop-culture physics language, while staying grounded in renderer telemetry.",
        "",
        "## Fixture",
        "",
        f"- Input directory: `{input_dir}`",
        f"- Base image: `{base_source}`",
        "",
        "## Generated Artifacts",
        "",
        f"- Overlay: `{outputs['overlay']}`",
        f"- Annotated overlay: `{outputs['annotated']}`",
        f"- Metrics CSV: `{outputs['metrics']}`",
        "",
        "## Region Interpretation",
        "",
    ]
    for row in rows:
        lines.append(
            f"- {row['label']} (`{row['region']}`): coverage={row['coverage_pct']:.2f}% mean_signal={row['mean_signal_0_255']:.2f}; source `{row['source']}`"
        )

    lines.extend(
        [
            "",
            "## Missing Or Placeholder Inputs",
            "",
        ]
    )
    if missing:
        lines.extend(f"- {item}" for item in sorted(set(missing)))
    else:
        lines.append("- None detected for this pass.")

    lines.extend(
        [
            "",
            "## Notes",
            "",
            "- Boundary confidence is treated as chamber wall interaction.",
            "- Throat events or portal ring-density imagery are treated as the resonant chamber core.",
            "- Sector report query density and step budget imagery are treated as phase accumulation.",
            "- Domain-id edges and selection flips are treated as ownership transitions.",
            "- Inbound and exit plume regions are deliberately soft spatial guides until direct transport direction maps are available.",
        ]
    )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    args = parse_args()
    input_dir = (args.input or latest_wormhole_output()).resolve()
    output_dir = (args.output or input_dir).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    base, base_source = choose_base_image(input_dir)
    masks, missing = build_signal_masks(input_dir, base.size)

    overlay = composite_overlay(base, masks)
    annotated = draw_annotation(overlay, masks)

    outputs = {key: output_dir / name for key, name in OUTPUT_NAMES.items()}
    overlay.save(outputs["overlay"])
    annotated.save(outputs["annotated"])
    write_metrics(outputs["metrics"], masks)
    write_summary(outputs["summary"], input_dir, base_source, masks, missing, outputs)

    print(f"[resonance_chamber_overlay] input={input_dir}")
    for key in ("overlay", "annotated", "summary", "metrics"):
        print(f"[resonance_chamber_overlay] {key}={outputs[key]}")
    if missing:
        print("[resonance_chamber_overlay] optional_missing=" + "; ".join(sorted(set(missing))))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
