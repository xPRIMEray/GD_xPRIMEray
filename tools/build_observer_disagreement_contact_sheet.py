#!/usr/bin/env python3
"""Build a restrained observer-disagreement cutsheet from a measured packet."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont


DEFAULT_PACKET_DIR = Path("output/observer_disagreement/offaxis_observe_delta")
PRIMARY_OUTPUT_NAME = "observability_cutsheet.png"
LEGACY_OUTPUT_NAME = "contact_sheet.png"

BG = (9, 14, 17)
PANEL = (14, 22, 27)
PANEL_INNER = (18, 29, 35)
LINE = (53, 86, 96)
LINE_SOFT = (34, 54, 62)
TEXT = (211, 226, 228)
TEXT_MUTED = (132, 155, 160)
CYAN = (89, 190, 210)
CYAN_SOFT = (66, 137, 151)
AMBER = (205, 157, 69)
RED_MUTED = (171, 82, 73)


def load_font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    candidates = [
        "/usr/share/fonts/truetype/dejavu/DejaVuSansMono-Bold.ttf" if bold else "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf" if bold else "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
    ]
    for candidate in candidates:
        path = Path(candidate)
        if path.exists():
            return ImageFont.truetype(str(path), size=size)
    return ImageFont.load_default()


FONT_TITLE = load_font(30, bold=True)
FONT_PANEL = load_font(17, bold=True)
FONT_SMALL = load_font(13)
FONT_METRIC = load_font(16)
FONT_METRIC_BOLD = load_font(18, bold=True)
FONT_MONO = load_font(14)


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def pct(value: float) -> str:
    return f"{value * 100:.1f}%"


def fit_text(draw: ImageDraw.ImageDraw, text: str, font: ImageFont.ImageFont, max_width: int) -> list[str]:
    words = text.split()
    lines: list[str] = []
    current = ""
    for word in words:
        trial = word if not current else f"{current} {word}"
        if draw.textbbox((0, 0), trial, font=font)[2] <= max_width:
            current = trial
        else:
            if current:
                lines.append(current)
            current = word
    if current:
        lines.append(current)
    return lines


def draw_panel(
    canvas: Image.Image,
    draw: ImageDraw.ImageDraw,
    xy: tuple[int, int],
    title: str,
    subtitle: str,
    image_path: Path,
) -> None:
    x, y = xy
    panel_w = 520
    panel_h = 336
    image_w = 480
    image_h = 270
    draw.rounded_rectangle((x, y, x + panel_w, y + panel_h), radius=6, fill=PANEL, outline=LINE_SOFT, width=1)
    draw.text((x + 20, y + 15), title, fill=TEXT, font=FONT_PANEL)
    draw.text((x + 20, y + 39), subtitle, fill=TEXT_MUTED, font=FONT_SMALL)

    img = Image.open(image_path).convert("RGBA")
    if img.size != (image_w, image_h):
        img = img.resize((image_w, image_h), Image.Resampling.NEAREST)
    base = Image.new("RGBA", (image_w, image_h), PANEL_INNER + (255,))
    base.alpha_composite(img)
    canvas.alpha_composite(base, (x + 20, y + 58))
    draw.rectangle((x + 20, y + 58, x + 20 + image_w, y + 58 + image_h), outline=LINE, width=1)


def draw_metric_row(
    draw: ImageDraw.ImageDraw,
    x: int,
    y: int,
    label: str,
    value: str,
    accent: tuple[int, int, int] = CYAN,
) -> int:
    draw.text((x, y), label.upper(), fill=TEXT_MUTED, font=FONT_SMALL)
    draw.text((x, y + 17), value, fill=accent, font=FONT_METRIC_BOLD)
    return y + 48


def draw_metrics_panel(
    draw: ImageDraw.ImageDraw,
    xy: tuple[int, int],
    summary: dict[str, Any],
) -> None:
    x, y = xy
    w = 420
    h = 1056
    draw.rounded_rectangle((x, y, x + w, y + h), radius=6, fill=PANEL, outline=LINE_SOFT, width=1)
    draw.text((x + 24, y + 24), "Metrics", fill=TEXT, font=FONT_PANEL)
    draw.text((x + 24, y + 50), "matched straight/curved transport assumptions", fill=TEXT_MUTED, font=FONT_SMALL)
    draw.line((x + 24, y + 78, x + w - 24, y + 78), fill=LINE_SOFT, width=1)

    image = summary["image"]
    metrics = summary["metrics"]
    yy = y + 100
    yy = draw_metric_row(draw, x + 24, yy, "resolution", f"{image['width']}x{image['height']}")
    yy = draw_metric_row(draw, x + 24, yy, "classification changed", f"{metrics['changed_pixels']:,} px")
    yy = draw_metric_row(draw, x + 24, yy, "changed ratio", pct(metrics["changed_ratio"]))
    yy = draw_metric_row(draw, x + 24, yy, "unresolved pixels", f"{metrics['unresolved_pixels']:,} px", RED_MUTED)
    yy = draw_metric_row(draw, x + 24, yy, "unresolved ratio", pct(metrics["unresolved_ratio"]), RED_MUTED)

    yy += 10
    draw.text((x + 24, yy), "TOP TRANSITIONS", fill=TEXT_MUTED, font=FONT_SMALL)
    yy += 24
    for bucket in summary.get("top_transition_buckets", [])[:2]:
        transition = bucket["transition"].replace("->", " -> ")
        count = f"{bucket['count']:,}"
        draw.text((x + 24, yy), transition, fill=TEXT, font=FONT_MONO)
        draw.text((x + w - 24 - draw.textbbox((0, 0), count, font=FONT_MONO)[2], yy), count, fill=AMBER, font=FONT_MONO)
        yy += 28

    yy += 12
    draw.line((x + 24, yy, x + w - 24, yy), fill=LINE_SOFT, width=1)
    yy += 24
    note = "beauty frames provide observatory context; terminal evidence redistributed in the classification buffers"
    for line in fit_text(draw, note, FONT_SMALL, w - 48):
        draw.text((x + 24, yy), line, fill=TEXT_MUTED, font=FONT_SMALL)
        yy += 19


def build(packet_dir: Path) -> Path:
    summary = read_json(packet_dir / "classification_delta_summary.json")
    manifest = read_json(packet_dir / "packet_manifest.json")
    if manifest["delta"]["status"] != "PASS":
        raise SystemExit("packet_manifest delta status is not PASS")
    beauty_frames = manifest.get("beauty_frames") or []
    if len(beauty_frames) != 2 or any(frame.get("status") != "PASS" for frame in beauty_frames):
        raise SystemExit("packet_manifest beauty_frames must contain two PASS entries")
    beauty_pair_contract = manifest.get("beauty_pair_contract") or {}
    if beauty_pair_contract and beauty_pair_contract.get("status") != "PASS":
        raise SystemExit("packet_manifest beauty_pair_contract status is not PASS")
    expected_beauty = [
        packet_dir / "straight_offaxis_observe_beauty.png",
        packet_dir / "grin_offaxis_observe_beauty.png",
    ]
    missing_beauty = [path.name for path in expected_beauty if not path.exists()]
    if missing_beauty:
        raise SystemExit(f"missing beauty frame(s): {', '.join(missing_beauty)}")

    canvas = Image.new("RGBA", (1600, 1300), BG + (255,))
    draw = ImageDraw.Draw(canvas)

    draw.text((56, 38), "Observer Disagreement Cutsheet", fill=TEXT, font=FONT_TITLE)
    draw.text(
        (56, 78),
        "measured off-axis observability packet",
        fill=TEXT_MUTED,
        font=FONT_METRIC,
    )
    draw.line((56, 116, 1544, 116), fill=LINE_SOFT, width=1)

    draw_panel(
        canvas,
        draw,
        (56, 144),
        "Straight Beauty",
        "resolved film",
        packet_dir / "straight_offaxis_observe_beauty.png",
    )
    draw_panel(
        canvas,
        draw,
        (600, 144),
        "Curved GRIN Beauty",
        "resolved film",
        packet_dir / "grin_offaxis_observe_beauty.png",
    )
    draw_panel(
        canvas,
        draw,
        (56, 504),
        "Straight Classification",
        "terminal evidence",
        packet_dir / "straight_offaxis_observe_transport_classification.png",
    )
    draw_panel(
        canvas,
        draw,
        (600, 504),
        "Curved GRIN Classification",
        "terminal evidence",
        packet_dir / "grin_offaxis_observe_transport_classification.png",
    )
    draw_panel(
        canvas,
        draw,
        (56, 864),
        "Delta Mask",
        "classification changed",
        packet_dir / "classification_delta.png",
    )
    draw_panel(
        canvas,
        draw,
        (600, 864),
        "Delta Contours",
        "terminal evidence redistributed",
        packet_dir / "classification_delta_contours.png",
    )
    draw_metrics_panel(draw, (1144, 144), summary)

    footer = (
        "presentation-only instrumentation  |  measured outputs only  |  no transport, scheduler, traversal, hit-selection, or oracle changes"
    )
    draw.text((56, 1242), footer, fill=TEXT_MUTED, font=FONT_SMALL)

    out_path = packet_dir / PRIMARY_OUTPUT_NAME
    legacy_path = packet_dir / LEGACY_OUTPUT_NAME
    rgb = canvas.convert("RGB")
    rgb.save(out_path)
    rgb.save(legacy_path)
    return out_path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packet-dir", type=Path, default=DEFAULT_PACKET_DIR)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    out_path = build(args.packet_dir)
    print(out_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
