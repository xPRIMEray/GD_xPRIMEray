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
STEP_LENGTHS = (0.040, 0.045, 0.050)
TURN_MULTIPLIERS = (0.8, 1.0, 1.2)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run the Fixture 001 stepLength x turnThreshold DOE.")
    parser.add_argument("--baseline-turn-threshold", type=float, default=None)
    parser.add_argument("--baseline-capture", type=Path, default=DEFAULT_BASELINE_CAPTURE)
    return parser.parse_args()


def load_latest_summary() -> dict:
    summaries = sorted(RUN_ROOT.glob("*/summary.json"), key=lambda path: path.parent.name)
    if not summaries:
        raise FileNotFoundError(f"no Fixture 001 summary.json found under {RUN_ROOT}")
    return json.loads(summaries[-1].read_text(encoding="utf-8"))


def resolve_baseline_turn_threshold(args: argparse.Namespace) -> float:
    if args.baseline_turn_threshold is not None:
        return args.baseline_turn_threshold
    latest = load_latest_summary()
    renderer = latest.get("renderer") or {}
    value = renderer.get("turnThreshold")
    if isinstance(value, (int, float)):
        return float(value)
    return 4.0


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


def case_key(step_length: float, turn_threshold: float) -> tuple[float, float]:
    return (round(step_length, 6), round(turn_threshold, 6))


def is_clean(case: dict) -> bool:
    return (
        case.get("status") == "ok"
        and case.get("guard_progress") == 0
        and case.get("forced_advance") == 0
    )


def run_case(step_length: float, turn_threshold: float, baseline_capture: Path) -> dict:
    before = {path.name for path in RUN_ROOT.iterdir() if path.is_dir()} if RUN_ROOT.exists() else set()
    env = os.environ.copy()
    env["FIXTURE_001_STEP_LENGTH"] = f"{step_length:.3f}"
    env["FIXTURE_001_MIN_STEP_LENGTH"] = f"{step_length:.3f}"
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
            f"harness failed for stepLength={step_length:.3f} turnThreshold={turn_threshold:.6f}"
        )

    after = {path.name for path in RUN_ROOT.iterdir() if path.is_dir()} if RUN_ROOT.exists() else set()
    new_dirs = sorted(after - before)
    if len(new_dirs) != 1:
        raise RuntimeError(
            f"expected one new run dir, found {len(new_dirs)} for stepLength={step_length:.3f} turnThreshold={turn_threshold:.6f}"
        )

    run_dir = RUN_ROOT / new_dirs[0]
    summary = json.loads((run_dir / "summary.json").read_text(encoding="utf-8"))
    metrics = summary.get("metrics") or {}
    params = summary.get("params") or {}
    return {
        "run_dir": str(run_dir),
        "timestamp": summary.get("timestamp"),
        "requested_step_length": params.get("requested_step_length"),
        "requested_turn_threshold": params.get("requested_turn_threshold"),
        "effective_step_length": metrics.get("effective_step_length"),
        "effective_turn_threshold": metrics.get("effective_turn_threshold"),
        "status": metrics.get("status"),
        "guard_progress": metrics.get("guard_progress"),
        "forced_advance": metrics.get("forced_advance"),
        "traced_pixels": metrics.get("traced_pixels"),
        "source_hits": metrics.get("source_hits"),
        "runtime_seconds": metrics.get("runtime_seconds"),
        "hit_success_rate": metrics.get("hit_success_rate"),
        "useful_hit_ratio": safe_ratio(metrics.get("source_hits"), metrics.get("traced_pixels")),
    }


def print_table(cases: list[dict], baseline_turn_threshold: float) -> None:
    lookup = {
        case_key(case["requested_step_length"], case["requested_turn_threshold"]): case
        for case in cases
    }
    print("3x3 result table (cell = traced/source/hr)")
    print("step\\turn | 0.8x | 1.0x | 1.2x")
    for step in STEP_LENGTHS:
        row = [f"{step:.3f}"]
        for multiplier in TURN_MULTIPLIERS:
            threshold = baseline_turn_threshold * multiplier
            case = lookup[case_key(step, threshold)]
            row.append(
                f"{fmt(case['traced_pixels'], 0)}/{fmt(case['source_hits'], 0)}/{fmt(case['hit_success_rate'], 4)}"
            )
        print(" | ".join(row))


def print_clean_runs(clean_cases: list[dict], baseline_turn_threshold: float) -> None:
    if not clean_cases:
        print("scheduler-clean runs: none")
        return
    labels = []
    for case in clean_cases:
        multiplier = case["requested_turn_threshold"] / baseline_turn_threshold if baseline_turn_threshold else None
        labels.append(
            f"step={fmt(case['requested_step_length'])}, turn={fmt(case['requested_turn_threshold'])} ({fmt(multiplier, 1)}x)"
        )
    print("scheduler-clean runs: " + "; ".join(labels))


def print_ranked(clean_cases: list[dict], baseline_turn_threshold: float) -> None:
    ranked = sorted(
        clean_cases,
        key=lambda case: (
            -(safe_rate(case.get("source_hits"), case.get("runtime_seconds")) or float("-inf")),
            -(safe_rate(case.get("traced_pixels"), case.get("runtime_seconds")) or float("-inf")),
            -(case.get("useful_hit_ratio") or float("-inf")),
        ),
    )
    print("clean ranking")
    for index, case in enumerate(ranked, start=1):
        multiplier = case["requested_turn_threshold"] / baseline_turn_threshold if baseline_turn_threshold else None
        print(
            f"{index}. step={fmt(case['requested_step_length'])} "
            f"turn={fmt(case['requested_turn_threshold'])} ({fmt(multiplier, 1)}x) "
            f"hit_rate={fmt(safe_rate(case.get('source_hits'), case.get('runtime_seconds')), 3)} "
            f"traced_rate={fmt(safe_rate(case.get('traced_pixels'), case.get('runtime_seconds')), 3)} "
            f"useful_hit_ratio={fmt(case.get('useful_hit_ratio'), 6)}"
        )


def describe_interaction(cases: list[dict]) -> str:
    grouped_by_step = {}
    for case in cases:
        grouped_by_step.setdefault(case["requested_step_length"], []).append(case)

    max_turn_spread = 0.0
    for group in grouped_by_step.values():
        hit_rates = [case.get("hit_success_rate") for case in group if isinstance(case.get("hit_success_rate"), (int, float))]
        if hit_rates:
            max_turn_spread = max(max_turn_spread, max(hit_rates) - min(hit_rates))

    if max_turn_spread < 1e-6:
        return (
            "interaction pattern: no observable turnThreshold interaction across the 3x3 matrix; "
            "stepLength changed coverage, while turnThreshold remained behaviorally inert on the current Fixture 001 baseline."
        )

    best_case = max(
        (case for case in cases if isinstance(case.get("traced_pixels"), (int, float))),
        key=lambda case: case["traced_pixels"],
        default=None,
    )
    if best_case is None:
        return "interaction pattern: insufficient numeric signal."
    return (
        f"interaction pattern: stepLength dominated, with best traced coverage at step={fmt(best_case['requested_step_length'])}; "
        f"turnThreshold produced only small hit-rate variation (max spread {fmt(max_turn_spread, 6)})."
    )


def main() -> int:
    args = parse_args()
    baseline_turn_threshold = resolve_baseline_turn_threshold(args)
    baseline_capture = args.baseline_capture
    if not baseline_capture.exists():
        raise FileNotFoundError(f"baseline capture not found: {baseline_capture}")

    cases = []
    for step_length in STEP_LENGTHS:
        for multiplier in TURN_MULTIPLIERS:
            turn_threshold = baseline_turn_threshold * multiplier
            cases.append(run_case(step_length, turn_threshold, baseline_capture))

    print()
    print_table(cases, baseline_turn_threshold)
    print()
    clean_cases = [case for case in cases if is_clean(case)]
    print_clean_runs(clean_cases, baseline_turn_threshold)
    print_ranked(clean_cases, baseline_turn_threshold)
    print(describe_interaction(cases))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
