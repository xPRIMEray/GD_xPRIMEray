#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CONTACT_SHEET_SCRIPT = ROOT / "tools" / "build_visual_contact_sheet.py"

CASES = (
    {
        "id": "01_clean_curved",
        "title": "Clean curved render",
        "description": "Observer-facing curved film render with no storytelling overlays.",
        "communicates": "Baseline perception. This is the curved wormhole view before interpretive scaffolding is added.",
    },
    {
        "id": "02_reference_reality",
        "title": "Reference Reality / straight transport view",
        "description": "Curved main render with the straight-path Reference Reality inset frozen for side-by-side comparison.",
        "communicates": "Perceptual contrast. It shows what the scene would look like under straight transport and exposes the wormhole distortion as a difference against the main view.",
    },
    {
        "id": "03_curvature_map",
        "title": "Curvature heat map only",
        "description": "Fullscreen pass-1 curvature heat map using cumulative absolute turn angle, without semantic or collision overlays.",
        "communicates": "Primary transport interpretation. It highlights where rays accumulate bending, without additional annotation competing for attention.",
    },
    {
        "id": "04_curvature_plus_semantic",
        "title": "Curvature plus semantic glyphs",
        "description": "Curvature heat map combined with semantic field / BLV / portal glyph overlays.",
        "communicates": "Structural interpretation. It helps relate annular high-curvature regions to portal-local geometry and semantic landmarks.",
    },
    {
        "id": "05_curvature_plus_collision",
        "title": "Curvature plus collision radar",
        "description": "Curvature heat map combined with collision radar labels for visible collision objects.",
        "communicates": "Geometry-query interpretation. It shows where strong bending and visible collision structure do or do not line up.",
    },
    {
        "id": "06_full_stack",
        "title": "Full stack",
        "description": "Reference Reality inset plus curvature, semantic glyphs, and collision radar overlays in one composed storytelling frame.",
        "communicates": "Integrated interpretation. This is the most information-dense frame and closes the ladder by stacking perception, transport, and structure together.",
    },
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate captions and a contact sheet for the wormhole DualRealityTransport storytelling ladder."
    )
    parser.add_argument("--run-root", type=Path, required=True)
    return parser.parse_args()


def build_contact_sheet(summary_path: Path, output_path: Path, title: str, columns: int) -> None:
    subprocess.run(
        [
            sys.executable,
            str(CONTACT_SHEET_SCRIPT),
            "--summary",
            str(summary_path),
            "--output",
            str(output_path),
            "--title",
            title,
            "--columns",
            str(columns),
        ],
        cwd=ROOT,
        check=True,
    )


def write_json(path: Path, payload: dict) -> None:
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def main() -> int:
    args = parse_args()
    run_root = args.run_root
    images_dir = run_root / "images"
    logs_dir = run_root / "logs"

    cases = []
    for case in CASES:
        image_path = images_dir / f"{case['id']}.png"
        log_path = logs_dir / f"{case['id']}.log"
        if not image_path.exists():
            raise FileNotFoundError(f"Missing storytelling image: {image_path}")
        if not log_path.exists():
            raise FileNotFoundError(f"Missing storytelling log: {log_path}")

        cases.append(
            {
                "caseId": case["id"],
                "status": "ok",
                "title": case["title"],
                "description": case["description"],
                "communicates": case["communicates"],
                "screenshotPath": str(image_path),
                "logPath": str(log_path),
                "contactSheetLabel": f"{case['id']}\n{case['title']}",
            }
        )

    contact_sheet_summary = {
        "runDate": run_root.name,
        "cases": cases,
    }
    contact_sheet_summary_path = run_root / "contact_sheet_summary.json"
    write_json(contact_sheet_summary_path, contact_sheet_summary)

    contact_sheet_path = run_root / "wormhole_dual_reality_storytelling_contact_sheet.png"
    build_contact_sheet(
        contact_sheet_summary_path,
        contact_sheet_path,
        "Wormhole DualRealityTransport Storytelling Ladder",
        columns=3,
    )

    note_lines = [
        "# Wormhole DualRealityTransport Storytelling Sequence",
        "",
        "This ladder is a small, repeatable storytelling sequence for explaining the wormhole-side `DualRealityTransport` stack from perception to interpretation.",
        "",
        "## Sequence",
        "",
    ]
    for case in cases:
        note_lines.extend(
            [
                f"### {case['caseId']} — {case['title']}",
                "",
                f"- What it shows: {case['description']}",
                f"- What it communicates: {case['communicates']}",
                f"- Image: `images/{Path(case['screenshotPath']).name}`",
                "",
            ]
        )

    note_lines.extend(
        [
            "## Artifacts",
            "",
            f"- Contact sheet: `{contact_sheet_path.relative_to(run_root)}`",
            f"- Contact sheet summary: `{contact_sheet_summary_path.relative_to(run_root)}`",
            "",
            "## Ordering Intent",
            "",
            "- `01` starts with perception alone.",
            "- `02` introduces the straight-transport comparison layer.",
            "- `03` isolates the curvature metric as the main interpretive diagnostic.",
            "- `04` and `05` connect curvature to semantic and collision structure separately.",
            "- `06` closes with the full interpretive stack.",
        ]
    )

    note_path = run_root / "storytelling_sequence.md"
    note_path.write_text("\n".join(note_lines) + "\n", encoding="utf-8")

    summary = {
        "runRoot": str(run_root),
        "cases": cases,
        "artifacts": {
            "contactSheet": str(contact_sheet_path),
            "contactSheetSummary": str(contact_sheet_summary_path),
            "storytellingNote": str(note_path),
        },
    }
    write_json(run_root / "summary.json", summary)
    print(f"[wormhole_dual_reality_storytelling] summary_saved path={run_root / 'summary.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
