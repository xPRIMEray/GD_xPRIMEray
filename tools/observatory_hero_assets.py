#!/usr/bin/env python3
"""Generate GitHub Pages hero assets for Observatory Story reports."""

from __future__ import annotations

import shutil
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
REPORTS = ROOT / "reports"
FIXTURE_ROOT = REPORTS / "observatory_fixtures"
CURVATURE_ASSETS = REPORTS / "weekend_fps_curvature_sweep_assets"


PANEL_NAMES = [
    "Raw visual",
    "Scene geometry",
    "Curvature field",
    "Transport ownership",
    "Hit/miss map",
    "Traversal steps",
    "Budget stress",
    "Combined diagnostic",
    "Curvature signature",
]


FIXTURE_GALLERY = [
    ("Hermetic", "hermetic_curved_room"),
    ("Curved Minimal", "curved_minimal"),
    ("Object Island", "object_island"),
    ("Corner Probe Reference", "cathedral_probe"),
    ("Oracle (experimental)", "oracle_closure"),
]


def font(size: int, *, bold: bool = False) -> Any:
    candidates = (
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf" if bold else "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/truetype/liberation2/LiberationSans-Bold.ttf" if bold else "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
    )
    for path in candidates:
        try:
            return ImageFont.truetype(path, size)
        except Exception:
            continue
    return ImageFont.load_default()


def wrap(draw: ImageDraw.ImageDraw, text: str, max_width: int, fnt: Any) -> list[str]:
    lines: list[str] = []
    for source in text.splitlines():
        words = source.split()
        current = ""
        for word in words:
            candidate = word if not current else f"{current} {word}"
            if draw.textbbox((0, 0), candidate, font=fnt)[2] <= max_width:
                current = candidate
            else:
                if current:
                    lines.append(current)
                current = word
        if current:
            lines.append(current)
    return lines or [""]


def fit(image: Image.Image, size: tuple[int, int], bg: tuple[int, int, int] = (248, 249, 252)) -> Image.Image:
    src = image.convert("RGB")
    src.thumbnail(size, Image.Resampling.LANCZOS)
    canvas = Image.new("RGB", size, bg)
    canvas.paste(src, ((size[0] - src.width) // 2, (size[1] - src.height) // 2))
    return canvas


def make_story_reference() -> Path:
    out = REPORTS / "observatory_story_reference.png"
    cell_w, cell_h = 360, 230
    title_h = 42
    img = Image.new("RGB", (cell_w * 3, cell_h * 3), (246, 247, 250))
    draw = ImageDraw.Draw(img)
    num_font = font(34, bold=True)
    title_font = font(18, bold=True)
    small = font(12)
    colors = [
        (45, 75, 128),
        (32, 100, 105),
        (100, 68, 160),
        (25, 120, 150),
        (36, 150, 100),
        (210, 85, 35),
        (210, 185, 35),
        (120, 92, 72),
        (205, 55, 88),
    ]
    for idx, name in enumerate(PANEL_NAMES):
        row, col = divmod(idx, 3)
        x, y = col * cell_w, row * cell_h
        draw.rectangle((x, y, x + cell_w - 1, y + cell_h - 1), fill=(249, 250, 253), outline=(202, 207, 218))
        draw.rectangle((x, y, x + cell_w - 1, y + title_h - 1), fill=(231, 235, 244))
        draw.text((x + 12, y + 9), name, fill=(20, 25, 35), font=title_font)
        cx, cy = x + cell_w // 2, y + 124
        color = colors[idx]
        draw.rounded_rectangle((cx - 70, cy - 54, cx + 70, cy + 54), radius=12, fill=color, outline=(18, 22, 32), width=2)
        number = str(idx + 1)
        bbox = draw.textbbox((0, 0), number, font=num_font)
        draw.text((cx - (bbox[2] - bbox[0]) // 2, cy - (bbox[3] - bbox[1]) // 2 - 4), number, fill=(255, 255, 255), font=num_font)
        caption = "Read row-major: left to right, top to bottom."
        for line_i, line in enumerate(wrap(draw, caption, cell_w - 24, small)[:2]):
            draw.text((x + 12, y + cell_h - 40 + line_i * 14), line, fill=(75, 82, 96), font=small)
    out.parent.mkdir(parents=True, exist_ok=True)
    img.save(out)
    return out


def make_ladder() -> Path:
    src = CURVATURE_ASSETS / "curvature_signature_ladder.png"
    out = REPORTS / "curvature_signature_ladder.png"
    if src.exists():
        shutil.copy2(src, out)
        return out
    img = Image.new("RGB", (640, 124), (18, 20, 28))
    draw = ImageDraw.Draw(img)
    f = font(13, bold=True)
    for i, label in enumerate(("0%", "25%", "50%", "75%", "100%")):
        x = i * 128
        draw.rectangle((x, 0, x + 127, 23), fill=(28, 30, 42), outline=(68, 72, 88))
        draw.text((x + 48, 4), label, fill=(230, 232, 245), font=f)
        draw.rectangle((x + 6, 30, x + 121, 88), fill=(45 + i * 30, 48, 64), outline=(245, 246, 250))
    draw.text((8, 100), "Curvature signature ladder: blue = easier, black = no change, red = harder traversal", fill=(180, 185, 200), font=font(12))
    img.save(out)
    return out


def make_fixture_gallery() -> Path:
    out = REPORTS / "fixture_gallery_overview.png"
    tile_w, tile_h = 360, 318
    margin = 28
    header_h = 74
    width = margin * 2 + tile_w * 3
    height = header_h + margin + tile_h * 2 + margin
    img = Image.new("RGB", (width, height), (244, 246, 250))
    draw = ImageDraw.Draw(img)
    title_font = font(28, bold=True)
    label_font = font(17, bold=True)
    small = font(12)
    draw.text((margin, 22), "xPRIMEray Observatory Fixture Gallery", fill=(18, 22, 32), font=title_font)
    draw.text((margin, 54), "Canonical scenes rendered as comparable 3x3 Observatory Stories", fill=(76, 84, 99), font=small)
    for idx, (label, fixture) in enumerate(FIXTURE_GALLERY):
        row, col = divmod(idx, 3)
        x = margin + col * tile_w
        y = header_h + margin + row * tile_h
        draw.rectangle((x, y, x + tile_w - 14, y + tile_h - 14), fill=(255, 255, 255), outline=(202, 207, 218))
        draw.rectangle((x, y, x + tile_w - 14, y + 34), fill=(232, 235, 242))
        draw.text((x + 10, y + 8), label, fill=(18, 22, 32), font=label_font)
        sheet = FIXTURE_ROOT / fixture / "diagnostic_contact_sheet.png"
        if sheet.exists():
            preview = fit(Image.open(sheet), (tile_w - 34, tile_h - 58), bg=(255, 255, 255))
        else:
            preview = Image.new("RGB", (tile_w - 34, tile_h - 58), (24, 26, 34))
            pdraw = ImageDraw.Draw(preview)
            pdraw.text((16, 16), "MISSING SHEET", fill=(255, 215, 80), font=label_font)
        img.paste(preview, (x + 10, y + 42))
    img.save(out)
    return out


def make_methods() -> Path:
    out = REPORTS / "observatory_methods.md"
    out.write_text(
        "\n".join([
            "# Observatory Methods",
            "",
            "Observatory Story sheets are 3x3 diagnostic summaries for xPRIMEray fixtures. They are reporting artifacts only: they do not feed rendering, scheduling, hit selection, shading, resolver decisions, traversal, or adaptive precision.",
            "",
            "## How To Read An Observatory Story",
            "",
            "Read row-major: panels 1 to 3 across the top row, 4 to 6 across the middle row, and 7 to 9 across the bottom row.",
            "",
            "1. Raw visual: what the camera or beauty capture saw.",
            "2. Scene geometry: what objects, receivers, or probe geometry exist.",
            "3. Curvature field: what field or probe context is active.",
            "4. Transport ownership: where rays or samples resolved.",
            "5. Hit/miss map: whether rays found targets.",
            "6. Traversal steps: how much integration work was required.",
            "7. Budget stress: where traversal or precision budget was pressured.",
            "8. Combined diagnostic: the mission-control overview.",
            "9. Curvature signature: the field/probe effect summary.",
            "",
            "## How To Interpret Curvature Signature",
            "",
            "The Curvature signature panel is a delta view. It compares a current field/probe state against a baseline when that baseline exists. In the curvature sweep, red indicates pixels that required more traversal steps, blue indicates fewer steps, and dark/black indicates no measured change. It is a transport-effort map, not a photograph of the scene.",
            "",
            "The `curvature_signature_ladder.png` asset compresses the 0%, 25%, 50%, 75%, and 100% sweep into one README-friendly strip.",
            "",
            "## How To Interpret Closure",
            "",
            "Closure asks whether evaluated rays/pixels found valid targets under the scene contract. In a sealed hermetic room, misses are expected to be zero. A blank beauty image does not fail closure if hit diagnostics prove all evaluated rays hit; it fails visual-render confirmation instead.",
            "",
            "## How To Interpret Budget Stress",
            "",
            "Budget stress highlights rays or pixels that reached max-step, overrun-step, precision, or oracle refinement limits. A budget warning does not automatically mean a miss: it may mean the hit was found late or after an overrun warning. Treat it as a map of where transport was expensive or numerically fragile.",
            "",
            "## Missing Panels",
            "",
            "`MISSING / N.A.` tiles are intentional. They mean no existing artifact matched that Observatory panel for the selected fixture output. The reporting layer does not synthesize fake evidence.",
            "",
        ]) + "\n",
        encoding="utf-8",
    )
    return out


def main() -> int:
    outputs = [
        make_story_reference(),
        make_ladder(),
        make_fixture_gallery(),
        make_methods(),
    ]
    for path in outputs:
        print(f"[observatory-hero-assets] wrote {path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
