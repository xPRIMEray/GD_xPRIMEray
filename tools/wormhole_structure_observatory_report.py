#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path


SAFETY_SENTENCE = "This observatory visualizes renderer transport structure, not physical wormhole evidence."
BASE_PANEL_ORDER = ["clean_curved", "straight_vs_curved", "depth_heatmap"]
EXTRA_PANEL_ORDER = ["step_budget_heatmap", "domain_diagnostics", "structure_minimap"]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build Wormhole Structure Observatory report artifacts.")
    parser.add_argument("run_root", type=Path)
    parser.add_argument("--quality", default="quick_review")
    parser.add_argument("--quality-unavailable", action="store_true")
    return parser.parse_args()


def read_json(path: Path) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return {}


def load_panels(run_root: Path) -> list[dict]:
    panels = []
    for panel_id in BASE_PANEL_ORDER + EXTRA_PANEL_ORDER:
        status_path = run_root / "panels" / panel_id / "panel_status.json"
        status = read_json(status_path)
        if status:
            panels.append(status)
        elif panel_id in BASE_PANEL_ORDER:
            panels.append(
                {
                    "panel_id": panel_id,
                    "title": panel_id.replace("_", " "),
                    "status": "missing",
                    "exit_code": -1,
                    "duration_seconds": 0.0,
                    "timed_out": False,
                    "image": "",
                    "log": "",
                    "notes": "panel_not_run",
                }
            )
    return panels


def build_contact_sheet(run_root: Path, panels: list[dict]) -> str:
    try:
        from PIL import Image, ImageDraw, ImageFont
    except Exception:
        return ""

    thumb_w, thumb_h = 320, 180
    label_h = 74
    pad = 12
    columns = 3
    rows = max(1, (len(panels) + columns - 1) // columns)
    sheet_w = pad + columns * (thumb_w + pad)
    sheet_h = pad + rows * (thumb_h + label_h + pad)
    sheet = Image.new("RGB", (sheet_w, sheet_h), (18, 22, 32))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()

    for idx, panel in enumerate(panels):
        col = idx % columns
        row = idx // columns
        x = pad + col * (thumb_w + pad)
        y = pad + row * (thumb_h + label_h + pad)
        image_path = Path(panel.get("image") or "")
        if panel.get("status") == "ok" and image_path.exists():
            image = Image.open(image_path).convert("RGB")
            image.thumbnail((thumb_w, thumb_h), Image.Resampling.LANCZOS)
            ox = x + (thumb_w - image.width) // 2
            oy = y + (thumb_h - image.height) // 2
            sheet.paste(image, (ox, oy))
        else:
            draw.rectangle([x, y, x + thumb_w, y + thumb_h], fill=(36, 38, 46), outline=(104, 84, 46))
            draw.text((x + 12, y + 78), "panel incomplete", fill=(255, 196, 90), font=font)

        band_y = y + thumb_h
        draw.rectangle([x, band_y, x + thumb_w, band_y + label_h], fill=(24, 28, 38))
        title = str(panel.get("title") or panel.get("panel_id"))
        status = str(panel.get("status", "missing"))
        duration = float(panel.get("duration_seconds") or 0.0)
        notes = str(panel.get("notes") or "")
        draw.text((x + 8, band_y + 8), title, fill=(235, 240, 248), font=font)
        draw.text((x + 8, band_y + 28), f"status={status} duration={duration:.0f}s", fill=(170, 210, 255), font=font)
        draw.text((x + 8, band_y + 48), notes[:46], fill=(190, 198, 210), font=font)

    out = run_root / "wormhole_structure_contact_sheet.png"
    sheet.save(out)
    return str(out)


def write_report(run_root: Path, panels: list[dict], summary: dict) -> str:
    lines = [
        "# Wormhole Structure Observatory",
        "",
        "Visual observatory only; not a validation verdict.",
        "",
        SAFETY_SENTENCE,
        "",
    ]
    contact_sheet = summary.get("artifacts", {}).get("contact_sheet", "")
    if contact_sheet:
        lines.extend(["![contact sheet](wormhole_structure_contact_sheet.png)", ""])

    lines.extend(
        [
            "## Panels",
            "",
            "| panel | status | duration_seconds | timed_out | notes |",
            "| --- | --- | ---: | --- | --- |",
        ]
    )
    for panel in panels:
        lines.append(
            f"| `{panel.get('panel_id')}` | `{panel.get('status')}` | "
            f"{float(panel.get('duration_seconds') or 0.0):.3f} | "
            f"{str(bool(panel.get('timed_out'))).lower()} | {panel.get('notes', '')} |"
        )
    lines.extend(
        [
            "",
            "## Artifacts",
            "",
            f"- Contact sheet: `{Path(contact_sheet).name if contact_sheet else 'missing'}`",
            "- Preview alias: `testbench_preview.png`",
            "- Summary: `wormhole_structure_summary.json`",
        ]
    )
    out = run_root / "wormhole_structure_report.md"
    out.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return str(out)


def main() -> int:
    args = parse_args()
    run_root = args.run_root
    run_root.mkdir(parents=True, exist_ok=True)

    panels = load_panels(run_root)
    if args.quality_unavailable:
        for panel in panels:
            panel["status"] = "not_run"
            panel["notes"] = f"quality_{args.quality}_reserved_for_later"

    complete = sum(1 for panel in panels if panel.get("status") == "ok")
    extra_enabled = any(panel.get("panel_id") in EXTRA_PANEL_ORDER for panel in panels)
    total_duration = sum(float(panel.get("duration_seconds") or 0.0) for panel in panels)
    contact_sheet = build_contact_sheet(run_root, panels)
    preview = ""
    if contact_sheet:
        preview_path = run_root / "testbench_preview.png"
        shutil.copyfile(contact_sheet, preview_path)
        preview = str(preview_path)

    summary = {
        "schema": "xprimeray.wormhole_structure_observatory.v1",
        "purpose": "visual_observatory_only",
        "safety_note": SAFETY_SENTENCE,
        "quality": args.quality,
        "extra_panels_enabled": extra_enabled,
        "panel_count": len(panels),
        "complete_panel_count": complete,
        "all_panels_complete": complete == len(panels),
        "total_panel_duration_seconds": total_duration,
        "panels": panels,
        "artifacts": {
            "contact_sheet": contact_sheet,
            "preview": preview,
            "report": str(run_root / "wormhole_structure_report.md"),
            "summary": str(run_root / "wormhole_structure_summary.json"),
        },
    }
    report = write_report(run_root, panels, summary)
    summary["artifacts"]["report"] = report
    summary_path = run_root / "wormhole_structure_summary.json"
    summary_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(f"[wormhole-structure-report] panels={complete}/{len(panels)} contact_sheet={contact_sheet or 'missing'}")
    return 0 if complete == len(panels) and not args.quality_unavailable else 2


if __name__ == "__main__":
    raise SystemExit(main())
