#!/usr/bin/env python3
"""Render Observer Storyboard v1 manifests.

This tool is renderer-agnostic by design. It consumes a JSON manifest and
produces a nine-panel PNG. It does not inspect renderer internals.
"""

from __future__ import annotations

import argparse
import json
import textwrap
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont


SCHEMA_ID = "xprimeray.observer_storyboard.v1"
STATUSES = {"PASS", "FAIL", "PARTIAL", "MISSING"}
PANEL_KEYS = [
    "observation",
    "assumptions",
    "perspectives",
    "disagreements",
    "closure_basin",
    "lineage",
    "coverage",
    "sensitivity_signature",
    "verdict",
]
PANEL_TITLES = [
    "Observation",
    "Assumptions",
    "Perspectives",
    "Disagreements",
    "Closure Basin",
    "Lineage",
    "Coverage",
    "Sensitivity Signature",
    "Verdict",
]
PANEL_QUESTIONS = [
    "What was directly observed?",
    "What must be true?",
    "Which viewpoints are compared?",
    "Where do they diverge?",
    "Where did evaluation close?",
    "What persisted or changed lineage?",
    "How much was evaluated?",
    "What changed under activation?",
    "Did the fixture contract pass?",
]


COLORS = {
    "ink": (24, 31, 42),
    "muted": (82, 94, 112),
    "bg": (244, 247, 251),
    "panel": (255, 255, 255),
    "border": (203, 213, 225),
    "canvas": (235, 240, 247),
    "blue": (37, 99, 235),
    "PASS": (22, 128, 65),
    "FAIL": (185, 28, 28),
    "PARTIAL": (217, 119, 6),
    "MISSING": (100, 116, 139),
}


def font(size: int, bold: bool = False) -> ImageFont.ImageFont:
    name = "DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf"
    path = Path("/usr/share/fonts/truetype/dejavu") / name
    if path.exists():
        return ImageFont.truetype(str(path), size)
    return ImageFont.load_default()


F_NUM = font(15, True)
F_TITLE = font(21, True)
F_STATUS = font(14, True)
F_BODY = font(15)
F_SMALL = font(12)
F_VERDICT = font(22, True)


def wrap_text(draw: ImageDraw.ImageDraw, text: str, max_width: int, text_font: ImageFont.ImageFont) -> list[str]:
    lines: list[str] = []
    for paragraph in str(text).splitlines() or [""]:
        words = paragraph.split()
        if not words:
            lines.append("")
            continue
        current = ""
        for word in words:
            candidate = word if not current else f"{current} {word}"
            width = draw.textbbox((0, 0), candidate, font=text_font)[2]
            if width <= max_width:
                current = candidate
            else:
                if current:
                    lines.append(current)
                current = word
        if current:
            lines.append(current)
    return lines


def validate_manifest(manifest: dict[str, Any]) -> None:
    if manifest.get("schema") != SCHEMA_ID:
        raise ValueError(f"manifest.schema must be {SCHEMA_ID!r}")
    panels = manifest.get("panels")
    if not isinstance(panels, list) or len(panels) != 9:
        raise ValueError("manifest.panels must contain exactly nine panels")
    slots = sorted(panel.get("slot") for panel in panels)
    if slots != list(range(1, 10)):
        raise ValueError("panel slots must be exactly 1..9")
    by_slot = sorted(panels, key=lambda panel: panel["slot"])
    for index, panel in enumerate(by_slot):
        expected_key = PANEL_KEYS[index]
        if panel.get("key") != expected_key:
            raise ValueError(f"slot {index + 1} key must be {expected_key!r}")
        status = panel.get("status")
        if status not in STATUSES:
            raise ValueError(f"slot {index + 1} status must be one of {sorted(STATUSES)}")


def placeholder_image(width: int, height: int, title: str, status: str) -> Image.Image:
    image = Image.new("RGB", (width, height), COLORS["canvas"])
    draw = ImageDraw.Draw(image)
    color = COLORS.get(status, COLORS["MISSING"])
    draw.rounded_rectangle((18, 18, width - 18, height - 18), radius=10, outline=color, width=3)
    draw.text((34, height // 2 - 28), status, font=F_VERDICT, fill=color)
    for i, line in enumerate(wrap_text(draw, title, width - 68, F_BODY)[:2]):
        draw.text((34, height // 2 + 8 + i * 20), line, font=F_BODY, fill=COLORS["muted"])
    return image


def load_artifact(path_text: str | None, base: Path, width: int, height: int, title: str, status: str) -> Image.Image:
    if not path_text:
        return placeholder_image(width, height, title, status)
    path = Path(path_text)
    if not path.is_absolute():
        path = base / path
    if not path.exists():
        return placeholder_image(width, height, f"Missing: {path_text}", "MISSING")
    try:
        source = Image.open(path).convert("RGB")
    except Exception:
        return placeholder_image(width, height, f"Invalid: {path_text}", "FAIL")
    canvas = Image.new("RGB", (width, height), COLORS["canvas"])
    source.thumbnail((width, height), Image.Resampling.LANCZOS)
    canvas.paste(source, ((width - source.width) // 2, (height - source.height) // 2))
    return canvas


def draw_status_badge(draw: ImageDraw.ImageDraw, xy: tuple[int, int], status: str) -> None:
    x, y = xy
    color = COLORS[status]
    label_w = draw.textbbox((0, 0), status, font=F_STATUS)[2] + 18
    draw.rounded_rectangle((x, y, x + label_w, y + 25), radius=8, fill=color)
    draw.text((x + 9, y + 5), status, font=F_STATUS, fill=(255, 255, 255))


def draw_panel(
    sheet: Image.Image,
    panel: dict[str, Any],
    manifest: dict[str, Any],
    manifest_base: Path,
    cell_w: int,
    cell_h: int,
) -> None:
    draw = ImageDraw.Draw(sheet)
    slot = int(panel["slot"])
    col = (slot - 1) % 3
    row = (slot - 1) // 3
    x = col * cell_w
    y = row * cell_h
    margin = 8
    px = x + margin
    py = y + margin
    pw = cell_w - margin * 2
    ph = cell_h - margin * 2
    draw.rounded_rectangle((px, py, px + pw, py + ph), radius=12, fill=COLORS["panel"], outline=COLORS["border"], width=2)

    badge_x = px + 14
    badge_y = py + 14
    draw.rounded_rectangle((badge_x, badge_y, badge_x + 28, badge_y + 28), radius=7, fill=(232, 239, 255), outline=(173, 196, 235))
    draw.text((badge_x + 10, badge_y + 5), str(slot), font=F_NUM, fill=COLORS["blue"])
    draw.text((px + 54, py + 16), panel.get("title", PANEL_TITLES[slot - 1]), font=F_TITLE, fill=COLORS["ink"])
    draw_status_badge(draw, (px + pw - 104, py + 16), panel["status"])

    image_x = px + 20
    image_y = py + 58
    image_w = pw - 40
    image_h = ph - 160
    if panel["key"] == "verdict":
        artifact = draw_verdict_tile(manifest, panel, image_w, image_h)
    else:
        artifact = load_artifact(panel.get("artifact"), manifest_base, image_w, image_h, panel.get("title", ""), panel["status"])
    sheet.paste(artifact, (image_x, image_y))
    draw.rectangle((image_x, image_y, image_x + image_w, image_y + image_h), outline=COLORS["border"])

    caption_y = image_y + image_h + 12
    question = panel.get("question", PANEL_QUESTIONS[slot - 1])
    caption = panel.get("caption", "")
    text = f"Question: {question}"
    if caption:
        text += f" {caption}"
    for index, line in enumerate(wrap_text(draw, text, pw - 40, F_BODY)[:4]):
        draw.text((image_x, caption_y + index * 20), line, font=F_BODY, fill=COLORS["muted"])


def draw_verdict_tile(manifest: dict[str, Any], panel: dict[str, Any], width: int, height: int) -> Image.Image:
    image = Image.new("RGB", (width, height), (248, 250, 252))
    draw = ImageDraw.Draw(image)
    verdict = manifest.get("verdict") or {}
    headline = verdict.get("headline") or panel.get("caption") or "Fixture Contract"
    status = verdict.get("status") or panel.get("status", "MISSING")
    draw.text((24, 24), headline, font=F_VERDICT, fill=COLORS["ink"])
    draw.text((24, 64), status, font=font(30, True), fill=COLORS[status])
    y = 112
    items = verdict.get("items") or []
    for item in items[:5]:
        item_status = item.get("status", "MISSING")
        label = item.get("label", "")
        value = item.get("value", item_status)
        draw.text((30, y), f"{label}:", font=F_BODY, fill=COLORS["ink"])
        draw.text((190, y), str(value), font=F_BODY, fill=COLORS[item_status])
        y += 28
    return image


def render_storyboard(manifest: dict[str, Any], output: Path, manifest_base: Path) -> None:
    validate_manifest(manifest)
    cell_w = 500
    cell_h = 450
    title_h = 92
    sheet = Image.new("RGB", (cell_w * 3, cell_h * 3 + title_h), COLORS["bg"])
    draw = ImageDraw.Draw(sheet)
    draw.text((28, 24), manifest.get("title", "Observer Storyboard v1"), font=font(30, True), fill=COLORS["ink"])
    subtitle = manifest.get("subtitle", "Renderer-agnostic nine-panel evidence story")
    draw.text((30, 62), subtitle, font=F_BODY, fill=COLORS["muted"])

    panels_sheet = Image.new("RGB", (cell_w * 3, cell_h * 3), COLORS["bg"])
    for panel in sorted(manifest["panels"], key=lambda item: item["slot"]):
        draw_panel(panels_sheet, panel, manifest, manifest_base, cell_w, cell_h)
    sheet.paste(panels_sheet, (0, title_h))
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output)


def demo_manifest() -> dict[str, Any]:
    statuses = ["PASS", "PASS", "PARTIAL", "PASS", "PASS", "PARTIAL", "PASS", "PASS", "PASS"]
    captions = [
        "Primary observation artifact is present.",
        "Contract and scope are explicitly declared.",
        "Two perspectives are available; third is pending.",
        "Disagreement map is available and bounded.",
        "Closure basin reports terminal classification.",
        "Lineage exists but is not yet complete.",
        "Coverage metrics are present.",
        "Activation delta is visible.",
        "Fixture contract is satisfied for this demo.",
    ]
    panels = []
    for index, key in enumerate(PANEL_KEYS):
        panels.append({
            "slot": index + 1,
            "key": key,
            "title": PANEL_TITLES[index],
            "question": PANEL_QUESTIONS[index],
            "status": statuses[index],
            "caption": captions[index],
        })
    return {
        "schema": SCHEMA_ID,
        "title": "Observer Storyboard v1 Demo",
        "subtitle": "Framework-only demo; not connected to renderer output",
        "source": "demo",
        "panels": panels,
        "verdict": {
            "status": "PASS",
            "headline": "Demo Verdict",
            "items": [
                {"label": "Observation", "status": "PASS", "value": "PASS"},
                {"label": "Coverage", "status": "PASS", "value": "PASS"},
                {"label": "Closure", "status": "PASS", "value": "PASS"},
                {"label": "Lineage", "status": "PARTIAL", "value": "PARTIAL"},
                {"label": "Contract", "status": "PASS", "value": "SATISFIED"}
            ]
        }
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Render Observer Storyboard v1 PNGs.")
    parser.add_argument("--manifest", type=Path, help="Observer Storyboard v1 manifest JSON.")
    parser.add_argument("--output", type=Path, required=True, help="Output PNG path.")
    parser.add_argument("--demo", action="store_true", help="Render built-in framework demo.")
    args = parser.parse_args()

    if args.demo:
        manifest = demo_manifest()
        manifest_base = Path.cwd()
    elif args.manifest:
        manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
        manifest_base = args.manifest.parent
    else:
        parser.error("provide --demo or --manifest")

    render_storyboard(manifest, args.output, manifest_base)
    print(args.output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
