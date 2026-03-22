#!/usr/bin/env python3
import argparse
import csv
import math
import sys
from collections import defaultdict
from pathlib import Path
from statistics import median


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_LEDGER_PATH = ROOT / "output" / "characterization_ledger" / "fixture_runs.csv"
DEFAULT_BASELINE_STEP_LENGTH = None


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Print a compact engineering summary for characterization ledger runs."
    )
    parser.add_argument(
        "--ledger-path",
        type=Path,
        default=DEFAULT_LEDGER_PATH,
        help=f"Path to the characterization ledger CSV (default: {DEFAULT_LEDGER_PATH})",
    )
    parser.add_argument(
        "--fixture-id",
        default="fixture_001",
        help="Filter to a fixture id. Pass an empty string to disable this filter.",
    )
    parser.add_argument(
        "--transport-model",
        default=None,
        help="Optional transport model filter.",
    )
    parser.add_argument(
        "--commit-hash",
        default=None,
        help="Optional commit hash filter.",
    )
    parser.add_argument(
        "--baseline-step-length",
        type=float,
        default=DEFAULT_BASELINE_STEP_LENGTH,
        help="Optional baseline step length used for delta reporting.",
    )
    parser.add_argument(
        "--compact",
        action="store_true",
        help="Print a compact dashboard view with only key summary lines.",
    )
    parser.add_argument(
        "--rank-by",
        choices=("default", "robust_score"),
        default="default",
        help="Optional repeatability ranking mode.",
    )
    parser.add_argument(
        "--rank-step-summary-by-robust-score",
        action="store_true",
        help="Sort repeatability summary groups by robust_score instead of effective_stepLength.",
    )
    return parser.parse_args()


def normalize_text(value) -> str:
    if value is None:
        return ""
    return str(value).strip()


def get_field(row: dict, key: str) -> str:
    value = row.get(key, "")
    return normalize_text(value)


def parse_bool(value) -> bool | None:
    text = normalize_text(value).lower()
    if text == "":
        return None
    if text in {"1", "true", "yes", "y", "on"}:
        return True
    if text in {"0", "false", "no", "n", "off"}:
        return False
    return None


def parse_number(value) -> float | None:
    text = normalize_text(value)
    if text == "":
        return None
    try:
        number = float(text)
    except ValueError:
        return None
    if not math.isfinite(number):
        return None
    return number


def parse_intish(value) -> int | None:
    number = parse_number(value)
    if number is None:
        return None
    return int(number)


def values_match_with_tolerance(requested: float | None, effective: float | None) -> bool:
    if requested is None or effective is None:
        return False
    tolerance = max(1e-6, abs(requested) * 1e-4)
    return math.isclose(requested, effective, rel_tol=1e-4, abs_tol=tolerance)


def format_number(value: float | None, decimals: int = 6) -> str:
    if value is None:
        return "-"
    if float(value).is_integer():
        return str(int(value))
    return f"{value:.{decimals}f}".rstrip("0").rstrip(".")


def format_runtime(value: float | None) -> str:
    if value is None:
        return "-"
    formatted = f"{value:.3f}".rstrip("0").rstrip(".")
    return f"{formatted}s"


def format_percent(value: float | None, decimals: int = 2) -> str:
    if value is None:
        return "-"
    return f"{value:.{decimals}f}%".rstrip("0").rstrip(".")


def derive_rate(numerator: float | None, runtime: float | None) -> float | None:
    if numerator is None or runtime is None or runtime == 0:
        return None
    return numerator / runtime


def derive_rate_from_row(row: dict, field: str) -> float | None:
    return derive_rate(parse_number(row.get(field)), parse_number(row.get("runtime")))


def format_rate(value: float | None) -> str:
    if value is None:
        return "-"
    return f"{format_number(value, decimals=1)}/s"


def population_std_dev(values: list[float]) -> float | None:
    if not values:
        return None
    if len(values) == 1:
        return 0.0
    mean = sum(values) / len(values)
    variance = sum((value - mean) ** 2 for value in values) / len(values)
    return math.sqrt(variance)


def hit_rates_for_rows(rows: list[dict]) -> list[float]:
    rates: list[float] = []
    for row in rows:
        rate = derive_rate_from_row(row, "source_hits")
        if rate is not None:
            rates.append(rate)
    return rates


def traced_rates_for_rows(rows: list[dict]) -> list[float]:
    rates: list[float] = []
    for row in rows:
        rate = derive_rate_from_row(row, "traced_pixels")
        if rate is not None:
            rates.append(rate)
    return rates


def robust_score_for_rows(rows: list[dict]) -> float | None:
    hit_rates = hit_rates_for_rows(rows)
    if len(hit_rates) < 2:
        return None
    return median(hit_rates) - 0.5 * (population_std_dev(hit_rates) or 0.0)


def mean(values: list[float]) -> float | None:
    if not values:
        return None
    return sum(values) / len(values)


def load_rows(path: Path) -> list[dict]:
    if not path.exists():
        print(f"ledger not found: {path}", file=sys.stderr)
        return []
    try:
        with path.open("r", newline="", encoding="utf-8") as handle:
            reader = csv.DictReader(handle)
            return list(reader)
    except OSError as exc:
        print(f"failed to read ledger: {path} ({exc})", file=sys.stderr)
        return []


def row_matches(row: dict, fixture_id: str | None, transport_model: str | None, commit_hash: str | None) -> bool:
    if fixture_id is not None and fixture_id != "":
        if get_field(row, "fixture_id") != fixture_id:
            return False
    if transport_model:
        if get_field(row, "transport_model") != transport_model:
            return False
    if commit_hash:
        if get_field(row, "commit_hash") != commit_hash:
            return False
    return True


def verification_flag(row: dict, key: str) -> bool:
    parsed = parse_bool(row.get(key))
    if parsed is not None:
        return parsed

    if key == "runtime_fingerprint_present":
        return bool(get_field(row, "runtime_fingerprint"))
    if key == "assembly_timestamp_present":
        return False
    if key == "effective_step_matches_requested":
        return values_match_with_tolerance(
            parse_number(row.get("requested_stepLength")),
            parse_number(row.get("effective_stepLength")),
        )
    if key == "row_diagnostics_present":
        return any(
            get_field(row, field)
            for field in (
                "processed_rows",
                "total_rows_considered",
                "total_rows_processed",
                "total_rows_skipped",
                "processed_row_start",
                "processed_row_end",
                "zero_hit_rows",
                "row_participation_summary",
            )
        )
    if key == "scheduler_clean":
        return is_scheduler_clean(row)
    if key == "run_verified":
        return run_verified(row)
    return False


def is_scheduler_clean(row: dict) -> bool:
    status_ok = get_field(row, "status").lower() == "ok"
    capture_ok = parse_bool(row.get("capture_succeeded")) is True
    launch_ok = get_field(row, "launch_audit_status").lower() == "ok"
    guard_progress = parse_intish(row.get("guard_progress"))
    forced_advance = parse_intish(row.get("forcedAdvance"))
    processed_rows = parse_number(row.get("processed_rows"))
    traced_pixels = parse_number(row.get("traced_pixels"))
    return (
        status_ok
        and capture_ok
        and launch_ok
        and guard_progress == 0
        and forced_advance == 0
        and processed_rows is not None
        and processed_rows >= 164
        and traced_pixels is not None
        and traced_pixels > 0
    )


def run_verified(row: dict) -> bool:
    return all(
        verification_flag(row, key)
        for key in (
            "runtime_fingerprint_present",
            "assembly_timestamp_present",
            "effective_step_matches_requested",
            "row_diagnostics_present",
            "scheduler_clean",
        )
    )


def format_step_delta(step_length: float | None, baseline_step_length: float | None) -> str:
    if step_length is None or baseline_step_length is None or baseline_step_length == 0:
        return ""
    delta = step_length - baseline_step_length
    delta_pct = (delta / baseline_step_length) * 100.0
    delta_sign = "+" if delta > 0 else ""
    pct_sign = "+" if delta_pct > 0 else ""
    return (
        f" baseline={format_number(baseline_step_length)}"
        f" delta={delta_sign}{format_number(delta)}"
        f" ({pct_sign}{format_percent(delta_pct)})"
    )


def describe_run(row: dict, baseline_step_length: float | None = None) -> str:
    fixture_id = get_field(row, "fixture_id") or "-"
    verification_badge = "verified" if run_verified(row) else "unverified"
    clean_badge = "clean" if is_scheduler_clean(row) else "dirty"
    timestamp = get_field(row, "timestamp") or "-"
    commit_hash = get_field(row, "commit_hash")
    short_commit = commit_hash[:12] if commit_hash else "-"
    runtime_fingerprint = get_field(row, "runtime_fingerprint")
    short_runtime_fingerprint = runtime_fingerprint[:24] if runtime_fingerprint else "-"
    step_length_value = parse_number(row.get("effective_stepLength"))
    step_length = format_number(step_length_value)
    useful_hit_ratio = format_number(parse_number(row.get("useful_hit_ratio")))
    traced_pixels = format_number(parse_number(row.get("traced_pixels")), decimals=0)
    runtime = format_runtime(parse_number(row.get("runtime")))
    traced_rate = format_rate(derive_rate_from_row(row, "traced_pixels"))
    hit_rate = format_rate(derive_rate_from_row(row, "source_hits"))
    total_rows_processed = format_number(parse_number(row.get("total_rows_processed")), decimals=0)
    total_rows_skipped = format_number(parse_number(row.get("total_rows_skipped")), decimals=0)
    total_rows_considered = format_number(parse_number(row.get("total_rows_considered")), decimals=0)
    processed_row_start = format_number(parse_number(row.get("processed_row_start")), decimals=0)
    processed_row_end = format_number(parse_number(row.get("processed_row_end")), decimals=0)
    return (
        f"{fixture_id} | {clean_badge}/{verification_badge} | "
        f"step={step_length}"
        f"{f' vs {format_number(baseline_step_length)} ({format_percent((step_length_value - baseline_step_length)/baseline_step_length*100)})' if baseline_step_length and step_length_value is not None else ''} | "
        f"traced={traced_pixels} | "
        f"rows={total_rows_processed}/{total_rows_skipped}/{total_rows_considered} "
        f"span={processed_row_start}-{processed_row_end} | "
        f"hit={useful_hit_ratio} | "
        f"time={runtime} | "
        f"traced_rate={traced_rate} | "
        f"hit_rate={hit_rate} | "
        f"{short_commit} | "
        f"rtfp={short_runtime_fingerprint} | "
        f"ts={timestamp}"
    )


def sort_key_for_top_runs(row: dict) -> tuple[float, float, float, float, float, str]:
    useful_hit_ratio = parse_number(row.get("useful_hit_ratio"))
    traced_pixels = parse_number(row.get("traced_pixels"))
    runtime = parse_number(row.get("runtime"))
    traced_rate = derive_rate_from_row(row, "traced_pixels")
    hit_rate = derive_rate_from_row(row, "source_hits")
    timestamp = get_field(row, "timestamp")
    return (
        -(hit_rate if hit_rate is not None else float("-inf")),
        -(traced_rate if traced_rate is not None else float("-inf")),
        -(useful_hit_ratio if useful_hit_ratio is not None else float("-inf")),
        -(traced_pixels if traced_pixels is not None else float("-inf")),
        runtime if runtime is not None else float("inf"),
        timestamp,
    )


def best_by_metric(rows: list[dict], field: str) -> dict | None:
    best_row = None
    best_value = None
    for row in rows:
        value = parse_number(row.get(field))
        if value is None:
            continue
        if best_value is None or value > best_value:
            best_row = row
            best_value = value
    return best_row


def fastest_run(rows: list[dict]) -> dict | None:
    best_row = None
    best_value = None
    for row in rows:
        value = parse_number(row.get("runtime"))
        if value is None:
            continue
        if best_value is None or value < best_value:
            best_row = row
            best_value = value
    return best_row


def best_by_derived_rate(rows: list[dict], field: str) -> dict | None:
    best_row = None
    best_value = None
    for row in rows:
        value = derive_rate_from_row(row, field)
        if value is None:
            continue
        if best_value is None or value > best_value:
            best_row = row
            best_value = value
    return best_row


def print_headline(label: str, row: dict | None, baseline_step_length: float | None) -> None:
    if row is None:
        print(f"{label}: -")
        return
    print(f"{label}: {describe_run(row, baseline_step_length)}")


def print_top_runs(rows: list[dict], baseline_step_length: float | None) -> None:
    print("top 5 clean runs:")
    if not rows:
        print("  -")
        return
    for index, row in enumerate(sorted(rows, key=sort_key_for_top_runs)[:5], start=1):
        print(f"  {index}. {describe_run(row, baseline_step_length)}")


def step_sort_key(value: str):
    parsed = parse_number(value)
    return (0, parsed) if parsed is not None else (1, value)


def group_key_sort_key(group_key: tuple[str, str, str]):
    return tuple(step_sort_key(part) for part in group_key)


def group_label(group_key: tuple[str, str, str]) -> str:
    step, error_tolerance, turn_threshold = group_key
    return (
        f"step={step or '-'} "
        f"err={error_tolerance or '-'} "
        f"turn={turn_threshold or '-'}"
    )


def compute_group_stats(rows: list[dict]) -> dict:
    hit_rates = hit_rates_for_rows(rows)
    traced_rates = traced_rates_for_rows(rows)
    return {
        "count_runs": len(rows),
        "mean_hit_rate": mean(hit_rates),
        "median_hit_rate": median(hit_rates) if hit_rates else None,
        "std_dev_hit_rate": population_std_dev(hit_rates),
        "mean_traced_rate": mean(traced_rates),
        "std_dev_traced_rate": population_std_dev(traced_rates),
        "robust_score": robust_score_for_rows(rows),
    }


def grouped_repeatability_rows(rows: list[dict]) -> dict[tuple[str, str, str], list[dict]]:
    grouped: dict[tuple[str, str, str], list[dict]] = defaultdict(list)
    for row in rows:
        group_key = (
            get_field(row, "effective_stepLength"),
            get_field(row, "effective_errorTolerance"),
            get_field(row, "effective_turnThreshold"),
        )
        if not any(group_key):
            continue
        grouped[group_key].append(row)
    return grouped


def print_step_summary(
    rows: list[dict],
    clean_rows: list[dict],
    rank_by_robust_score: bool = False,
) -> None:
    present_values = [get_field(row, "effective_stepLength") for row in rows if get_field(row, "effective_stepLength")]
    if not present_values:
        return

    grouped_all: dict[str, list[dict]] = defaultdict(list)
    grouped_clean: dict[str, list[dict]] = defaultdict(list)
    for row in rows:
        step = get_field(row, "effective_stepLength")
        if step:
            grouped_all[step].append(row)
    for row in clean_rows:
        step = get_field(row, "effective_stepLength")
        if step:
            grouped_clean[step].append(row)

    print("by effective_stepLength:")
    robust_scores = {
        step: robust_score_for_rows(grouped_clean.get(step, []))
        for step in grouped_all.keys()
    }
    ordered_steps = sorted(grouped_all.keys(), key=step_sort_key)
    if rank_by_robust_score:
        ordered_steps = sorted(
            grouped_all.keys(),
            key=lambda step: (
                -(robust_scores[step] if robust_scores[step] is not None else float("-inf")),
                step_sort_key(step),
            ),
        )

    for step in ordered_steps:
        all_rows = grouped_all[step]
        clean_group = grouped_clean.get(step, [])
        best_hit_rate = best_by_derived_rate(clean_group, "source_hits")
        best_traced_rate = best_by_derived_rate(clean_group, "traced_pixels")
        best_useful = best_by_metric(clean_group, "useful_hit_ratio")
        best_traced = best_by_metric(clean_group, "traced_pixels")
        fastest = fastest_run(clean_group)
        robust_score = robust_scores[step]
        hit_rate_value = derive_rate_from_row(best_hit_rate, "source_hits") if best_hit_rate else None
        traced_rate_value = derive_rate_from_row(best_traced_rate, "traced_pixels") if best_traced_rate else None
        useful_value = parse_number(best_useful.get("useful_hit_ratio")) if best_useful else None
        traced_value = parse_number(best_traced.get("traced_pixels")) if best_traced else None
        runtime_value = parse_number(fastest.get("runtime")) if fastest else None
        print(
            "  "
            f"{step}: total={len(all_rows)} clean={len(clean_group)} "
            f"robust_score={format_rate(robust_score)} "
            f"best_hit_rate={format_rate(hit_rate_value)} "
            f"best_traced_rate={format_rate(traced_rate_value)} "
            f"best_useful_hit_ratio={format_number(useful_value)} "
            f"best_traced_pixels={format_number(traced_value, decimals=0)} "
            f"fastest={format_runtime(runtime_value)}"
        )


def print_grouped_repeatability_summary(rows: list[dict], rank_by: str) -> list[tuple[tuple[str, str, str], dict]]:
    grouped = grouped_repeatability_rows(rows)
    if not grouped:
        return []

    summaries = [(group_key, compute_group_stats(group_rows)) for group_key, group_rows in grouped.items()]
    if rank_by == "robust_score":
        summaries.sort(
            key=lambda item: (
                -(item[1]["robust_score"] if item[1]["robust_score"] is not None else float("-inf")),
                group_key_sort_key(item[0]),
            )
        )
    else:
        summaries.sort(key=lambda item: group_key_sort_key(item[0]))

    print("repeatability by stepLength/errorTolerance/turnThreshold:")
    for group_key, stats in summaries:
        print(
            "  "
            f"{group_label(group_key)} "
            f"runs={stats['count_runs']} "
            f"mean_hit_rate={format_rate(stats['mean_hit_rate'])} "
            f"median_hit_rate={format_rate(stats['median_hit_rate'])} "
            f"std_dev_hit_rate={format_rate(stats['std_dev_hit_rate'])} "
            f"mean_traced_rate={format_rate(stats['mean_traced_rate'])} "
            f"std_dev_traced_rate={format_rate(stats['std_dev_traced_rate'])} "
            f"robust_score={format_rate(stats['robust_score'])}"
        )
    return summaries


def print_verification_summary(rows: list[dict]) -> None:
    total = len(rows)
    verified = sum(1 for row in rows if run_verified(row))
    runtime_fingerprint_present = sum(1 for row in rows if verification_flag(row, "runtime_fingerprint_present"))
    assembly_timestamp_present = sum(1 for row in rows if verification_flag(row, "assembly_timestamp_present"))
    effective_step_matches_requested = sum(1 for row in rows if verification_flag(row, "effective_step_matches_requested"))
    row_diagnostics_present = sum(1 for row in rows if verification_flag(row, "row_diagnostics_present"))
    scheduler_clean = sum(1 for row in rows if verification_flag(row, "scheduler_clean"))
    print(
        "verification: "
        f"verified={verified}/{total} "
        f"runtime_fp={runtime_fingerprint_present}/{total} "
        f"assembly_ts={assembly_timestamp_present}/{total} "
        f"step_match={effective_step_matches_requested}/{total} "
        f"row_diag={row_diagnostics_present}/{total} "
        f"scheduler_clean={scheduler_clean}/{total}"
    )


def print_compact_best_performers(clean_rows: list[dict], repeatability_summary: list[tuple[tuple[str, str, str], dict]]) -> None:
    best_raw = best_by_derived_rate(clean_rows, "source_hits")
    if best_raw is None:
        print("best raw: -")
    else:
        print(
            "best raw: "
            f"step={format_number(parse_number(best_raw.get('effective_stepLength')))} "
            f"err={format_number(parse_number(best_raw.get('effective_errorTolerance')))} "
            f"turn={format_number(parse_number(best_raw.get('effective_turnThreshold')))} "
            f"hit_rate={format_number(derive_rate_from_row(best_raw, 'source_hits'), decimals=1)}"
        )

    best_robust = None
    for group_key, stats in repeatability_summary:
        if stats["robust_score"] is None:
            continue
        if best_robust is None or stats["robust_score"] > best_robust[1]["robust_score"]:
            best_robust = (group_key, stats)
    if best_robust is None:
        print("best robust: -")
    else:
        group_key, stats = best_robust
        print(
            "best robust: "
            f"step={group_key[0] or '-'} "
            f"err={group_key[1] or '-'} "
            f"turn={group_key[2] or '-'} "
            f"robust_score={format_number(stats['robust_score'], decimals=1)}"
        )


def main() -> int:
    args = parse_args()
    rows = load_rows(args.ledger_path)
    if not rows:
        return 1

    filtered_rows = [
        row
        for row in rows
        if row_matches(row, args.fixture_id, args.transport_model, args.commit_hash)
    ]
    clean_rows = [row for row in filtered_rows if is_scheduler_clean(row)]
    rank_by = "robust_score" if args.rank_step_summary_by_robust_score else args.rank_by

    fixture_label = args.fixture_id if args.fixture_id else "(all fixtures)"
    print(f"fixture summary: {fixture_label}")
    if args.transport_model:
        print(f"transport_model filter: {args.transport_model}")
    if args.commit_hash:
        print(f"commit_hash filter: {args.commit_hash}")
    print(f"total rows considered: {len(filtered_rows)}")
    print(f"scheduler-clean rows: {len(clean_rows)}")
    print(f"verified rows: {sum(1 for row in filtered_rows if run_verified(row))}")
    if args.baseline_step_length is not None:
        print(f"baseline stepLength: {format_number(args.baseline_step_length)}")

    print_headline(
        "best traced clean run",
        best_by_metric(clean_rows, "traced_pixels"),
        args.baseline_step_length,
    )
    print_headline(
        "best useful-hit clean run",
        best_by_metric(clean_rows, "useful_hit_ratio"),
        args.baseline_step_length,
    )
    fastest = fastest_run(clean_rows)
    if fastest is None:
        print("fastest clean run: -")
    else:
        print(
            "fastest clean run: "
            f"{describe_run(fastest, args.baseline_step_length)}"
        )

    print_verification_summary(filtered_rows)

    if not args.compact:
        print_top_runs(clean_rows, args.baseline_step_length)
    print_step_summary(
        filtered_rows,
        clean_rows,
        rank_by_robust_score=(rank_by == "robust_score"),
    )
    repeatability_summary = print_grouped_repeatability_summary(clean_rows, rank_by=rank_by)
    if args.compact:
        print_compact_best_performers(clean_rows, repeatability_summary)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
