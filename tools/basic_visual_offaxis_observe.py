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
BASELINE_SUMMARY_PATH = ROOT / "logs" / "basic_visual_offaxis" / "summary.json"
SCREENSHOT_ROOT = ROOT / "screenshots" / "basic_visual_offaxis_observe"
LOG_ROOT = ROOT / "logs" / "basic_visual_offaxis_observe"
CONTACT_SHEET_SCRIPT = ROOT / "tools" / "build_visual_contact_sheet.py"
DEFAULT_SETTLE_FRAMES = 12
DEFAULT_MIN_RENDER_HEALTH_STEP = 20
DEFAULT_FULL_COVERAGE_ROWS = 270
DEFAULT_MIN_PROCESSED_ROWS = DEFAULT_FULL_COVERAGE_ROWS
DEFAULT_CAPTURE_FILM_OPACITY = 1.0
FAIL_RE = re.compile(r"\[GrinBasicVisual\]\[Capture\]\[FAIL\].*")

CASES = [
    {
        "id": "straight_offaxis_observe_reference",
        "label": "straight reference",
        "baselineId": "straight_offaxis_reference",
        "scene": "res://test-straight-basic-visual-offaxis-observe.tscn",
        "launcher": "run_grin_basic_visual_straight_offaxis_observe",
        "renderer": {},
        "isMetric": False,
    },
    {
        "id": "grin_minimal_offaxis_observe",
        "label": "GRIN minimal",
        "baselineId": "grin_minimal_offaxis",
        "scene": "res://test-grin-basic-visual-minimal-offaxis-observe.tscn",
        "launcher": "run_grin_basic_visual_minimal_offaxis_observe",
        "renderer": {},
        "isMetric": False,
    },
    {
        "id": "grin_stronger_offaxis_observe",
        "label": "GRIN stronger",
        "baselineId": "grin_stronger_offaxis",
        "scene": "res://test-grin-basic-visual-offaxis-observe.tscn",
        "launcher": "run_grin_basic_visual_offaxis_observe",
        "renderer": {},
        "isMetric": False,
    },
    {
        "id": "metric_minimal_offaxis_observe",
        "label": "Metric minimal",
        "baselineId": "metric_minimal_offaxis",
        "scene": "res://test-metric-basic-visual-minimal-offaxis-observe.tscn",
        "launcher": "run_metric_basic_visual_minimal_offaxis_observe",
        "renderer": {},
        "isMetric": True,
    },
    {
        "id": "metric_stronger_offaxis_observe",
        "label": "Metric stronger",
        "baselineId": "metric_stronger_offaxis",
        "scene": "res://test-metric-basic-visual-offaxis-observe.tscn",
        "launcher": "run_metric_basic_visual_offaxis_observe",
        "renderer": {},
        "isMetric": True,
    },
    {
        "id": "metric_stronger_offaxis_observe_gain10",
        "label": "Metric stronger gain10",
        "baselineId": "metric_stronger_offaxis_gain10",
        "scene": "res://test-metric-basic-visual-offaxis-observe.tscn",
        "launcher": "run_metric_basic_visual_offaxis_observe",
        "renderer": {"metric_gain": 10.0},
        "isMetric": True,
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


def parse_zero_reason_map(metric_diag: dict | None) -> dict:
    result = {}
    if not metric_diag:
        return result
    zero_reasons = metric_diag.get("zeroReasons")
    if not zero_reasons or zero_reasons == "none":
        return result
    for token in str(zero_reasons).split(","):
        token = token.strip()
        if "=" not in token:
            continue
        key, value = token.split("=", 1)
        parsed = scalar(value)
        if isinstance(parsed, (int, float)):
            result[key] = parsed
    return result


def parse_log(text: str) -> dict:
    parsed = {
        "capture": None,
        "renderer": None,
        "metricDiag": None,
        "renderHealth": None,
        "launchAudit": None,
        "captureArtifacts": None,
        "captureFailure": None,
    }
    for line in text.splitlines():
        for key, prefix in {
            "capture": "[GrinBasicVisual][Capture]",
            "renderer": "[GrinBasicVisual][Renderer]",
            "metricDiag": "[GrinBasicVisual][MetricDiag]",
            "renderHealth": "[RenderHealth]",
            "launchAudit": "[LaunchAudit]",
            "captureArtifacts": "[GrinBasicVisual][CaptureArtifacts]",
        }.items():
            data = parse_kv(line, prefix)
            if data:
                parsed[key] = data
        fail = FAIL_RE.search(line)
        if fail:
            parsed["captureFailure"] = fail.group(0).strip()
    return parsed


def build_args(case_data: dict, screenshot_path: Path) -> list[str]:
    min_rows = os.environ.get("OFFAXIS_OBSERVE_MIN_PROCESSED_ROWS", str(DEFAULT_MIN_PROCESSED_ROWS))
    args = [
        f"--grin-basic-capture={screenshot_path.as_posix()}",
        f"--grin-basic-settle-frames={os.environ.get('OFFAXIS_OBSERVE_SETTLE_FRAMES', str(DEFAULT_SETTLE_FRAMES))}",
        f"--grin-basic-min-rh-step={os.environ.get('OFFAXIS_OBSERVE_MIN_RH_STEP', str(DEFAULT_MIN_RENDER_HEALTH_STEP))}",
        f"--grin-basic-min-processed-rows={min_rows}",
        f"--grin-basic-capture-film-opacity={os.environ.get('OFFAXIS_OBSERVE_CAPTURE_FILM_OPACITY', str(DEFAULT_CAPTURE_FILM_OPACITY))}",
        f"--grin-basic-compare-grid={os.environ.get('OFFAXIS_OBSERVE_COMPARE_GRID', '1')}",
        f"--grin-basic-compare-crosshair={os.environ.get('OFFAXIS_OBSERVE_COMPARE_CROSSHAIR', '1')}",
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


def run_case(godot_exe: str, case_data: dict, screenshot_dir: Path, log_dir: Path) -> dict:
    screenshot_path = screenshot_dir / f"{case_data['id']}.png"
    log_path = log_dir / f"{case_data['id']}.log"
    cmd = [godot_exe, "--path", ".", "--scene", case_data["scene"], "--", *build_args(case_data, screenshot_path)]
    env = os.environ.copy()
    env["XPRIMERAY_REQUESTED_LAUNCHER"] = case_data["launcher"]
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
    combined = completed.stdout + ("\n" + completed.stderr if completed.stderr else "")
    log_path.write_text(combined, encoding="utf-8")
    parsed = parse_log(combined)
    capture = parsed.get("capture") or {}
    capture_artifacts = parsed.get("captureArtifacts") or {}
    zero_reason_map = parse_zero_reason_map(parsed.get("metricDiag"))
    capture_stats = {
        "tracedPixels": capture.get("tracedPixels"),
        "sourceHits": capture.get("sourceHits"),
        "backgroundHits": capture.get("backgroundHits"),
        "absorbedHits": capture.get("absorbedHits"),
        "missHits": capture.get("missHits"),
        "readyFrames": capture.get("readyFrames"),
        "rhStep": capture.get("rhStep"),
        "processedRows": capture.get("processedRows"),
    }
    expected_full_rows = int(os.environ.get("OFFAXIS_OBSERVE_EXPECTED_FULL_ROWS", str(DEFAULT_FULL_COVERAGE_ROWS)))
    processed_rows = to_int(capture.get("processedRows"))
    film_rows_rendered = to_int(capture_artifacts.get("filmRowsRendered"))
    film_height = to_int(capture_artifacts.get("filmHeight"))
    unrendered_bounds = str(capture_artifacts.get("unrenderedImageBounds") or "")
    failure = None
    if completed.returncode != 0:
        failure = f"godot_exit_{completed.returncode}"
    elif parsed.get("captureFailure"):
        failure = parsed["captureFailure"]
    elif not screenshot_path.exists():
        failure = "missing_screenshot"
    elif not capture:
        failure = "missing_capture_log"
    elif processed_rows is None or processed_rows < expected_full_rows:
        failure = f"incomplete_processed_rows_{processed_rows}_of_{expected_full_rows}"
    elif film_rows_rendered is None or film_height is None or film_rows_rendered < film_height:
        failure = f"incomplete_film_rows_{film_rows_rendered}_of_{film_height}"
    elif not unrendered_bounds.endswith(",0"):
        failure = f"unrendered_bounds_present_{unrendered_bounds}"
    return {
        "caseId": case_data["id"],
        "label": case_data["label"],
        "baselineId": case_data["baselineId"],
        "scene": case_data["scene"],
        "launcher": case_data["launcher"],
        "requestedRendererOverrides": case_data.get("renderer", {}),
        "rendererConfig": parsed.get("renderer"),
        "metricDiagnostics": parsed.get("metricDiag"),
        "zeroReasonMap": zero_reason_map,
        "parallelRaw": zero_reason_map.get("parallel_raw"),
        "renderHealth": parsed.get("renderHealth"),
        "captureArtifacts": capture_artifacts,
        "captureStats": capture_stats,
        "fullCoverage": {
            "expectedRows": expected_full_rows,
            "processedRows": processed_rows,
            "filmRowsRendered": film_rows_rendered,
            "filmHeight": film_height,
            "unrenderedImageBounds": unrendered_bounds,
            "complete": failure is None,
        },
        "launchAudit": parsed.get("launchAudit"),
        "screenshotPath": str(screenshot_path),
        "logPath": str(log_path),
        "status": "failed" if failure else "ok",
        "failureReason": failure,
        "exitCode": completed.returncode,
        "contactSheetLabel": build_contact_label(case_data["label"], capture_stats, parsed.get("metricDiag"), zero_reason_map),
    }


def build_contact_label(label: str, capture_stats: dict, metric_diag: dict | None, zero_reason_map: dict) -> str:
    source_hits = capture_stats.get("sourceHits")
    background_hits = capture_stats.get("backgroundHits")
    rh_step = capture_stats.get("rhStep")
    processed_rows = capture_stats.get("processedRows")
    if metric_diag:
        return (
            f"{label}\n"
            f"src={source_hits} bg={background_hits}\n"
            f"p_raw={zero_reason_map.get('parallel_raw', 'na')} meanTurn={metric_diag.get('meanTurn', 'na')}\n"
            f"rh={rh_step} rows={processed_rows}"
        )
    return (
        f"{label}\n"
        f"src={source_hits} bg={background_hits}\n"
        f"rh={rh_step} rows={processed_rows}"
    )


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


def load_baseline_summary() -> dict:
    if not BASELINE_SUMMARY_PATH.exists():
        raise FileNotFoundError(f"Missing baseline summary: {BASELINE_SUMMARY_PATH}")
    payload = json.loads(BASELINE_SUMMARY_PATH.read_text(encoding="utf-8"))
    cases = {case["id"]: case for case in payload.get("cases", []) if case.get("status") == "ok"}
    for case_data in CASES:
        if case_data["baselineId"] not in cases:
            raise RuntimeError(f"Baseline summary missing {case_data['baselineId']}")
    return {"payload": payload, "cases": cases}


def to_int(value):
    if value is None:
        return None
    if isinstance(value, int):
        return value
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def to_float(value):
    if value is None:
        return None
    if isinstance(value, (int, float)):
        return float(value)
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def baseline_capture_value(case_data: dict, key: str):
    capture = case_data.get("capture") or case_data.get("captureStats") or {}
    return to_int(capture.get(key))


def baseline_metric_value(case_data: dict, key: str):
    metric_diag = case_data.get("metricDiag") or case_data.get("metricDiagnostics") or {}
    value = metric_diag.get(key)
    if key == "meanTurn":
        return to_float(value)
    return to_int(value)


def build_comparison_cases(observe_cases: list[dict], baseline_cases: dict) -> list[dict]:
    comparison_cases = []
    for observe_case in observe_cases:
        baseline = baseline_cases[observe_case["baselineId"]]
        baseline_capture = baseline.get("capture") or {}
        baseline_metric = baseline.get("metricDiag") or {}
        baseline_parallel_raw = parse_baseline_parallel_raw(baseline_metric.get("zeroReasons"))
        comparison_cases.append(
            {
                "caseId": f"{observe_case['baselineId']}_baseline",
                "status": "ok",
                "screenshotPath": baseline["screenshotPath"],
                "contactSheetLabel": (
                    f"{observe_case['label']}\nold off-axis\n"
                    f"src={baseline_capture.get('sourceHits', 'na')} bg={baseline_capture.get('backgroundHits', 'na')}\n"
                    f"p_raw={baseline_parallel_raw if baseline_parallel_raw is not None else 'na'} "
                    f"meanTurn={baseline_metric.get('meanTurn', 'na')}"
                ),
            }
        )
        comparison_cases.append(
            {
                "caseId": f"{observe_case['caseId']}_observe",
                "status": "ok",
                "screenshotPath": observe_case["screenshotPath"],
                "contactSheetLabel": (
                    f"{observe_case['label']}\nobserve ladder\n"
                    f"src={observe_case['captureStats'].get('sourceHits', 'na')} bg={observe_case['captureStats'].get('backgroundHits', 'na')}\n"
                    f"p_raw={observe_case.get('parallelRaw', 'na')} "
                    f"meanTurn={(observe_case.get('metricDiagnostics') or {}).get('meanTurn', 'na')}"
                ),
            }
        )
    return comparison_cases


def parse_baseline_parallel_raw(zero_reasons):
    if not zero_reasons:
        return None
    for token in str(zero_reasons).split(","):
        token = token.strip()
        if not token.startswith("parallel_raw="):
            continue
        return to_int(token.split("=", 1)[1])
    return None


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def write_report(report_path: Path, summary: dict, baseline_cases: dict, observe_cases: list[dict]) -> None:
    metric_rows = []
    metric_nonzero_hits = 0
    metric_easier_count = 0
    metric_background_gain_total = 0
    metric_source_gain_total = 0
    for observe_case in observe_cases:
        if not observe_case.get("metricDiagnostics"):
            continue
        baseline = baseline_cases[observe_case["baselineId"]]
        old_source = baseline_capture_value(baseline, "sourceHits") or 0
        old_background = baseline_capture_value(baseline, "backgroundHits") or 0
        new_source = to_int(observe_case["captureStats"].get("sourceHits")) or 0
        new_background = to_int(observe_case["captureStats"].get("backgroundHits")) or 0
        old_parallel_raw = parse_baseline_parallel_raw((baseline.get("metricDiag") or {}).get("zeroReasons"))
        new_parallel_raw = to_int(observe_case.get("parallelRaw"))
        old_mean_turn = baseline_metric_value(baseline, "meanTurn")
        new_mean_turn = to_float((observe_case.get("metricDiagnostics") or {}).get("meanTurn"))
        nonzero_hits = new_source > 0 or new_background > 0
        easier = nonzero_hits or ((old_parallel_raw or 0) > (new_parallel_raw or 0) and (new_mean_turn or 0.0) >= (old_mean_turn or 0.0))
        if nonzero_hits:
            metric_nonzero_hits += 1
        if easier:
            metric_easier_count += 1
        metric_source_gain_total += new_source - old_source
        metric_background_gain_total += new_background - old_background
        metric_rows.append(
            f"- `{observe_case['label']}`: sourceHits `{old_source}` -> `{new_source}`, "
            f"backgroundHits `{old_background}` -> `{new_background}`, "
            f"parallel_raw `{old_parallel_raw if old_parallel_raw is not None else 'na'}` -> `{new_parallel_raw if new_parallel_raw is not None else 'na'}`, "
            f"meanTurn `{old_mean_turn if old_mean_turn is not None else 'na'}` -> `{new_mean_turn if new_mean_turn is not None else 'na'}`."
        )

    if metric_background_gain_total > metric_source_gain_total:
        best_geometry_change = "expanded right-biased background target"
    elif metric_source_gain_total > 0:
        best_geometry_change = "added right-biased diagnostic dots"
    else:
        best_geometry_change = "no single change isolated; the combined observe geometry did most of the work"

    report_lines = [
        f"# Basic Visual Off-axis Observe Report ({summary['runDate']})",
        "",
        "## Capture Fix",
        "",
        "- Root blocker: `LaunchAudit` treated the new `*-observe.tscn` harnesses as launcher/scene mismatches, so `GrinBasicVisualController._Ready()` returned before `[GrinBasicVisual][CaptureConfig]` and the observe runs never armed capture.",
        "- Secondary gate fix retained: capture readiness now latches best-observed render-health step and processed-row values instead of relying on the transient current `FilmRowCursor`.",
        "",
        "## Metric Observability",
        "",
        f"- Metric cases with nonzero source/background hits at capture: `{metric_nonzero_hits}` / `3`.",
        f"- Metric cases judged easier to read than the old off-axis ladder: `{metric_easier_count}` / `3`.",
        f"- Most likely geometry change driving the improvement: `{best_geometry_change}`.",
        "",
        "## Case Readout",
        "",
        *metric_rows,
        "",
        "## Artifacts",
        "",
        f"- Observe contact sheet: `{summary['artifacts']['observeContactSheet']}`",
        f"- Old off-axis vs observe sheet: `{summary['artifacts']['comparisonSheet']}`",
        f"- Summary JSON: `{LOG_ROOT / 'summary.json'}`",
    ]
    report_path.write_text("\n".join(report_lines) + "\n", encoding="utf-8")


def main() -> int:
    godot_exe = require_godot_exe()
    baseline = load_baseline_summary()
    run_date = date.today().isoformat()
    timestamp = datetime.now().strftime("%Y-%m-%dT%H:%M:%S")
    screenshot_dir = SCREENSHOT_ROOT / run_date
    screenshot_dir.mkdir(parents=True, exist_ok=True)
    LOG_ROOT.mkdir(parents=True, exist_ok=True)

    summary = {
        "generatedAt": timestamp,
        "runDate": run_date,
        "screenshotDir": str(screenshot_dir),
        "baselineSummaryPath": str(BASELINE_SUMMARY_PATH),
        "cases": [],
        "artifacts": {},
    }

    for case_data in CASES:
        print(f"RUN {case_data['id']}", flush=True)
        result = run_case(godot_exe, case_data, screenshot_dir, LOG_ROOT)
        summary["cases"].append(result)
        write_json(LOG_ROOT / "summary.json", summary)
        print(json.dumps(result, indent=2), flush=True)
        if result["status"] != "ok":
            print(f"STOP {case_data['id']} reason={result['failureReason']}", flush=True)
            return 1

    observe_cases = [case for case in summary["cases"] if case.get("status") == "ok"]
    comparison_cases = build_comparison_cases(observe_cases, baseline["cases"])
    observe_contact_summary = {"runDate": run_date, "cases": observe_cases}
    comparison_summary = {"runDate": run_date, "cases": comparison_cases}
    write_json(LOG_ROOT / "contact_sheet_summary.json", observe_contact_summary)
    write_json(LOG_ROOT / "comparison_sheet_summary.json", comparison_summary)

    observe_contact_sheet = screenshot_dir / "observe_contact_sheet.png"
    comparison_sheet = screenshot_dir / "old_vs_observe_comparison_sheet.png"
    build_contact_sheet(LOG_ROOT / "contact_sheet_summary.json", observe_contact_sheet, "Off-axis Observe Ladder", columns=3)
    build_contact_sheet(LOG_ROOT / "comparison_sheet_summary.json", comparison_sheet, "Old Off-axis vs Observe Ladder", columns=2)

    summary["artifacts"]["observeContactSheet"] = str(observe_contact_sheet)
    summary["artifacts"]["comparisonSheet"] = str(comparison_sheet)
    summary["artifacts"]["contactSheetSummary"] = str(LOG_ROOT / "contact_sheet_summary.json")
    summary["artifacts"]["comparisonSheetSummary"] = str(LOG_ROOT / "comparison_sheet_summary.json")

    report_path = LOG_ROOT / f"basic_visual_offaxis_observe_report_{run_date}.md"
    write_report(report_path, summary, baseline["cases"], observe_cases)
    summary["artifacts"]["reportPath"] = str(report_path)

    write_json(LOG_ROOT / "summary.json", summary)
    print(f"WROTE {LOG_ROOT / 'summary.json'}", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
