import json
import os
import re
import subprocess
import sys
from datetime import date, datetime
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_GODOT_EXE = (
    r"C:\Users\wmbro\Downloads\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64"
    r"\Godot_v4.5.1-stable_mono_win64_console.exe"
)
SCREENSHOT_ROOT = ROOT / "screenshots" / "grin_basic_visual_sweep"
LOG_ROOT = ROOT / "logs" / "grin_basic_visual_sweep"
DEFAULT_SETTLE_FRAMES = 12
DEFAULT_MIN_RENDER_HEALTH_STEP = 12
DEFAULT_MIN_PROCESSED_ROWS = 0
DEFAULT_CAPTURE_FILM_OPACITY = 1.0
RESOLVED_RE = re.compile(
    r"\[GrinBasicVisual\].*?rOuter=(?P<router>-?\d+(?:\.\d+)?) "
    r"amp=(?P<amp>-?\d+(?:\.\d+)?) gamma=(?P<gamma>-?\d+(?:\.\d+)?)"
)
CAPTURE_RE = re.compile(r"\[GrinBasicVisual\]\[Capture\].*?path=(?P<path>\S+)")
FAIL_RE = re.compile(r"\[GrinBasicVisual\]\[Capture\]\[FAIL\].*")

CASES = [
    {"id": "straight_reference", "rung": "straight", "scene": "res://test-grin-basic-visual-straight.tscn", "launcher": "run_grin_basic_visual_straight", "overrides": {}},
    {"id": "minimal_baseline", "rung": "minimal", "scene": "res://test-grin-basic-visual-minimal.tscn", "launcher": "run_grin_basic_visual_minimal", "overrides": {}},
    {"id": "stronger_baseline", "rung": "stronger", "scene": "res://test-grin-basic-visual.tscn", "launcher": "run_grin_basic_visual", "overrides": {}},
    {"id": "minimal_amp_0p75", "rung": "minimal", "scene": "res://test-grin-basic-visual-minimal.tscn", "launcher": "run_grin_basic_visual_minimal", "overrides": {"amp": 0.75}},
    {"id": "minimal_amp_1p05", "rung": "minimal", "scene": "res://test-grin-basic-visual-minimal.tscn", "launcher": "run_grin_basic_visual_minimal", "overrides": {"amp": 1.05}},
    {"id": "minimal_router_4p5", "rung": "minimal", "scene": "res://test-grin-basic-visual-minimal.tscn", "launcher": "run_grin_basic_visual_minimal", "overrides": {"r_outer": 4.5}},
    {"id": "minimal_router_5p5", "rung": "minimal", "scene": "res://test-grin-basic-visual-minimal.tscn", "launcher": "run_grin_basic_visual_minimal", "overrides": {"r_outer": 5.5}},
    {"id": "minimal_gamma_1p4", "rung": "minimal", "scene": "res://test-grin-basic-visual-minimal.tscn", "launcher": "run_grin_basic_visual_minimal", "overrides": {"gamma": 1.4}},
    {"id": "minimal_gamma_2p2", "rung": "minimal", "scene": "res://test-grin-basic-visual-minimal.tscn", "launcher": "run_grin_basic_visual_minimal", "overrides": {"gamma": 2.2}},
    {"id": "stronger_amp_1p15", "rung": "stronger", "scene": "res://test-grin-basic-visual.tscn", "launcher": "run_grin_basic_visual", "overrides": {"amp": 1.15}},
    {"id": "stronger_amp_1p55", "rung": "stronger", "scene": "res://test-grin-basic-visual.tscn", "launcher": "run_grin_basic_visual", "overrides": {"amp": 1.55}},
    {"id": "stronger_router_5p5", "rung": "stronger", "scene": "res://test-grin-basic-visual.tscn", "launcher": "run_grin_basic_visual", "overrides": {"r_outer": 5.5}},
    {"id": "stronger_router_7p5", "rung": "stronger", "scene": "res://test-grin-basic-visual.tscn", "launcher": "run_grin_basic_visual", "overrides": {"r_outer": 7.5}},
    {"id": "stronger_gamma_2p0", "rung": "stronger", "scene": "res://test-grin-basic-visual.tscn", "launcher": "run_grin_basic_visual", "overrides": {"gamma": 2.0}},
    {"id": "stronger_gamma_2p8", "rung": "stronger", "scene": "res://test-grin-basic-visual.tscn", "launcher": "run_grin_basic_visual", "overrides": {"gamma": 2.8}},
]


def require_godot_exe() -> str:
    candidate = os.environ.get("GODOT_EXE", DEFAULT_GODOT_EXE)
    if not Path(candidate).exists():
        raise FileNotFoundError(
            f"GODOT_EXE not found at '{candidate}'. Set GODOT_EXE to a valid Godot console executable."
        )
    return candidate


def parse_log(text: str) -> dict:
    parsed = {
        "resolved": None,
        "capture_path_logged": None,
        "capture_failure": None,
    }
    for line in text.splitlines():
        resolved_match = RESOLVED_RE.search(line)
        if resolved_match:
            parsed["resolved"] = {
                "rOuter": float(resolved_match.group("router")),
                "amp": float(resolved_match.group("amp")),
                "gamma": float(resolved_match.group("gamma")),
            }
        capture_match = CAPTURE_RE.search(line)
        if capture_match:
            parsed["capture_path_logged"] = capture_match.group("path")
        fail_match = FAIL_RE.search(line)
        if fail_match:
            parsed["capture_failure"] = fail_match.group(0).strip()
    return parsed


def build_case_args(case: dict, screenshot_path: Path) -> list[str]:
    settle_frames = int(os.environ.get("GRIN_BASIC_SWEEP_SETTLE_FRAMES", str(DEFAULT_SETTLE_FRAMES)))
    min_rh_step = int(os.environ.get("GRIN_BASIC_SWEEP_MIN_RH_STEP", str(DEFAULT_MIN_RENDER_HEALTH_STEP)))
    min_processed_rows = int(os.environ.get("GRIN_BASIC_SWEEP_MIN_PROCESSED_ROWS", str(DEFAULT_MIN_PROCESSED_ROWS)))
    capture_film_opacity = float(os.environ.get("GRIN_BASIC_SWEEP_CAPTURE_FILM_OPACITY", str(DEFAULT_CAPTURE_FILM_OPACITY)))
    args = [
        "--grin-basic-capture=" + screenshot_path.as_posix(),
        f"--grin-basic-settle-frames={settle_frames}",
        f"--grin-basic-min-rh-step={min_rh_step}",
        f"--grin-basic-min-processed-rows={min_processed_rows}",
        f"--grin-basic-capture-film-opacity={capture_film_opacity}",
        "--grin-basic-exit-after-capture=1",
    ]
    overrides = case["overrides"]
    if "r_outer" in overrides:
        args.append(f"--grin-basic-r-outer={overrides['r_outer']}")
    if "amp" in overrides:
        args.append(f"--grin-basic-amp={overrides['amp']}")
    if "gamma" in overrides:
        args.append(f"--grin-basic-gamma={overrides['gamma']}")
    return args


def run_case(godot_exe: str, case: dict, screenshot_dir: Path, log_dir: Path) -> dict:
    screenshot_path = screenshot_dir / f"{case['id']}.png"
    log_path = log_dir / f"{case['id']}.log"
    cmd = [
        godot_exe,
        "--path",
        ".",
        "--scene",
        case["scene"],
        "--",
        *build_case_args(case, screenshot_path),
    ]
    env = os.environ.copy()
    env["XPRIMERAY_REQUESTED_LAUNCHER"] = case["launcher"]
    completed = subprocess.run(
        cmd,
        cwd=ROOT,
        env=env,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=600,
        check=False,
    )
    combined = completed.stdout + ("\n" + completed.stderr if completed.stderr else "")
    log_path.write_text(combined, encoding="utf-8")
    parsed = parse_log(combined)
    screenshot_exists = screenshot_path.exists()
    failure_reason = None
    if completed.returncode != 0:
        failure_reason = f"godot_exit_{completed.returncode}"
    elif parsed["capture_failure"]:
        failure_reason = parsed["capture_failure"]
    elif not screenshot_exists:
        failure_reason = "missing_screenshot"
    elif parsed["resolved"] is None:
        failure_reason = "missing_resolved_log"

    return {
        "caseId": case["id"],
        "rung": case["rung"],
        "scene": case["scene"],
        "launcher": case["launcher"],
        "requestedOverrides": case["overrides"],
        "resolved": parsed["resolved"],
        "screenshotPath": str(screenshot_path),
        "capturePathLogged": parsed["capture_path_logged"],
        "logFile": str(log_path),
        "exitCode": completed.returncode,
        "verdict": "pending_review",
        "status": "failed" if failure_reason else "ok",
        "failureReason": failure_reason,
    }


def main() -> int:
    godot_exe = require_godot_exe()
    run_date = date.today().isoformat()
    timestamp = datetime.now().strftime("%Y-%m-%dT%H:%M:%S")
    screenshot_dir = SCREENSHOT_ROOT / run_date
    log_dir = LOG_ROOT
    screenshot_dir.mkdir(parents=True, exist_ok=True)
    log_dir.mkdir(parents=True, exist_ok=True)

    summary = {
        "generatedAt": timestamp,
        "runDate": run_date,
        "screenshotDir": str(screenshot_dir),
        "cases": [],
        "stoppedOnFailure": False,
    }

    for case in CASES:
        print(f"RUN {case['id']}", flush=True)
        result = run_case(godot_exe, case, screenshot_dir, log_dir)
        summary["cases"].append(result)
        print(json.dumps(result, indent=2), flush=True)
        Path(LOG_ROOT / "summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
        if result["status"] != "ok":
            summary["stoppedOnFailure"] = True
            Path(LOG_ROOT / "summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
            print(f"STOP {case['id']} reason={result['failureReason']}", flush=True)
            return 1

    Path(LOG_ROOT / "summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(f"WROTE {LOG_ROOT / 'summary.json'}", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
