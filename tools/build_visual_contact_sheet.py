import argparse
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_BG = (18, 22, 32)
DEFAULT_PANEL = (30, 36, 48)
DEFAULT_TEXT = (235, 240, 248)
DEFAULT_MUTED = (150, 160, 178)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a labeled contact sheet from a sweep summary.json file.")
    parser.add_argument("--summary", required=True, help="Path to the sweep summary.json file.")
    parser.add_argument("--output", required=True, help="Output image path.")
    parser.add_argument("--title", required=True, help="Title shown at the top of the sheet.")
    parser.add_argument("--columns", type=int, default=3, help="Number of columns in the sheet.")
    parser.add_argument("--tile-width", type=int, default=320, help="Width of each thumbnail tile.")
    parser.add_argument("--padding", type=int, default=20, help="Outer and inner padding.")
    return parser.parse_args()


def measure_text(draw: ImageDraw.ImageDraw, text: str, font: ImageFont.ImageFont) -> tuple[int, int]:
    box = draw.multiline_textbbox((0, 0), text, font=font, spacing=4)
    return box[2] - box[0], box[3] - box[1]


def build_tile(
    image_path: Path,
    label: str,
    font: ImageFont.ImageFont,
    tile_width: int,
) -> Image.Image:
    source = Image.open(image_path).convert("RGB")
    target_height = round(source.height * (tile_width / source.width))
    preview = ImageOps.contain(source, (tile_width, target_height), method=Image.Resampling.LANCZOS)

    scratch = Image.new("RGB", (tile_width, 10))
    scratch_draw = ImageDraw.Draw(scratch)
    label_lines = label.strip()
    _, label_height = measure_text(scratch_draw, label_lines, font)
    label_band = label_height + 20

    tile = Image.new("RGB", (tile_width, preview.height + label_band), DEFAULT_PANEL)
    tile_draw = ImageDraw.Draw(tile)
    tile.paste(preview, (0, 0))
    tile_draw.rectangle((0, 0, tile.width - 1, tile.height - 1), outline=(66, 77, 96), width=1)
    tile_draw.multiline_text((12, preview.height + 10), label_lines, font=font, fill=DEFAULT_TEXT, spacing=4)
    return tile


def main() -> int:
    args = parse_args()
    summary_path = Path(args.summary)
    output_path = Path(args.output)
    summary = json.loads(summary_path.read_text(encoding="utf-8"))

    cases = [case for case in summary.get("cases", []) if case.get("status") == "ok"]
    if not cases:
        raise SystemExit(f"No successful cases found in {summary_path}")

    font = ImageFont.load_default()
    tiles = []
    for case in cases:
        image_path = Path(case["screenshotPath"])
        label = f"{case['caseId']}\n{case['rung']}"
        resolved = case.get("resolved") or {}
        if resolved:
            label += f"\nr={resolved.get('rOuter')} a={resolved.get('amp')} g={resolved.get('gamma')}"
        tiles.append(build_tile(image_path, label, font, args.tile_width))

    columns = max(1, args.columns)
    rows = math.ceil(len(tiles) / columns)
    padding = max(0, args.padding)
    tile_width = max(tile.width for tile in tiles)
    tile_height = max(tile.height for tile in tiles)

    title_probe = Image.new("RGB", (10, 10))
    title_draw = ImageDraw.Draw(title_probe)
    title_font = font
    title_w, title_h = measure_text(title_draw, args.title, title_font)
    subtitle = f"{summary.get('runDate', 'unknown date')}  cases={len(cases)}"
    subtitle_w, subtitle_h = measure_text(title_draw, subtitle, font)
    header_h = title_h + subtitle_h + 26

    canvas_w = padding * 2 + columns * tile_width + (columns - 1) * padding
    canvas_h = padding * 2 + header_h + rows * tile_height + (rows - 1) * padding
    canvas = Image.new("RGB", (canvas_w, canvas_h), DEFAULT_BG)
    draw = ImageDraw.Draw(canvas)
    draw.text((padding, padding), args.title, font=title_font, fill=DEFAULT_TEXT)
    draw.text((padding, padding + title_h + 8), subtitle, font=font, fill=DEFAULT_MUTED)

    for index, tile in enumerate(tiles):
        row = index // columns
        col = index % columns
        x = padding + col * (tile_width + padding)
        y = padding + header_h + row * (tile_height + padding)
        canvas.paste(tile, (x, y))

    output_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output_path)
    print(output_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
