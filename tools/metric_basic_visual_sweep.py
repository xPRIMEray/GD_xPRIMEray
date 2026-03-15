import argparse
import json
import os
import re
import subprocess
import sys
from collections import defaultdict
from datetime import date, datetime
from pathlib import Path

from PIL import Image, ImageChops, ImageFilter, ImageOps, ImageStat


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_GODOT_EXE = (
    r"C:\Users\wmbro\Downloads\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64"
    r"\Godot_v4.5.1-stable_mono_win64_console.exe"
)
SCREENSHOT_ROOT = ROOT / "screenshots" / "metric_basic_visual_sweep"
LOG_ROOT = ROOT / "logs" / "metric_basic_visual_sweep"
GRIN_SUMMARY_PATH = ROOT / "logs" / "grin_basic_visual_sweep" / "summary.json"
CONTACT_SHEET_SCRIPT = ROOT / "tools" / "build_visual_contact_sheet.py"
RESOLVED_RE = re.compile(
    r"\[GrinBasicVisual\].*?rOuter=(?P<router>-?\d+(?:\.\d+)?) "
    r"amp=(?P<amp>-?\d+(?:\.\d+)?) gamma=(?P<gamma>-?\d+(?:\.\d+)?)"
)
FAIL_RE = re.compile(r"\[GrinBasicVisual\]\[Capture\]\[FAIL\].*")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run the Metric basic-visual diagnostic sweep.")
    parser.add_argument("--grin-summary", default=str(GRIN_SUMMARY_PATH))
    parser.add_argument("--refresh-grin-baselines", action="store_true")
    parser.add_argument("--max-cases", type=int, default=0)
    return parser.parse_args()


def case(case_id: str, rung: str, scene: str, launcher: str, family: str, value, renderer=None) -> dict:
    return {
        "id": case_id,
        "rung": rung,
        "scene": scene,
        "launcher": launcher,
        "family": family,
        "familyValue": value,
        "renderer": renderer or {},
    }


CASES = [
    case("straight_reference", "straight", "res://test-grin-basic-visual-straight.tscn", "run_grin_basic_visual_straight", "control", "straight"),
    case("minimal_baseline", "minimal", "res://test-metric-basic-visual-minimal.tscn", "run_metric_basic_visual_minimal", "baseline", 1.0),
    case("minimal_step_0p5x", "minimal", "res://test-metric-basic-visual-minimal.tscn", "run_metric_basic_visual_minimal", "step", 0.5, {"step_scale": 0.5}),
    case("minimal_step_0p25x", "minimal", "res://test-metric-basic-visual-minimal.tscn", "run_metric_basic_visual_minimal", "step", 0.25, {"step_scale": 0.25}),
    case("minimal_gain_5x", "minimal", "res://test-metric-basic-visual-minimal.tscn", "run_metric_basic_visual_minimal", "gain", 5.0, {"metric_gain": 5.0}),
    case("minimal_gain_10x", "minimal", "res://test-metric-basic-visual-minimal.tscn", "run_metric_basic_visual_minimal", "gain", 10.0, {"metric_gain": 10.0}),
    case("minimal_gain_20x", "minimal", "res://test-metric-basic-visual-minimal.tscn", "run_metric_basic_visual_minimal", "gain", 20.0, {"metric_gain": 20.0}),
    case("minimal_depth_2x", "minimal", "res://test-metric-basic-visual-minimal.tscn", "run_metric_basic_visual_minimal", "depth", 2.0, {"steps_per_ray": 1000}),
    case("minimal_depth_4x", "minimal", "res://test-metric-basic-visual-minimal.tscn", "run_metric_basic_visual_minimal", "depth", 4.0, {"steps_per_ray": 2000}),
    case("stronger_baseline", "stronger", "res://test-metric-basic-visual.tscn", "run_metric_basic_visual", "baseline", 1.0),
    case("stronger_step_0p5x", "stronger", "res://test-metric-basic-visual.tscn", "run_metric_basic_visual", "step", 0.5, {"step_scale": 0.5}),
    case("stronger_step_0p25x", "stronger", "res://test-metric-basic-visual.tscn", "run_metric_basic_visual", "step", 0.25, {"step_scale": 0.25}),
    case("stronger_gain_5x", "stronger", "res://test-metric-basic-visual.tscn", "run_metric_basic_visual", "gain", 5.0, {"metric_gain": 5.0}),
    case("stronger_gain_10x", "stronger", "res://test-metric-basic-visual.tscn", "run_metric_basic_visual", "gain", 10.0, {"metric_gain": 10.0}),
    case("stronger_gain_20x", "stronger", "res://test-metric-basic-visual.tscn", "run_metric_basic_visual", "gain", 20.0, {"metric_gain": 20.0}),
    case("stronger_depth_2x", "stronger", "res://test-metric-basic-visual.tscn", "run_metric_basic_visual", "depth", 2.0, {"steps_per_ray": 1000}),
    case("stronger_depth_4x", "stronger", "res://test-metric-basic-visual.tscn", "run_metric_basic_visual", "depth", 4.0, {"steps_per_ray": 2000}),
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
    parsed = {"resolved": None, "capture": None, "renderer": None, "metricDiag": None, "metricScalarMap": None, "renderHealth": None, "captureFailure": None}
    for line in text.splitlines():
        match = RESOLVED_RE.search(line)
        if match:
            parsed["resolved"] = {
                "rOuter": float(match.group("router")),
                "amp": float(match.group("amp")),
                "gamma": float(match.group("gamma")),
            }
        for key, prefix in {
            "capture": "[GrinBasicVisual][Capture]",
            "renderer": "[GrinBasicVisual][Renderer]",
            "metricDiag": "[GrinBasicVisual][MetricDiag]",
            "metricScalarMap": "[Transport][MetricScalarMap]",
            "renderHealth": "[RenderHealth]",
        }.items():
            data = parse_kv(line, prefix)
            if data:
                parsed[key] = data
        fail = FAIL_RE.search(line)
        if fail:
            parsed["captureFailure"] = fail.group(0).strip()
    return parsed


def build_args(case_data: dict, screenshot_path: Path) -> list[str]:
    compare_grid = os.environ.get("METRIC_BASIC_SWEEP_COMPARE_GRID", "1")
    compare_crosshair = os.environ.get("METRIC_BASIC_SWEEP_COMPARE_CROSSHAIR", "1")
    args = [
        f"--grin-basic-capture={screenshot_path.as_posix()}",
        f"--grin-basic-settle-frames={os.environ.get('METRIC_BASIC_SWEEP_SETTLE_FRAMES', '12')}",
        f"--grin-basic-min-rh-step={os.environ.get('METRIC_BASIC_SWEEP_MIN_RH_STEP', '20')}",
        f"--grin-basic-min-processed-rows={os.environ.get('METRIC_BASIC_SWEEP_MIN_PROCESSED_ROWS', '64')}",
        f"--grin-basic-capture-film-opacity={os.environ.get('METRIC_BASIC_SWEEP_CAPTURE_FILM_OPACITY', '1.0')}",
        f"--grin-basic-compare-grid={compare_grid}",
        f"--grin-basic-compare-crosshair={compare_crosshair}",
        "--grin-basic-exit-after-capture=1",
    ]
    renderer = case_data.get("renderer", {})
    if "step_scale" in renderer:
        args.append(f"--grin-basic-step-scale={renderer['step_scale']}")
    if "metric_gain" in renderer:
        args.append(f"--grin-basic-metric-gain={renderer['metric_gain']}")
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
    failure = None
    if completed.returncode != 0:
        failure = f"godot_exit_{completed.returncode}"
    elif parsed.get("captureFailure"):
        failure = parsed["captureFailure"]
    elif not screenshot_path.exists():
        failure = "missing_screenshot"
    elif not parsed.get("resolved"):
        failure = "missing_resolved_log"
    return {
        "caseId": case_data["id"],
        "caseName": case_data["id"],
        "rung": case_data["rung"],
        "family": case_data["family"],
        "familyValue": case_data["familyValue"],
        "scene": case_data["scene"],
        "launcher": case_data["launcher"],
        "requestedRendererOverrides": case_data.get("renderer", {}),
        "resolved": parsed.get("resolved"),
        "rendererConfig": parsed.get("renderer"),
        "metricDiagnostics": parsed.get("metricDiag"),
        "metricScalarMap": parsed.get("metricScalarMap"),
        "renderHealth": parsed.get("renderHealth"),
        "captureStats": {
            "tracedPixels": capture.get("tracedPixels"),
            "sourceHits": capture.get("sourceHits"),
            "backgroundHits": capture.get("backgroundHits"),
            "absorbedHits": capture.get("absorbedHits"),
            "missHits": capture.get("missHits"),
            "rhStep": capture.get("rhStep"),
            "processedRows": capture.get("processedRows"),
        },
        "screenshotPath": str(screenshot_path),
        "logFile": str(log_path),
        "visibleDistortionEmerged": None,
        "verdict": "pending_review",
        "status": "failed" if failure else "ok",
        "failureReason": failure,
    }


def crop_image(path: str) -> Image.Image:
    image = Image.open(path).convert("RGB")
    width, height = image.size
    return image.crop((int(width * 0.08), int(height * 0.06), int(width * 0.92), int(height * 0.78)))


def image_metrics(path: str) -> dict:
    crop = crop_image(path)
    gray = crop.convert("L")
    edges = gray.filter(ImageFilter.FIND_EDGES)
    gray_stat = ImageStat.Stat(gray)
    edge_mean = ImageStat.Stat(edges).mean[0]
    values = list(gray.getdata())
    total = max(1, len(values))
    non_dark = sum(1 for v in values if v >= 24) / total
    return {
        "meanLuma": round(gray_stat.mean[0], 6),
        "stddevLuma": round(gray_stat.stddev[0], 6),
        "nonDarkPct": round(non_dark, 6),
        "edgeMean": round(edge_mean, 6),
        "structureScore": round(non_dark * 100.0 + edge_mean / 8.0, 6),
    }


def diff_artifact(metric_path: str, grin_path: str, output_path: Path) -> dict:
    metric = crop_image(metric_path)
    grin = crop_image(grin_path)
    if metric.size != grin.size:
        target = (min(metric.width, grin.width), min(metric.height, grin.height))
        metric = ImageOps.fit(metric, target, method=Image.Resampling.LANCZOS)
        grin = ImageOps.fit(grin, target, method=Image.Resampling.LANCZOS)
    diff_rgb = ImageChops.difference(grin, metric)
    diff_luma = ImageChops.difference(grin.convert("L"), metric.convert("L"))
    heat = ImageOps.colorize(diff_luma.point(lambda v: min(255, int(v * 3.0))), black=(10, 14, 22), mid=(255, 136, 20), white=(255, 248, 230))
    output_path.parent.mkdir(parents=True, exist_ok=True)
    heat.save(output_path)
    values = list(diff_luma.getdata())
    total = max(1, len(values))
    stat = ImageStat.Stat(diff_luma)
    changed = sum(1 for v in values if v >= 12)
    return {
        "heatmapPath": str(output_path),
        "changedPixelCount": changed,
        "changedPixelPct": round(changed / total, 6),
        "meanAbsLumaDiff": round(stat.mean[0], 6),
        "similarityProxy": round(max(0.0, 1.0 - stat.mean[0] / 255.0), 6),
    }


def build_contact_sheet(summary_path: Path, output_path: Path, title: str) -> None:
    subprocess.run(
        [sys.executable, str(CONTACT_SHEET_SCRIPT), "--summary", str(summary_path), "--output", str(output_path), "--title", title, "--columns", "3"],
        cwd=ROOT,
        check=True,
    )


def ensure_grin_summary(summary_path: Path, refresh: bool) -> Path:
    if refresh or not summary_path.exists():
        subprocess.run([sys.executable, str(ROOT / "tools" / "grin_basic_visual_sweep.py")], cwd=ROOT, check=True)
    if not summary_path.exists():
        raise FileNotFoundError(f"Missing GRIN summary: {summary_path}")
    return summary_path


def baseline_case_id(rung: str) -> str:
    return {"straight": "straight_reference", "minimal": "minimal_baseline", "stronger": "stronger_baseline"}[rung]


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def write_report(summary: dict, stable_cases: list[dict], report_path: Path) -> None:
    family_scores = defaultdict(list)
    hitless_cases = 0
    zero_turn_cases = 0
    zero_reason_totals = defaultdict(int)
    for case_data in stable_cases:
        if case_data["family"] not in {"step", "gain", "depth"}:
            pass
        else:
            family_scores[case_data["family"]].append(case_data["improvementScore"])
        capture = case_data.get("captureStats") or {}
        if (capture.get("sourceHits") or 0) == 0 and (capture.get("backgroundHits") or 0) == 0 and case_data["rung"] != "straight":
            hitless_cases += 1
        metric_diag = case_data.get("metricDiagnostics") or {}
        if case_data["rung"] != "straight" and (metric_diag.get("steeringTurns") or 0) == 0:
            zero_turn_cases += 1
        zero_reasons = str(metric_diag.get("zeroReasons") or "")
        if zero_reasons and zero_reasons != "none":
            for token in zero_reasons.split(","):
                if "=" not in token:
                    continue
                key, value = token.split("=", 1)
                try:
                    zero_reason_totals[key] += int(value)
                except ValueError:
                    continue
    family_means = {k: round(sum(v) / len(v), 6) for k, v in family_scores.items() if v}
    best_family = max(family_means, key=family_means.get) if family_means else "none"
    visible = [c for c in stable_cases if c.get("visibleDistortionEmerged")]
    best_by_rung = {}
    for rung in ("minimal", "stronger"):
        rung_cases = [c for c in stable_cases if c["rung"] == rung]
        if rung_cases:
            best_by_rung[rung] = max(rung_cases, key=lambda c: (1 if c.get("visibleDistortionEmerged") else 0, c.get("improvementScore", 1.0), c["imageMetrics"]["structureScore"]))
    dominant_zero_reason = max(zero_reason_totals, key=zero_reason_totals.get) if zero_reason_totals else "none"
    metric_case_count = max(1, sum(1 for c in stable_cases if c["rung"] != "straight"))
    materially_helped = family_means.get(best_family, 1.0) >= 1.02
    if hitless_cases >= metric_case_count and zero_turn_cases >= metric_case_count:
        limiter = "other transport-path issue"
    elif best_family == "gain":
        limiter = "scale"
    elif best_family == "step":
        limiter = "step size"
    elif best_family == "depth":
        limiter = "max depth"
    else:
        limiter = "other transport-path issue"
    lines = [
        f"# Metric Basic Visual Diagnostic Report ({summary['runDate']})",
        "",
        "## Outcome",
        "",
        (
            f"- Most effective bounded family: `{best_family}` (mean score `{family_means.get(best_family, 1.0):0.3f}`), "
            "but the uplift was marginal and did not produce a visually responsive Metric image."
            if materially_helped is False and best_family != "none"
            else f"- Most effective bounded family: `{best_family}` (mean score `{family_means.get(best_family, 1.0):0.3f}`)."
        ),
        f"- First visually obvious Metric distortion: `{visible[0]['caseId']}`." if visible else "- First visually obvious Metric distortion: none within this bounded sweep.",
        f"- Provisional dominant limiter: `{limiter}`.",
        "",
        "## Canonical Baselines",
        "",
    ]
    for rung in ("minimal", "stronger"):
        best = best_by_rung.get(rung)
        if best:
            renderer = best.get("rendererConfig") or {}
            lines.append(
                f"- `{rung}`: `{best['caseId']}` "
                f"(steps={renderer.get('stepsPerRay', 'na')}, stepLength={renderer.get('stepLength', 'na')}, metricGain={renderer.get('metricGain', 'na')})."
            )
    lines.extend(
        [
            "",
            "## Transport Readout",
            "",
            f"- Metric cases with zero source/background hits at capture: `{hitless_cases}` / `{metric_case_count}`.",
            f"- Metric cases with zero logged steering turns: `{zero_turn_cases}` / `{metric_case_count}`.",
            f"- Dominant logged zero-reason token: `{dominant_zero_reason}`.",
            "",
            "## Artifacts",
            "",
            f"- Metric sweep contact sheet: `{summary['artifacts']['metricContactSheet']}`",
            f"- GRIN-vs-Metric heatmaps: `{summary['artifacts']['comparisonContactSheet']}`",
            f"- Summary JSON: `{LOG_ROOT / 'summary.json'}`",
        ]
    )
    report_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    args = parse_args()
    godot_exe = require_godot_exe()
    grin_summary_path = ensure_grin_summary(Path(args.grin_summary), args.refresh_grin_baselines)
    grin_summary = json.loads(grin_summary_path.read_text(encoding="utf-8"))
    grin_cases = {c["caseId"]: c for c in grin_summary.get("cases", []) if c.get("status") == "ok"}
    for required in ("straight_reference", "minimal_baseline", "stronger_baseline"):
        if required not in grin_cases:
            raise RuntimeError(f"GRIN summary missing {required}")

    run_date = date.today().isoformat()
    screenshot_dir = SCREENSHOT_ROOT / run_date
    comparison_dir = screenshot_dir / "comparisons"
    screenshot_dir.mkdir(parents=True, exist_ok=True)
    comparison_dir.mkdir(parents=True, exist_ok=True)
    LOG_ROOT.mkdir(parents=True, exist_ok=True)

    selected = CASES[: args.max_cases] if args.max_cases > 0 else CASES
    summary = {
        "generatedAt": datetime.now().strftime("%Y-%m-%dT%H:%M:%S"),
        "runDate": run_date,
        "screenshotDir": str(screenshot_dir),
        "comparisonDir": str(comparison_dir),
        "grinSummaryPath": str(grin_summary_path),
        "cases": [],
        "comparisonCases": [],
        "artifacts": {},
    }

    for case_data in selected:
        print(f"RUN {case_data['id']}", flush=True)
        result = run_case(godot_exe, case_data, screenshot_dir, LOG_ROOT)
        summary["cases"].append(result)
        write_json(LOG_ROOT / "summary.json", summary)
        print(json.dumps(result, indent=2), flush=True)

    stable = [c for c in summary["cases"] if c["status"] == "ok"]
    baselines = {c["rung"]: c for c in stable if c["family"] == "baseline"}
    for case_data in stable:
        case_data["imageMetrics"] = image_metrics(case_data["screenshotPath"])
        grin_case_id = baseline_case_id(case_data["rung"])
        comparison = diff_artifact(case_data["screenshotPath"], grin_cases[grin_case_id]["screenshotPath"], comparison_dir / f"{case_data['caseId']}_vs_{grin_case_id}.png")
        comparison["grinCaseId"] = grin_case_id
        case_data["comparison"] = comparison
        summary["comparisonCases"].append(
            {
                "caseId": f"{case_data['caseId']}_vs_{grin_case_id}",
                "rung": case_data["rung"],
                "screenshotPath": comparison["heatmapPath"],
                "status": "ok",
                "contactSheetLabel": f"{case_data['caseId']} vs {grin_case_id}\n{case_data['family']}={case_data['familyValue']}\nchanged={comparison['changedPixelPct']:.3f} mad={comparison['meanAbsLumaDiff']:.1f}",
            }
        )

    for case_data in stable:
        if case_data["rung"] not in baselines or case_data["family"] == "control":
            case_data["improvementScore"] = 1.0
            case_data["visibleDistortionEmerged"] = False
        else:
            base = baselines[case_data["rung"]]
            structure_gain = case_data["imageMetrics"]["structureScore"] / max(base["imageMetrics"]["structureScore"], 0.001)
            gap_gain = base["comparison"]["meanAbsLumaDiff"] / max(case_data["comparison"]["meanAbsLumaDiff"], 0.001)
            case_data["improvementScore"] = round(structure_gain * 0.6 + gap_gain * 0.4, 6)
            case_data["visibleDistortionEmerged"] = (
                case_data["family"] != "baseline"
                and structure_gain >= 1.35
                and gap_gain >= 1.03
                and case_data["imageMetrics"]["nonDarkPct"] >= max(base["imageMetrics"]["nonDarkPct"] * 1.25, 0.01)
            )
        case_data["verdict"] = "visible_metric_curvature" if case_data["visibleDistortionEmerged"] else "weak_or_non_obvious"
        case_data["contactSheetLabel"] = f"{case_data['caseId']}\n{case_data['family']}={case_data['familyValue']}\nscore={case_data['improvementScore']:.3f} rows={case_data['captureStats'].get('processedRows', 'na')}"

    write_json(LOG_ROOT / "contact_sheet_summary.json", {"runDate": run_date, "cases": stable})
    write_json(LOG_ROOT / "comparison_summary.json", {"runDate": run_date, "cases": summary["comparisonCases"]})
    metric_sheet = screenshot_dir / "contact_sheet.png"
    comparison_sheet = screenshot_dir / "comparison_sheet.png"
    build_contact_sheet(LOG_ROOT / "contact_sheet_summary.json", metric_sheet, "Metric Diagnostic Sweep")
    build_contact_sheet(LOG_ROOT / "comparison_summary.json", comparison_sheet, "GRIN vs Metric Heatmaps")
    summary["artifacts"]["metricContactSheet"] = str(metric_sheet)
    summary["artifacts"]["comparisonContactSheet"] = str(comparison_sheet)

    report_path = LOG_ROOT / f"metric_basic_visual_report_{run_date}.md"
    write_report(summary, stable, report_path)
    summary["artifacts"]["reportPath"] = str(report_path)
    write_json(LOG_ROOT / "summary.json", summary)
    print(f"WROTE {LOG_ROOT / 'summary.json'}", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
