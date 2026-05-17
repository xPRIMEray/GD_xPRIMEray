#!/usr/bin/env python3
"""Build a v0.0-pre GRIN Observe fallback MP4 from verified still artifacts."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = ROOT / "output" / "v0.0-pre"
VIDEO_DIR = OUT_DIR / "video"
DEFAULT_OUTPUT = VIDEO_DIR / "xprimeray_grin_observe_v0_pre_take001_fallback.mp4"
MANIFEST = VIDEO_DIR / "TAKE001_MANIFEST.md"

# Palette — shared across all card types for visual coherence.
_DARK_BG     = (8,   12,  18)
_PANEL_BG    = (14,  26,  36)
_SECTION_BG  = (10,  14,  18)
_ACCENT_TEAL = (91,  173, 191)
_ACCENT_GOLD = (255, 213, 91)
_TEXT_TITLE  = (236, 248, 255)
_TEXT_SUB    = (255, 221, 106)
_TEXT_BODY   = (218, 232, 240)
_TEXT_SMALL  = (170, 205, 222)
_TEXT_MUTED  = (130, 160, 175)
_BORDER      = (65,  92,  106)


@dataclass(frozen=True)
class TimelineItem:
    name: str
    kind: str
    seconds: float
    image: Image.Image
    artifact: Path | None = None
    note: str = ""


@dataclass(frozen=True)
class SectionTiming:
    name: str
    kind: str
    start: float
    end: float
    artifact: Path | None
    note: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", default=str(DEFAULT_OUTPUT))
    parser.add_argument("--fps", type=int, default=30)
    parser.add_argument("--width", type=int, default=1920)
    parser.add_argument("--height", type=int, default=1080)
    parser.add_argument("--seconds-per-card", type=float, default=3.5)
    parser.add_argument("--seconds-per-still", type=float, default=8.0)
    parser.add_argument("--method", default="fallback frames")
    parser.add_argument("--skip-manifest", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    VIDEO_DIR.mkdir(parents=True, exist_ok=True)
    output = Path(args.output)
    if not output.is_absolute():
        output = ROOT / output
    output.parent.mkdir(parents=True, exist_ok=True)

    frame_dir = VIDEO_DIR / "take001_fallback_frames"
    frame_dir.mkdir(parents=True, exist_ok=True)
    for old in frame_dir.glob("frame_*.png"):
        old.unlink()

    timeline = build_timeline(args)
    frame_index = 0
    timings: list[SectionTiming] = []
    elapsed = 0.0
    for item in timeline:
        frame_index = write_repeated_frames(frame_dir, frame_index, item.image, args.fps, item.seconds)
        timings.append(SectionTiming(item.name, item.kind, elapsed, elapsed + item.seconds, item.artifact, item.note))
        elapsed += item.seconds

    ffmpeg = resolve_ffmpeg()
    cmd = [
        ffmpeg, "-y",
        "-framerate", str(args.fps),
        "-i", str(frame_dir / "frame_%05d.png"),
        "-c:v", "libx264",
        "-pix_fmt", "yuv420p",
        "-r", str(args.fps),
        "-movflags", "+faststart",
        str(output),
    ]
    subprocess.run(cmd, cwd=ROOT, check=True)

    if not args.skip_manifest:
        write_manifest(output, args.method, args.width, args.height, args.fps, timings)

    print(f"[fallback-video] wrote {rel(output)}")
    return 0


def build_timeline(args: argparse.Namespace) -> list[TimelineItem]:
    w, h = args.width, args.height
    card_s  = args.seconds_per_card   # default 3.5
    still_s = args.seconds_per_still  # default 8.0

    straight    = OUT_DIR / "straight_control_verify.png"
    curved      = OUT_DIR / "curved_grin_verify.png"
    diagnostics = OUT_DIR / "curved_grin_final_smoke.png"
    for path in (straight, curved, diagnostics):
        if not path.exists():
            raise FileNotFoundError(f"missing required artifact: {rel(path)}")

    # Total at defaults: 6 + (3.5+8)×3 + 3.5 + 6 = 50.0s
    return [
        TimelineItem(
            "Title",
            "title card",
            6.0,
            title_card(
                w, h,
                "xPRIMEray v0.0-pre",
                "GRIN Basic Visual Off-Axis Observe",
                [
                    "Phase 2  ·  Instrument Validation",
                    "Straight / control vs curved GRIN — same fixture, same observer.",
                    "Purpose: coherent transport instrumentation.",
                    "No exotic physics proof is claimed.",
                ],
                footer="2026-05-17  ·  Take 001  ·  smoke-verified fallback payload",
            ),
            note="Opening title and scope.",
        ),
        TimelineItem(
            "Straight Control",
            "section card",
            card_s,
            section_card(
                w, h,
                "Straight Control",
                "Baseline fixture  ·  no refractive gradient",
                [
                    "Transport: straight.  Refractive gradient: none.",
                    "Film overlay active.  F1–F12 cockpit controls verified.",
                    "Establishes the observable baseline before gradient is applied.",
                ],
                section_num="01 / 04",
            ),
            note="Section card.",
        ),
        TimelineItem(
            "Straight Control Artifact",
            "verified still",
            still_s,
            compose_still(
                straight,
                "Straight Control",
                "Baseline export.  No refractive gradient.  Checksummed artifact.",
                w, h,
            ),
            artifact=straight,
            note="Baseline render artifact.",
        ),
        TimelineItem(
            "Curved GRIN Transport",
            "section card",
            card_s,
            section_card(
                w, h,
                "Curved GRIN Transport",
                "Matched comparison  ·  GRIN gradient active",
                [
                    "Transport: curved GRIN.  Same fixture, same observer.",
                    "Framing and film geometry held constant between scenes.",
                    "Observable: path bending and hit-pattern shift relative to control.",
                ],
                section_num="02 / 04",
            ),
            note="Section card.",
        ),
        TimelineItem(
            "Curved GRIN Artifact",
            "verified still",
            still_s,
            compose_still(
                curved,
                "Curved GRIN Transport",
                "Curved export.  Compare path bending and hit-pattern shift to the baseline.",
                w, h,
            ),
            artifact=curved,
            note="Curved render artifact.",
        ),
        TimelineItem(
            "Overlay / Diagnostics",
            "section card",
            card_s,
            section_card(
                w, h,
                "Overlay / Diagnostics",
                "Cockpit evidence path",
                [
                    "F1–F12 cockpit map verified in play mode.",
                    "F9: still packet capture.  F10: diagnostics JSON export.",
                    "Evidence chain: PNG stills → pixel coverage → diagnostics.json → manifest.",
                ],
                section_num="03 / 04",
            ),
            note="Section card.",
        ),
        TimelineItem(
            "Diagnostics Artifact",
            "verified still",
            still_s,
            compose_still(
                diagnostics,
                "Diagnostics Path",
                "Play-mode verification output.  Report, coverage, and diagnostics JSON checksummed.",
                w, h,
            ),
            artifact=diagnostics,
            note="Diagnostics artifact.",
        ),
        TimelineItem(
            "Release Gate",
            "section card",
            card_s,
            section_card(
                w, h,
                "Release Gate",
                "Before tagging v0.0-pre",
                [
                    "Full-pixel automation pending: run on faster GPU hardware before tagging.",
                    "Claim scope: instrumentation usability only.  No physics proof implied.",
                    "Release payload: logs, PNGs, video, and manifest with section timing.",
                ],
                section_num="04 / 04",
            ),
            note="Section card.",
        ),
        TimelineItem(
            "Manifest",
            "manifest card",
            6.0,
            title_card(
                w, h,
                "Artifact Manifest",
                "output/v0.0-pre/video/TAKE001_MANIFEST.md",
                [
                    "output/v0.0-pre/GRIN_OBSERVE_PLAYMODE_VERIFY.md",
                    "output/v0.0-pre/video/xprimeray_grin_observe_v0_pre_take001_fallback.mp4",
                    "scripts/run_grin_observe_v0_pre_full_pixel.sh  ←  pre-tag gate",
                ],
                footer="End of Take 001 fallback payload",
            ),
            artifact=MANIFEST,
            note="Final manifest path card.",
        ),
    ]


def title_card(
    width: int,
    height: int,
    title: str,
    subtitle: str,
    bullets: list[str],
    footer: str = "",
) -> Image.Image:
    image = Image.new("RGB", (width, height), _DARK_BG)
    draw  = ImageDraw.Draw(image)
    title_font    = load_font(72)
    subtitle_font = load_font(38)
    body_font     = load_font(30)
    small_font    = load_font(22)

    draw.rectangle((0, height - 96, width, height), fill=_PANEL_BG)
    draw.rectangle((0, 0, 8, height), fill=_ACCENT_TEAL)  # left accent bar

    draw.text((96, 108), title,    font=title_font,    fill=_TEXT_TITLE)
    draw.text((100, 202), subtitle, font=subtitle_font, fill=_TEXT_SUB)
    draw.line((100, 256, width - 100, 256), fill=_ACCENT_TEAL, width=1)

    y = 296
    for bullet in bullets:
        draw.text((116, y), f"·  {bullet}", font=body_font, fill=_TEXT_BODY)
        y += 56

    if footer:
        draw.text((96, height - 64), footer, font=small_font, fill=_TEXT_SMALL)
    return image


def section_card(
    width: int,
    height: int,
    title: str,
    subtitle: str,
    bullets: list[str],
    section_num: str = "",
) -> Image.Image:
    image = Image.new("RGB", (width, height), _SECTION_BG)
    draw  = ImageDraw.Draw(image)
    title_font    = load_font(72)
    subtitle_font = load_font(36)
    body_font     = load_font(29)
    small_font    = load_font(22)
    label_font    = load_font(20)

    # Left accent bars — teal + gold
    draw.rectangle((0,  0, 28, height), fill=_ACCENT_TEAL)
    draw.rectangle((28, 0, 38, height), fill=_ACCENT_GOLD)

    draw.text((108, 148), title,    font=title_font,    fill=_TEXT_TITLE)
    draw.text((112, 240), subtitle, font=subtitle_font, fill=_TEXT_SUB)
    draw.line((112, 292, width - 108, 292), fill=_ACCENT_TEAL, width=1)

    y = 336
    for bullet in bullets:
        draw.text((128, y), f"·  {bullet}", font=body_font, fill=_TEXT_BODY)
        y += 60

    draw.text((108, height - 64), "GRIN Observe  ·  Take 001  ·  section marker", font=label_font, fill=_TEXT_MUTED)

    if section_num:
        bbox = draw.textbbox((0, 0), section_num, font=small_font)
        tw = bbox[2] - bbox[0]
        draw.text((width - tw - 96, height - 64), section_num, font=small_font, fill=_ACCENT_TEAL)

    return image


def compose_still(path: Path, title: str, subtitle: str, width: int, height: int) -> Image.Image:
    image = Image.new("RGB", (width, height), (7, 10, 14))
    draw  = ImageDraw.Draw(image)
    title_font = load_font(50)
    body_font  = load_font(25)
    small_font = load_font(20)

    still = Image.open(path).convert("RGB")
    target_w = int(width  * 0.82)
    target_h = int(height * 0.70)
    scale = min(target_w / still.width, target_h / still.height)
    still = still.resize(
        (max(1, int(still.width * scale)), max(1, int(still.height * scale))),
        Image.Resampling.LANCZOS,
    )
    x = (width - still.width) // 2
    y = 192
    draw.rectangle((x - 3, y - 3, x + still.width + 3, y + still.height + 3), fill=_BORDER)
    image.paste(still, (x, y))

    draw.rectangle((0, 0, 6, height), fill=_ACCENT_TEAL)  # left accent bar
    draw.rectangle((0, height - 84, width, height), fill=_PANEL_BG)  # bottom panel

    draw.text((88, 56),          title,    font=title_font, fill=_TEXT_TITLE)
    draw.text((92, 122),         subtitle, font=body_font,  fill=_TEXT_BODY)
    draw.text((88, height - 58), f"Artifact: {rel(path)}", font=small_font, fill=_TEXT_SMALL)

    return image


def write_repeated_frames(frame_dir: Path, start: int, image: Image.Image, fps: int, seconds: float) -> int:
    count = max(1, int(round(fps * seconds)))
    for offset in range(count):
        image.save(frame_dir / f"frame_{start + offset:05d}.png")
    return start + count


def resolve_ffmpeg() -> str:
    for candidate in ("ffmpeg",):
        try:
            subprocess.run([candidate, "-version"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=True)
            return candidate
        except (OSError, subprocess.CalledProcessError):
            pass
    try:
        import imageio_ffmpeg
        return imageio_ffmpeg.get_ffmpeg_exe()
    except Exception as exc:  # pragma: no cover
        raise RuntimeError("ffmpeg unavailable; install ffmpeg or imageio-ffmpeg in the capture venv") from exc


def write_manifest(output: Path, method: str, width: int, height: int, fps: int, timings: list[SectionTiming]) -> None:
    now      = dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    status   = "PASS" if output.exists() and output.stat().st_size > 0 else "FAIL"
    sha      = sha256(output) if output.exists() else ""
    duration = timings[-1].end if timings else 0.0
    lines = [
        "# TAKE001 Manifest",
        "",
        f"- Timestamp UTC: `{now}`",
        f"- Status: `{status}`",
        f"- Capture method used: `{method}`",
        f"- Video artifact path: `{rel(output)}`",
        f"- Resolution/FPS/codec: `{width}x{height} @ {fps} FPS, H.264 MP4`",
        f"- Runtime: `{duration:.1f}s`",
        f"- Video SHA-256: `{sha}`",
        "- Verification report path: `output/v0.0-pre/GRIN_OBSERVE_PLAYMODE_VERIFY.md`",
        "",
        "## Section Timing",
        "",
        "| Start | End | Section | Type | Artifact / Note |",
        "| ---: | ---: | --- | --- | --- |",
    ]
    for t in timings:
        detail = rel(t.artifact) if t.artifact is not None else t.note
        lines.append(f"| `{format_time(t.start)}` | `{format_time(t.end)}` | {t.name} | {t.kind} | `{detail}` |")
    lines += ["", "## Screenshot Artifacts", ""]
    for t in timings:
        if t.artifact is not None and t.artifact.suffix.lower() == ".png":
            lines.append(f"- `{rel(t.artifact)}` - {t.name}")
    lines += [
        "",
        "## Known Limitations",
        "",
        "- This fallback MP4 is assembled from verified still artifacts, not live OBS window capture.",
        "- It communicates the v0.0-pre workflow and evidence packet but does not replace the operator OBS walkthrough.",
        "- Full-pixel release automation remains a pre-tag gate on faster GPU hardware.",
        "- Captions intentionally avoid unsupported physics claims.",
        "",
    ]
    MANIFEST.write_text("\n".join(lines), encoding="utf-8")


def format_time(seconds: float) -> str:
    minutes = int(seconds // 60)
    return f"{minutes:02d}:{seconds - minutes * 60:04.1f}"


def load_font(size: int) -> ImageFont.ImageFont:
    for path in (
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
    ):
        candidate = Path(path)
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size)
    return ImageFont.load_default()


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def fail(message: str) -> int:
    print(f"[fallback-video] ERROR: {message}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
