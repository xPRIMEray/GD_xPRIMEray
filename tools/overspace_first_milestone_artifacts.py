#!/usr/bin/env python3
from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CONTACT_SHEET_SCRIPT = ROOT / "tools" / "build_visual_contact_sheet.py"
WSL_LAUNCHER = Path("/home/bb/code/godot_xPRIMEray/scripts/godot_local.sh")
RUN_ROOT = ROOT / "output" / "overspace_first_milestone" / "latest"
IMAGES_DIR = RUN_ROOT / "images"
LOGS_DIR = RUN_ROOT / "logs"

CASES = (
    {
        "id": "01_path_start",
        "title": "Path start",
        "description": "Initial camera pose before the auto-path begins moving toward the gallery orb.",
        "communicates": "Baseline framing for path-tuning. This is the exact starting pose that the auto-path logic inherits.",
        "progress": 0.0,
    },
    {
        "id": "02_path_20",
        "title": "20% path progress",
        "description": "Auto-path sample at the first evenly spaced progress stop toward the gallery orb.",
        "communicates": "Early approach framing. This helps tune acceleration, centering, and early overlay legibility.",
        "progress": 0.2,
    },
    {
        "id": "03_path_40",
        "title": "40% path progress",
        "description": "Auto-path sample at forty percent of the planned approach distance.",
        "communicates": "Mid-path stability check. This reveals whether the orb alignment converges cleanly before the close approach.",
        "progress": 0.4,
    },
    {
        "id": "04_path_60",
        "title": "60% path progress",
        "description": "Auto-path sample at sixty percent of the planned approach distance.",
        "communicates": "Late-mid path check. This is where motion and portal framing should begin to feel intentional rather than exploratory.",
        "progress": 0.6,
    },
    {
        "id": "05_path_80",
        "title": "80% path progress",
        "description": "Auto-path sample near the close approach to the gallery orb.",
        "communicates": "Pre-entry composition check. This shows whether the path gets us near the orb without losing diagnostic clarity.",
        "progress": 0.8,
    },
    {
        "id": "06_path_100",
        "title": "100% path progress",
        "description": "Final pre-entry approach sample at the end of the planned auto-path.",
        "communicates": "Terminal path framing. This is the best pre-traversal frame for deciding whether speed, offset, and aim need adjustment.",
        "progress": 1.0,
    },
)


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def run_contact_sheet(summary_path: Path, output_path: Path) -> None:
    subprocess.run(
        [
            sys.executable,
            str(CONTACT_SHEET_SCRIPT),
            "--summary",
            str(summary_path),
            "--output",
            str(output_path),
            "--title",
            "Overspace First Milestone Path Ladder",
            "--columns",
            "3",
        ],
        cwd=ROOT,
        check=True,
    )


def run_case(case: dict) -> dict:
    screenshot_rel = f"res://output/overspace_first_milestone/latest/images/{case['id']}.png"
    screenshot_path = IMAGES_DIR / f"{case['id']}.png"
    log_path = LOGS_DIR / f"{case['id']}.log"

    env = os.environ.copy()
    env["APPDATA"] = str(ROOT / ".appdata")
    env["LOCALAPPDATA"] = str(ROOT / ".localappdata")
    env["USERPROFILE"] = str(ROOT / ".userprofile")
    env["OVERSPACE_AUTOVALIDATE"] = "1"
    env["OVERSPACE_CAPTURE_PATH"] = screenshot_rel
    env["OVERSPACE_CAPTURE_PROGRESS"] = f"{case['progress']:.3f}"

    launcher = WSL_LAUNCHER if WSL_LAUNCHER.exists() else ROOT / "scripts" / "godot_local.sh"
    command = [
        "bash",
        str(launcher),
        "--path",
        ".",
        "--scene",
        "res://overspace_trophy_room_demo.tscn",
        "--",
        "--overspace-autovalidate",
        f"--overspace-capture-path={screenshot_rel}",
        f"--overspace-capture-progress={case['progress']:.3f}",
    ]

    with log_path.open("w", encoding="utf-8") as log_file:
        process = subprocess.run(
            command,
            cwd=ROOT,
            env=env,
            stdout=log_file,
            stderr=subprocess.STDOUT,
            timeout=120,
            check=False,
        )

    return {
        "caseId": case["id"],
        "status": "ok" if screenshot_path.exists() else "error",
        "title": case["title"],
        "description": case["description"],
        "communicates": case["communicates"],
        "screenshotPath": str(screenshot_path),
        "logPath": str(log_path),
        "contactSheetLabel": f"{case['id']}\n{case['title']}",
        "exitCode": process.returncode,
        "progress": case["progress"],
    }


def main() -> int:
    IMAGES_DIR.mkdir(parents=True, exist_ok=True)
    LOGS_DIR.mkdir(parents=True, exist_ok=True)

    cases = [run_case(case) for case in CASES]

    contact_sheet_summary_path = RUN_ROOT / "contact_sheet_summary.json"
    write_json(contact_sheet_summary_path, {"runDate": RUN_ROOT.name, "cases": cases})

    contact_sheet_path = RUN_ROOT / "overspace_first_milestone_contact_sheet.png"
    contact_sheet_error = None
    if any(case["status"] == "ok" for case in cases):
        try:
            run_contact_sheet(contact_sheet_summary_path, contact_sheet_path)
        except Exception as exc:
            contact_sheet_error = str(exc)
    elif contact_sheet_path.exists():
        contact_sheet_path.unlink()

    note_lines = [
        "# Overspace First Milestone Path Ladder",
        "",
        "This run captures one starting frame plus five evenly spaced auto-path samples so we can tune camera speed, offset, and orb approach composition.",
        "",
        "## Cases",
        "",
    ]
    for case in cases:
        note_lines.extend(
            [
                f"### {case['caseId']} — {case['title']}",
                "",
                f"- Progress target: {case['progress']:.1%}",
                f"- What it shows: {case['description']}",
                f"- What it communicates: {case['communicates']}",
                f"- Image: `images/{Path(case['screenshotPath']).name}`",
                f"- Log: `logs/{Path(case['logPath']).name}`",
                "",
            ]
        )

    note_path = RUN_ROOT / "storytelling_sequence.md"
    note_path.write_text("\n".join(note_lines), encoding="utf-8")

    summary = {
        "runRoot": str(RUN_ROOT),
        "cases": cases,
        "artifacts": {
            "contactSheet": str(contact_sheet_path) if contact_sheet_path.exists() else None,
            "contactSheetSummary": str(contact_sheet_summary_path),
            "storytellingNote": str(note_path),
            "contactSheetError": contact_sheet_error,
        },
    }
    write_json(RUN_ROOT / "summary.json", summary)

    print(f"[overspace_first_milestone] summary_saved path={RUN_ROOT / 'summary.json'}")
    return 0 if all(case["status"] == "ok" for case in cases) else 1


if __name__ == "__main__":
    raise SystemExit(main())
