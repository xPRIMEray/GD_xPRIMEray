import json
import os
import re
import subprocess
import sys
import time
from datetime import date, datetime
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_GODOT_EXE = (
    r"C:\Users\wmbro\Downloads\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64"
    r"\Godot_v4.5.1-stable_mono_win64_console.exe"
)
SCREENSHOT_ROOT = ROOT / "screenshots" / "renderer_compact_regression_pack"
LOG_ROOT = ROOT / "logs" / "renderer_compact_regression_pack"
DEFAULT_SETTLE_FRAMES = 12
DEFAULT_MIN_RENDER_HEALTH_STEP = 20
DEFAULT_MIN_PROCESSED_ROWS = 64
DEFAULT_CAPTURE_FILM_OPACITY = 1.0
FAIL_RE = re.compile(r"\[GrinBasicVisual\]\[Capture\]\[FAIL\].*")

CASES = [
    {
        "id": "flat_baseline_no_field",
        "label": "flat baseline / no FieldSource3D",
        "scene": "res://test-grin-basic-visual-straight.tscn",
        "launcher": "run_grin_basic_visual_straight",
        "renderer": {},
        "packRole": "flat_baseline",
    },
    {
        "id": "grin_curved_minimal",
        "label": "minimal curved GRIN sphere",
        "scene": "res://test-grin-basic-visual-minimal.tscn",
        "launcher": "run_grin_basic_visual_minimal",
        "renderer": {},
        "packRole": "grin_curved_minimal",
    },
    {
        "id": "metric_minimal_offaxis_observe",
        "label": "metric observe off-axis",
        "scene": "res://test-metric-basic-visual-minimal-offaxis-observe.tscn",
        "launcher": "run_metric_basic_visual_minimal_offaxis_observe",
        "renderer": {},
        "packRole": "metric_observe_offaxis",
    },
]


def require_godot_exe() -> str:
    candidate = os.environ.get("GODOT_EXE", DEFAULT_GODOT_EXE)
    if not Path(candidate).exists():
        raise FileNotFoundError(f"GODOT_EXE not found at '{candidate}'.")
    return candidate


def scalar(token: str):
    if token is None:
        return None
    token = token.strip()
    if not token or token.lower() == "na":
        return None
    try:
        if any(ch in token for ch in ".eE"):
            return float(token)
        return int(token)
    except ValueError:
        return token


def parse_kv(line: str, prefix: str):
    idx = line.find(prefix)
    if idx < 0:
        return None
    result = {}
    for token in line[idx + len(prefix):].strip().split():
        if "=" not in token:
            continue
        key, value = token.split("=", 1)
        result[key] = scalar(value)
    return result


def parse_log(text: str) -> dict:
    parsed = {
        "capture": None,
        "renderer": None,
        "metricDiag": None,
        "renderHealth": None,
        "launchAudit": None,
        "captureFailure": None,
    }
    for line in text.splitlines():
        for key, prefix in {
            "capture": "[GrinBasicVisual][Capture]",
            "renderer": "[GrinBasicVisual][Renderer]",
            "metricDiag": "[GrinBasicVisual][MetricDiag]",
            "renderHealth": "[RenderHealth]",
            "launchAudit": "[LaunchAudit]",
        }.items():
            data = parse_kv(line, prefix)
            if data:
                parsed[key] = data
        fail = FAIL_RE.search(line)
        if fail:
            parsed["captureFailure"] = fail.group(0).strip()
    return parsed


def build_args(case_data: dict, screenshot_path: Path) -> list[str]:
    args = [
        f"--grin-basic-capture={screenshot_path.as_posix()}",
        f"--grin-basic-settle-frames={os.environ.get('COMPACT_BENCH_SETTLE_FRAMES', str(DEFAULT_SETTLE_FRAMES))}",
        f"--grin-basic-min-rh-step={os.environ.get('COMPACT_BENCH_MIN_RH_STEP', str(DEFAULT_MIN_RENDER_HEALTH_STEP))}",
        f"--grin-basic-min-processed-rows={os.environ.get('COMPACT_BENCH_MIN_PROCESSED_ROWS', str(DEFAULT_MIN_PROCESSED_ROWS))}",
        f"--grin-basic-capture-film-opacity={os.environ.get('COMPACT_BENCH_CAPTURE_FILM_OPACITY', str(DEFAULT_CAPTURE_FILM_OPACITY))}",
        f"--grin-basic-compare-grid={os.environ.get('COMPACT_BENCH_COMPARE_GRID', '1')}",
        f"--grin-basic-compare-crosshair={os.environ.get('COMPACT_BENCH_COMPARE_CROSSHAIR', '1')}",
        "--grin-basic-exit-after-capture=1",
    ]
    renderer = case_data.get("renderer", {})
    if "metric_gain" in renderer:
        args.append(f"--grin-basic-metric-gain={renderer['metric_gain']}")
    if "step_scale" in renderer:
        args.append(f"--grin-basic-step-scale={renderer['step_scale']}")
    if "steps_per_ray" in renderer:
        args.append(f"--grin-basic-steps-per-ray={renderer['steps_per_ray']}")
    return args


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def run_case(godot_exe: str, case_data: dict, screenshot_dir: Path, log_dir: Path) -> dict:
    screenshot_path = screenshot_dir / f"{case_data['id']}.png"
    log_path = log_dir / f"{case_data['id']}.log"
    cmd = [godot_exe, "--path", ".", "--scene", case_data["scene"], "--", *build_args(case_data, screenshot_path)]
    env = os.environ.copy()
    env["XPRIMERAY_REQUESTED_LAUNCHER"] = case_data["launcher"]
    settle_target = int(os.environ.get("COMPACT_BENCH_SETTLE_FRAMES", str(DEFAULT_SETTLE_FRAMES)))
    started = time.perf_counter()
    completed = subprocess.run(
        cmd,
        cwd=ROOT,
        env=env,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=900,
        check=False,
    )
    runtime_seconds = round(time.perf_counter() - started, 3)
    combined = completed.stdout + ("\n" + completed.stderr if completed.stderr else "")
    log_path.write_text(combined, encoding="utf-8")
    parsed = parse_log(combined)
    capture = parsed.get("capture") or {}
    capture_success = screenshot_path.exists() and parsed.get("captureFailure") is None and bool(capture)
    ready_frames = capture.get("readyFrames")
    failure = None
    if completed.returncode != 0:
        failure = f"godot_exit_{completed.returncode}"
    elif parsed.get("captureFailure"):
        failure = parsed["captureFailure"]
    elif not screenshot_path.exists():
        failure = "missing_screenshot"
    elif not capture:
        failure = "missing_capture_log"
    return {
        "caseId": case_data["id"],
        "label": case_data["label"],
        "packRole": case_data["packRole"],
        "scene": case_data["scene"],
        "launcher": case_data["launcher"],
        "requestedRendererOverrides": case_data.get("renderer", {}),
        "rendererConfig": parsed.get("renderer"),
        "metricDiagnostics": parsed.get("metricDiag"),
        "renderHealth": parsed.get("renderHealth"),
        "launchAudit": parsed.get("launchAudit"),
        "captureStats": {
            "tracedPixels": capture.get("tracedPixels"),
            "sourceHits": capture.get("sourceHits"),
            "backgroundHits": capture.get("backgroundHits"),
            "absorbedHits": capture.get("absorbedHits"),
            "missHits": capture.get("missHits"),
            "readyFrames": ready_frames,
            "rhStep": capture.get("rhStep"),
            "processedRows": capture.get("processedRows"),
        },
        "benchSummary": {
            "success": failure is None,
            "runtimeSeconds": runtime_seconds,
            "rowsProcessed": capture.get("processedRows"),
            "settleFramesTarget": settle_target,
            "settleFramesReached": ready_frames,
            "settleReached": isinstance(ready_frames, int) and ready_frames >= settle_target,
            "captureSucceeded": capture_success,
            "transportModelUsed": (parsed.get("renderer") or {}).get("transport"),
        },
        "screenshotPath": str(screenshot_path),
        "logPath": str(log_path),
        "status": "failed" if failure else "ok",
        "failureReason": failure,
        "exitCode": completed.returncode,
    }


def print_case_summary(result: dict) -> None:
    bench = result["benchSummary"]
    rows = bench.get("rowsProcessed")
    settle_reached = bench.get("settleFramesReached")
    settle_target = bench.get("settleFramesTarget")
    transport = bench.get("transportModelUsed") or "na"
    capture_token = "ok" if bench.get("captureSucceeded") else "fail"
    print(
        "SUMMARY "
        f"case={result['caseId']} "
        f"status={result['status']} "
        f"runtime_s={bench.get('runtimeSeconds')} "
        f"rows={rows if rows is not None else 'na'} "
        f"settle={settle_reached if settle_reached is not None else 'na'}/{settle_target} "
        f"capture={capture_token} "
        f"transport={transport}",
        flush=True,
    )


def main() -> int:
    godot_exe = require_godot_exe()
    run_date = date.today().isoformat()
    timestamp = datetime.now().strftime("%Y-%m-%dT%H:%M:%S")
    screenshot_dir = SCREENSHOT_ROOT / run_date
    screenshot_dir.mkdir(parents=True, exist_ok=True)
    LOG_ROOT.mkdir(parents=True, exist_ok=True)

    summary = {
        "generatedAt": timestamp,
        "runDate": run_date,
        "screenshotDir": str(screenshot_dir),
        "cases": [],
        "stoppedOnFailure": False,
    }

    for case_data in CASES:
        print(f"RUN {case_data['id']}", flush=True)
        result = run_case(godot_exe, case_data, screenshot_dir, LOG_ROOT)
        summary["cases"].append(result)
        write_json(LOG_ROOT / "summary.json", summary)
        print_case_summary(result)
        print(json.dumps(result["benchSummary"], indent=2), flush=True)
        if result["status"] != "ok":
            summary["stoppedOnFailure"] = True
            write_json(LOG_ROOT / "summary.json", summary)
            print(f"STOP {case_data['id']} reason={result['failureReason']}", flush=True)
            return 1

    write_json(LOG_ROOT / "summary.json", summary)
    print(f"WROTE {LOG_ROOT / 'summary.json'}", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
