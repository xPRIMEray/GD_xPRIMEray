#!/usr/bin/env python3
import argparse
import json
import os
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
HARNESS = ROOT / "scripts" / "run_fixture_001.sh"
RUN_ROOT = ROOT / "output" / "fixture_runs" / "fixture_001"
DEFAULT_BASELINE_CAPTURE = RUN_ROOT / "2026-03-19T22-57-53" / "capture.png"
FIXED_STEP_LENGTH = 0.040
BASELINE_TURN_THRESHOLD = 2.4
DEFAULT_BASELINE_ERROR_TOLERANCE = 0.01
ERROR_TOLERANCE_MULTIPLIERS = (0.8, 1.0, 1.2)
TURN_THRESHOLDS = (2.0, 2.4, 2.8)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Run the Fixture 001 errorTolerance x turnThreshold DOE at fixed stepLength."
    )
    parser.add_argument("--baseline-turn-threshold", type=float, default=BASELINE_TURN_THRESHOLD)
    parser.add_argument("--baseline-error-tolerance", type=float, default=None)
    parser.add_argument("--baseline-capture", type=Path, default=DEFAULT_BASELINE_CAPTURE)
    return parser.parse_args()


def load_latest_summary() -> dict:
    summaries = sorted(RUN_ROOT.glob("*/summary.json"), key=lambda path: path.parent.name)
    if not summaries:
        raise FileNotFoundError(f"no Fixture 001 summary.json found under {RUN_ROOT}")
    return json.loads(summaries[-1].read_text(encoding="utf-8"))


def resolve_baseline_error_tolerance(args: argparse.Namespace) -> float:
    if args.baseline_error_tolerance is not None:
        return args.baseline_error_tolerance
    latest = load_latest_summary()
    metrics = latest.get("metrics") or {}
    renderer = latest.get("renderer") or {}
    for value in (
        metrics.get("effective_error_tolerance"),
        renderer.get("errorTolerance"),
    ):
        if isinstance(value, (int, float)):
            return float(value)
    return DEFAULT_BASELINE_ERROR_TOLERANCE


def safe_ratio(numerator, denominator):
    if not isinstance(numerator, (int, float)):
        return None
    if not isinstance(denominator, (int, float)) or denominator == 0:
        return None
    return numerator / denominator


def safe_rate(numerator, runtime):
    if not isinstance(numerator, (int, float)):
        return None
    if not isinstance(runtime, (int, float)) or runtime == 0:
        return None
    return numerator / runtime


def fmt(value, digits=3) -> str:
    if value is None:
        return "-"
    if isinstance(value, int):
        return str(value)
    return f"{value:.{digits}f}".rstrip("0").rstrip(".")


def case_key(error_tolerance: float, turn_threshold: float) -> tuple[float, float]:
    return (round(error_tolerance, 8), round(turn_threshold, 6))


def is_clean(case: dict) -> bool:
    return (
        case.get("status") == "ok"
        and case.get("capture_succeeded") is True
        and case.get("launch_audit_status") == "ok"
        and case.get("guard_progress") == 0
        and case.get("forced_advance") == 0
        and isinstance(case.get("processed_rows"), (int, float))
        and case.get("processed_rows") >= 164
        and isinstance(case.get("traced_pixels"), (int, float))
        and case.get("traced_pixels") > 0
    )


def run_case(
    step_length: float,
    error_tolerance: float,
    turn_threshold: float,
    baseline_capture: Path,
) -> dict:
    before = {path.name for path in RUN_ROOT.iterdir() if path.is_dir()} if RUN_ROOT.exists() else set()
    env = os.environ.copy()
    env["FIXTURE_001_STEP_LENGTH"] = f"{step_length:.3f}"
    env["FIXTURE_001_MIN_STEP_LENGTH"] = f"{step_length:.3f}"
    env["FIXTURE_001_ERROR_TOLERANCE"] = f"{error_tolerance:.6f}"
    env["FIXTURE_001_TURN_THRESHOLD"] = f"{turn_threshold:.6f}"
    env["FIXTURE_001_BASELINE_CAPTURE"] = str(baseline_capture)

    completed = subprocess.run(
        [str(HARNESS)],
        cwd=ROOT,
        env=env,
        text=True,
        encoding="utf-8",
        errors="replace",
        capture_output=True,
        check=False,
    )
    sys.stdout.write(completed.stdout)
    sys.stderr.write(completed.stderr)
    if completed.returncode != 0:
        raise RuntimeError(
            "harness failed for "
            f"stepLength={step_length:.3f} "
            f"errorTolerance={error_tolerance:.6f} "
            f"turnThreshold={turn_threshold:.6f}"
        )

    after = {path.name for path in RUN_ROOT.iterdir() if path.is_dir()} if RUN_ROOT.exists() else set()
    new_dirs = sorted(after - before)
    if len(new_dirs) != 1:
        raise RuntimeError(
            "expected one new run dir, found "
            f"{len(new_dirs)} for stepLength={step_length:.3f} "
            f"errorTolerance={error_tolerance:.6f} "
            f"turnThreshold={turn_threshold:.6f}"
        )

    run_dir = RUN_ROOT / new_dirs[0]
    summary = json.loads((run_dir / "summary.json").read_text(encoding="utf-8"))
    metrics = summary.get("metrics") or {}
    params = summary.get("params") or {}
    return {
        "run_dir": str(run_dir),
        "timestamp": summary.get("timestamp"),
        "requested_step_length": params.get("requested_step_length"),
        "requested_error_tolerance": params.get("requested_error_tolerance"),
        "requested_turn_threshold": params.get("requested_turn_threshold"),
        "effective_step_length": metrics.get("effective_step_length"),
        "effective_error_tolerance": metrics.get("effective_error_tolerance"),
        "effective_turn_threshold": metrics.get("effective_turn_threshold"),
        "status": metrics.get("status"),
        "capture_succeeded": metrics.get("capture_succeeded"),
        "launch_audit_status": metrics.get("launch_audit_status"),
        "guard_progress": metrics.get("guard_progress"),
        "forced_advance": metrics.get("forced_advance"),
        "processed_rows": metrics.get("processed_rows"),
        "traced_pixels": metrics.get("traced_pixels"),
        "source_hits": metrics.get("source_hits"),
        "runtime_seconds": metrics.get("runtime_seconds"),
        "hit_success_rate": metrics.get("hit_success_rate"),
        "hit_rate": safe_rate(metrics.get("source_hits"), metrics.get("runtime_seconds")),
        "traced_rate": safe_rate(metrics.get("traced_pixels"), metrics.get("runtime_seconds")),
        "useful_hit_ratio": safe_ratio(metrics.get("source_hits"), metrics.get("traced_pixels")),
    }


def print_table(cases: list[dict], baseline_error_tolerance: float) -> None:
    lookup = {
        case_key(case["requested_error_tolerance"], case["requested_turn_threshold"]): case
        for case in cases
    }
    print("3x3 result table")
    print("rows=errorTolerance multiplier of baseline, cols=turnThreshold")
    header = ["errTol\\turn", *(fmt(value, 1) for value in TURN_THRESHOLDS)]
    print(" | ".join(header))
    for multiplier in ERROR_TOLERANCE_MULTIPLIERS:
        error_tolerance = baseline_error_tolerance * multiplier
        row = [f"{multiplier:.1f}x"]
        for turn_threshold in TURN_THRESHOLDS:
            case = lookup[case_key(error_tolerance, turn_threshold)]
            row.append(
                " ".join(
                    [
                        f"tp={fmt(case['traced_pixels'], 0)}",
                        f"sh={fmt(case['source_hits'], 0)}",
                        f"rt={fmt(case['runtime_seconds'], 3)}s",
                        f"hr={fmt(case['hit_rate'], 3)}",
                        f"tr={fmt(case['traced_rate'], 3)}",
                        f"uhr={fmt(case['useful_hit_ratio'], 6)}",
                    ]
                )
            )
        print(" | ".join(row))


def print_ranked(clean_cases: list[dict], baseline_error_tolerance: float) -> None:
    ranked = sorted(
        clean_cases,
        key=lambda case: (
            -(case.get("hit_rate") or float("-inf")),
            -(case.get("traced_rate") or float("-inf")),
            -(case.get("useful_hit_ratio") or float("-inf")),
        ),
    )
    print("scheduler-clean ranking")
    if not ranked:
        print("none")
        return
    for index, case in enumerate(ranked, start=1):
        multiplier = (
            case["requested_error_tolerance"] / baseline_error_tolerance
            if baseline_error_tolerance
            else None
        )
        print(
            f"{index}. errTol={fmt(case['requested_error_tolerance'], 6)} ({fmt(multiplier, 1)}x) "
            f"turn={fmt(case['requested_turn_threshold'])} "
            f"hit_rate={fmt(case.get('hit_rate'), 3)} "
            f"traced_rate={fmt(case.get('traced_rate'), 3)} "
            f"useful_hit_ratio={fmt(case.get('useful_hit_ratio'), 6)}"
        )


def describe_interaction(clean_cases: list[dict], baseline_error_tolerance: float) -> tuple[str, str, dict | None]:
    if not clean_cases:
        return "insufficient", "noisy", None

    ranked = sorted(
        clean_cases,
        key=lambda case: (
            -(case.get("hit_rate") or float("-inf")),
            -(case.get("traced_rate") or float("-inf")),
            -(case.get("useful_hit_ratio") or float("-inf")),
        ),
    )
    best_case = ranked[0]

    by_error = {}
    by_turn = {}
    for case in clean_cases:
        by_error.setdefault(case["requested_error_tolerance"], []).append(case)
        by_turn.setdefault(case["requested_turn_threshold"], []).append(case)

    def mean(values: list[float]) -> float | None:
        if not values:
            return None
        return sum(values) / len(values)

    error_means = []
    for value, group in by_error.items():
        rates = [case["hit_rate"] for case in group if isinstance(case.get("hit_rate"), (int, float))]
        if rates:
            error_means.append((value, mean(rates)))

    turn_means = []
    for value, group in by_turn.items():
        rates = [case["hit_rate"] for case in group if isinstance(case.get("hit_rate"), (int, float))]
        if rates:
            turn_means.append((value, mean(rates)))

    error_span = (
        max(score for _, score in error_means) - min(score for _, score in error_means)
        if error_means
        else 0.0
    )
    turn_span = (
        max(score for _, score in turn_means) - min(score for _, score in turn_means)
        if turn_means
        else 0.0
    )

    if error_span <= 1e-9 and turn_span <= 1e-9:
        dominance = "weak"
    elif error_span >= turn_span * 1.5:
        dominance = "dominant"
    elif turn_span >= error_span * 1.5:
        dominance = "weak"
    else:
        dominance = "coupled"

    top_hit_rate = best_case.get("hit_rate")
    near_top = [
        case
        for case in ranked
        if isinstance(case.get("hit_rate"), (int, float))
        and top_hit_rate is not None
        and case["hit_rate"] >= top_hit_rate * 0.99
    ]
    if len(near_top) >= 3:
        shape = "flat"
    elif len(near_top) == 1:
        shape = "peaked"
    else:
        shape = "noisy"

    return dominance, shape, best_case


def main() -> int:
    args = parse_args()
    baseline_turn_threshold = args.baseline_turn_threshold
    baseline_error_tolerance = resolve_baseline_error_tolerance(args)
    baseline_capture = args.baseline_capture
    if not baseline_capture.exists():
        raise FileNotFoundError(f"baseline capture not found: {baseline_capture}")

    cases = []
    for multiplier in ERROR_TOLERANCE_MULTIPLIERS:
        error_tolerance = baseline_error_tolerance * multiplier
        for turn_threshold in TURN_THRESHOLDS:
            cases.append(
                run_case(
                    FIXED_STEP_LENGTH,
                    error_tolerance,
                    turn_threshold,
                    baseline_capture,
                )
            )

    print()
    print_table(cases, baseline_error_tolerance)
    print()
    clean_cases = [case for case in cases if is_clean(case)]
    print_ranked(clean_cases, baseline_error_tolerance)
    dominance, shape, best_case = describe_interaction(clean_cases, baseline_error_tolerance)
    print()
    print(f"interaction_read: errorTolerance appears {dominance} relative to turnThreshold")
    print(f"winning_region: {shape}")
    if best_case is not None:
        print(
            "recommended_operating_point: "
            f"stepLength={fmt(best_case['effective_step_length'], 3)} "
            f"errorTolerance={fmt(best_case['effective_error_tolerance'], 6)} "
            f"turnThreshold={fmt(best_case['effective_turn_threshold'], 3)}"
        )
    if shape in {"flat", "noisy"}:
        center_error_tolerance = baseline_error_tolerance
        print(
            "follow_up_repeatability_check: "
            f"repeat 2 more runs at stepLength={FIXED_STEP_LENGTH:.3f}, "
            f"errorTolerance={fmt(center_error_tolerance, 6)}, "
            f"turnThreshold={fmt(baseline_turn_threshold, 3)} and "
            f"2 more runs at the current top-ranked cell "
            f"(errorTolerance={fmt(best_case['requested_error_tolerance'], 6)}, "
            f"turnThreshold={fmt(best_case['requested_turn_threshold'], 3)})"
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
