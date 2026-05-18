import argparse
import os
import re
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_GODOT_EXE = (
    r"C:\Users\wmbro\Downloads\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64"
    r"\Godot_v4.5.1-stable_mono_win64_console.exe"
)

FULL_MIN_ROWS = 360
QUICK_MIN_ROWS = 180
GODOT_TIMEOUT_SECONDS = 1800

FAIL_RE = re.compile(r"\[GrinBasicVisual\]\[Capture\]\[FAIL\].*")

CASES_FULL = [
    {
        "id": "hermetic_straight",
        "label": "hermetic straight",
        "scene": "res://test-straight-hermetic-observatory-v0-pre.tscn",
        "min_rows": FULL_MIN_ROWS,
    },
    {
        "id": "hermetic_grin",
        "label": "hermetic GRIN",
        "scene": "res://test-grin-hermetic-observatory-v0-pre.tscn",
        "min_rows": FULL_MIN_ROWS,
    },
]

CASES_QUICK = [
    {
        "id": "hermetic_straight_quick",
        "label": "hermetic straight (quick 320×180)",
        "scene": "res://test-straight-hermetic-observatory-quick.tscn",
        "min_rows": QUICK_MIN_ROWS,
    },
    {
        "id": "hermetic_grin_quick",
        "label": "hermetic GRIN (quick 320×180)",
        "scene": "res://test-grin-hermetic-observatory-quick.tscn",
        "min_rows": QUICK_MIN_ROWS,
    },
]

CASES_TILE_FULL = [
    {
        "id": "hermetic_straight_tile",
        "label": "hermetic straight (tile)",
        "scene": "res://test-straight-hermetic-observatory-tile-v0.tscn",
        "min_rows": FULL_MIN_ROWS,
    },
    {
        "id": "hermetic_grin_tile",
        "label": "hermetic GRIN (tile)",
        "scene": "res://test-grin-hermetic-observatory-tile-v0.tscn",
        "min_rows": FULL_MIN_ROWS,
    },
]

CASES_TILE_QUICK = [
    {
        "id": "hermetic_straight_tile_quick",
        "label": "hermetic straight tile (quick 320×180)",
        "scene": "res://test-straight-hermetic-observatory-tile-quick.tscn",
        "min_rows": QUICK_MIN_ROWS,
    },
    {
        "id": "hermetic_grin_tile_quick",
        "label": "hermetic GRIN tile (quick 320×180)",
        "scene": "res://test-grin-hermetic-observatory-tile-quick.tscn",
        "min_rows": QUICK_MIN_ROWS,
    },
]


def require_godot_exe(override):
    candidate = override or os.environ.get("GODOT_EXE", DEFAULT_GODOT_EXE)
    if not Path(candidate).exists():
        raise FileNotFoundError(
            f"GODOT_EXE not found at '{candidate}'.\n"
            f"  Set GODOT_EXE env var or pass --godot-exe <path>.\n"
            f"  On this system, try: GODOT_EXE=./scripts/godot_local.sh"
        )
    return candidate


def scalar(token):
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


def parse_kv(line, prefix):
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


def to_int(value):
    if value is None:
        return None
    if isinstance(value, int):
        return value
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def parse_log(text):
    parsed = {
        "capture": None,
        "captureArtifacts": None,
        "captureConfig": None,
        "launchAudit": None,
        "coverage": None,
        "tileScheduler": None,
        "captureFailure": None,
    }
    for line in text.splitlines():
        for key, prefix in {
            "capture": "[GrinBasicVisual][Capture] ",
            "captureArtifacts": "[GrinBasicVisual][CaptureArtifacts] ",
            "captureConfig": "[GrinBasicVisual][CaptureConfig] ",
            "launchAudit": "[LaunchAudit] ",
            "coverage": "[GrinBasicVisual][Coverage] ",
            "tileScheduler": "[TileScheduler] ",
        }.items():
            data = parse_kv(line, prefix)
            if data:
                parsed[key] = data
        fail = FAIL_RE.search(line)
        if fail:
            parsed["captureFailure"] = fail.group(0).strip()
    return parsed


def check_hermetic(parsed):
    """Returns list of failure descriptions (empty = pass)."""
    failures = []
    cap = parsed.get("capture") or {}
    art = parsed.get("captureArtifacts") or {}
    cov = parsed.get("coverage") or {}

    if not cap and not art:
        # Capture never ran — check for clues in launchAudit
        audit = parsed.get("launchAudit") or {}
        scene_match = audit.get("scene_match")
        fixture_match = audit.get("fixture_match")
        status = audit.get("status")
        if status == "warn" or fixture_match == 0 or scene_match == 0:
            failures.append(
                f"LaunchAudit rejected scene (scene_match={scene_match} fixture_match={fixture_match} status={status}); "
                f"controller returned early from _Ready() — capture was never activated"
            )
        else:
            failures.append("capture log lines missing — Godot may have exited before rendering started")
        return failures

    miss = to_int(cap.get("missHits"))
    if miss is None:
        failures.append("missHits not found in [Capture] log line")
    elif miss != 0:
        failures.append(f"missHits={miss} (expected 0 — rays escaped the sealed chamber)")

    traversal_rows = to_int(art.get("traversalRowsCompleted"))
    film_rows = to_int(art.get("filmRowsRendered"))
    height = to_int(art.get("filmHeight"))
    if traversal_rows is not None:
        # Use traversal completion (persists across pass resets) as the authoritative metric
        if height is not None and traversal_rows < height:
            failures.append(f"traversalRowsCompleted={traversal_rows} < filmHeight={height} (incomplete traversal)")
    else:
        # traversalRowsCompleted not present (older build) — fall back to filmRowsRendered
        if film_rows is None or height is None:
            if film_rows is None:
                failures.append("filmRowsRendered not found in [CaptureArtifacts] log line")
        elif film_rows < height:
            failures.append(f"filmRowsRendered={film_rows} < filmHeight={height} (incomplete render)")

    traced = to_int(cap.get("tracedPixels"))
    if traced is not None and traced == 0:
        failures.append("tracedPixels=0 (no pixels rendered)")

    # Coverage supplements: check for escapes not captured in FixtureDebugStats
    escaped = to_int(cov.get("escapedNoHitPixels"))
    budget_exhausted = to_int(cov.get("budgetExhaustedPixels"))
    hermetic_ok = cov.get("hermeticRuleSatisfied")
    if hermetic_ok == 0:
        detail = []
        if escaped and escaped > 0:
            detail.append(f"escapedNoHitPixels={escaped}")
        if budget_exhausted and budget_exhausted > 0:
            detail.append(f"budgetExhaustedPixels={budget_exhausted}")
        failures.append(f"hermeticRuleSatisfied=0 ({', '.join(detail) if detail else 'see coverage log'})")

    return failures


def build_telemetry_block(result, log_text):
    lines = []
    lines.append(f"  expected screenshot: {result['screenshotPath']}")
    lines.append(f"  screenshot exists:   {Path(result['screenshotPath']).exists()}")
    lines.append(f"  log file:            {result['logPath']}")
    lines.append(f"  output dir exists:   {Path(result['screenshotPath']).parent.exists()}")
    lines.append(f"  cwd (project root):  {ROOT}")
    lines.append(f"  godot exit code:     {result['exitCode']}")

    parsed = result.get("_parsed") or {}
    audit = parsed.get("launchAudit") or {}
    cfg = parsed.get("captureConfig") or {}
    if audit:
        lines.append(f"  [LaunchAudit]:  scene_match={audit.get('scene_match')} fixture_match={audit.get('fixture_match')} "
                     f"status={audit.get('status')} actual_fixture={audit.get('actual_fixture')}")
    else:
        lines.append("  [LaunchAudit]:  not found in log (controller may not have reached startup)")
    if cfg:
        lines.append(f"  [CaptureConfig]: analysisPath={cfg.get('analysisPath')} "
                     f"minRows={cfg.get('minRows')} exitAfterCapture={cfg.get('exitAfterCapture')}")
    else:
        lines.append("  [CaptureConfig]: not found in log (capture was never configured)")

    lines.append("")
    lines.append("  --- last 50 Godot log lines ---")
    log_tail = (log_text or "").splitlines()[-50:]
    for ln in log_tail:
        lines.append(f"  {ln}")
    return "\n".join(lines)


def run_case(godot_exe, case_data, screenshot_dir, log_dir):
    min_rows = case_data["min_rows"]
    screenshot_path = screenshot_dir / f"{case_data['id']}.png"
    log_path = log_dir / f"{case_data['id']}.log"
    args = [
        f"--grin-basic-capture={screenshot_path.as_posix()}",
        f"--grin-basic-min-processed-rows={min_rows}",
        "--grin-basic-exit-after-capture=1",
        "--grin-basic-settle-frames=6",
    ]
    cmd = [godot_exe, "--path", ".", "--scene", case_data["scene"], "--", *args]
    combined = ""
    exit_code = -1
    timed_out = False

    try:
        completed = subprocess.run(
            cmd,
            cwd=ROOT,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=GODOT_TIMEOUT_SECONDS,
            check=False,
        )
        combined = completed.stdout + ("\n" + completed.stderr if completed.stderr else "")
        exit_code = completed.returncode
    except subprocess.TimeoutExpired as exc:
        timed_out = True
        # exc.stdout/stderr are bytes when the process is killed mid-run, even with text=True
        def _decode(b):
            if b is None:
                return ""
            if isinstance(b, bytes):
                return b.decode("utf-8", errors="replace")
            return b
        combined = _decode(exc.stdout) + ("\n" + _decode(exc.stderr) if exc.stderr else "")
        exit_code = -1

    log_path.write_text(combined, encoding="utf-8")
    parsed = parse_log(combined)

    capture_failure = None
    if timed_out:
        capture_failure = f"python_timeout_{GODOT_TIMEOUT_SECONDS}s"
    elif exit_code != 0:
        capture_failure = f"godot_exit_{exit_code}"
    elif parsed.get("captureFailure"):
        capture_failure = parsed["captureFailure"]
    elif not screenshot_path.exists():
        capture_failure = "missing_screenshot"

    hermetic_failures = check_hermetic(parsed) if capture_failure is None else []

    art = parsed.get("captureArtifacts") or {}
    cap = parsed.get("capture") or {}
    cov = parsed.get("coverage") or {}
    tile = parsed.get("tileScheduler") or {}

    status = "PASS" if (capture_failure is None and not hermetic_failures) else "FAIL"

    return {
        "caseId": case_data["id"],
        "label": case_data["label"],
        "scene": case_data["scene"],
        "minRows": min_rows,
        "status": status,
        "captureFailure": capture_failure,
        "hermeticFailures": hermetic_failures,
        "missHits": to_int(cap.get("missHits")),
        "tileMode": tile.get("mode"),
        "traversalRowsCompleted": to_int(art.get("traversalRowsCompleted")),
        "filmRowsRendered": to_int(art.get("filmRowsRendered")),
        "filmHeight": to_int(art.get("filmHeight")),
        "tracedPixels": to_int(cap.get("tracedPixels")),
        "backgroundHits": to_int(cap.get("backgroundHits")),
        "sourceHits": to_int(cap.get("sourceHits")),
        "hermeticRuleSatisfied": cov.get("hermeticRuleSatisfied"),
        "escapedNoHitPixels": to_int(cov.get("escapedNoHitPixels")),
        "budgetExhaustedPixels": to_int(cov.get("budgetExhaustedPixels")),
        "totalPixels": to_int(cov.get("totalPixels")),
        "classifiedPixels": to_int(cov.get("classifiedPixels")),
        "screenshotPath": str(screenshot_path),
        "logPath": str(log_path),
        "exitCode": exit_code,
        "timedOut": timed_out,
        "_parsed": parsed,
        "_logText": combined,
    }


def write_report(report_path, results, mode_label):
    all_pass = all(r["status"] == "PASS" for r in results)
    now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

    def fmt(v):
        return str(v) if v is not None else "—"

    lines = [
        "# Hermetic Observatory Validation Report",
        "",
        f"- Generated: `{now}`",
        f"- Mode: `{mode_label}`",
        f"- Overall: `{'PASS' if all_pass else 'FAIL'}`",
        "",
        "## Results",
        "",
        "| Case | Status | missHits | traversalRowsCompleted | filmHeight | filmRowsRendered | tracedPixels | hermeticRuleSat | backgroundHits |",
        "|------|--------|----------|------------------------|------------|-----------------|--------------|-----------------|----------------|",
    ]
    for r in results:
        status_cell = f"**{r['status']}**" if r["status"] == "FAIL" else r["status"]
        lines.append(
            f"| {r['label']} | {status_cell} | {fmt(r['missHits'])} | {fmt(r['traversalRowsCompleted'])} "
            f"| {fmt(r['filmHeight'])} | {fmt(r['filmRowsRendered'])} | {fmt(r['tracedPixels'])} "
            f"| {fmt(r['hermeticRuleSatisfied'])} | {fmt(r['backgroundHits'])} |"
        )
    lines.append("")

    for r in results:
        if r["captureFailure"] or r["hermeticFailures"] or r["timedOut"]:
            lines.append(f"### {r['label']} — failure detail")
            lines.append("")
            if r["timedOut"]:
                lines.append(f"- Process timed out after {GODOT_TIMEOUT_SECONDS}s without completing capture")
            if r["captureFailure"]:
                lines.append(f"- Capture error: `{r['captureFailure']}`")
            for f in r["hermeticFailures"]:
                lines.append(f"- {f}")
            lines.append(f"- Expected screenshot: `{r['screenshotPath']}`")
            lines.append(f"- Log: `{r['logPath']}`")
            lines.append("")

    lines.append("## Coverage (if available)")
    lines.append("")
    lines.append("| Case | totalPixels | classifiedPixels | escapedNoHitPixels | budgetExhaustedPixels |")
    lines.append("|------|-------------|-----------------|--------------------|-----------------------|")
    for r in results:
        lines.append(
            f"| {r['label']} | {fmt(r['totalPixels'])} | {fmt(r['classifiedPixels'])} "
            f"| {fmt(r['escapedNoHitPixels'])} | {fmt(r['budgetExhaustedPixels'])} |"
        )
    lines.append("")

    lines.append("## Artifacts")
    lines.append("")
    for r in results:
        lines.append(f"- `{r['screenshotPath']}`")
        lines.append(f"- `{r['logPath']}`")
    lines.append("")

    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text("\n".join(lines), encoding="utf-8")
    print(f"[hermetic-observe] report written to {report_path}")


def main():
    parser = argparse.ArgumentParser(description="Hermetic observatory full-pixel validation")
    parser.add_argument("--godot-exe", default=None, help="Path to Godot executable (or wrapper script). Overrides GODOT_EXE env var.")
    parser.add_argument("--output-dir", default=str(ROOT / "output" / "v0.0-pre"), help="Output directory for screenshots and report")
    parser.add_argument("--quick", action="store_true", help="Use quick smoke scenes (320x180 effective, min_rows=180)")
    parser.add_argument("--tile", action="store_true", help="Use tile-mode traversal scenes instead of row-mode scenes")
    args = parser.parse_args()

    godot_exe = require_godot_exe(args.godot_exe)
    if args.tile:
        cases = CASES_TILE_QUICK if args.quick else CASES_TILE_FULL
        mode_label = ("tile quick (320×180)" if args.quick else "tile full (640×360)")
    else:
        cases = CASES_QUICK if args.quick else CASES_FULL
        mode_label = "quick (320×180)" if args.quick else "full (640×360)"

    output_dir = Path(args.output_dir)
    screenshot_dir = output_dir
    log_dir = output_dir / "logs" / "hermetic_observatory"
    report_path = output_dir / "HERMETIC_OBSERVATORY_VALIDATE.md"

    screenshot_dir.mkdir(parents=True, exist_ok=True)
    log_dir.mkdir(parents=True, exist_ok=True)

    print(f"[hermetic-observe] mode={mode_label}  godot={godot_exe}")

    results = []
    for case_data in cases:
        print(f"[hermetic-observe] running {case_data['label']} (min_rows={case_data['min_rows']}) ...")
        result = run_case(godot_exe, case_data, screenshot_dir, log_dir)

        suffix = ""
        if result["captureFailure"]:
            suffix = f" — capture error: {result['captureFailure']}"
        elif result["hermeticFailures"]:
            suffix = f" — hermetic failures: {result['hermeticFailures']}"
        print(f"[hermetic-observe] {case_data['label']}: {result['status']}{suffix}")

        if result["status"] == "FAIL":
            print(build_telemetry_block(result, result["_logText"]))

        results.append(result)

    write_report(report_path, results, mode_label)

    all_pass = all(r["status"] == "PASS" for r in results)
    if all_pass:
        print("[hermetic-observe] ALL PASS")
    else:
        print("[hermetic-observe] FAIL — see report for details")
    return 0 if all_pass else 1


if __name__ == "__main__":
    sys.exit(main())
