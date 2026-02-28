#!/usr/bin/env python3
import argparse
import csv
import json
import os
import re
import sys
from datetime import datetime
from typing import Any, Dict, List, Optional


COLUMNS = [
    "timestamp",
    "log_path",
    "fixture",
    "fixture_curvature_enabled",
    "camera_signature",
    "goal",
    "budget_mode",
    "budget_n",
    "best_probe",
    "best_trust",
    "best_probe_class",
    "best_geomPix",
    "best_raytest_deficit",
    "best_max_geomRayTestsTotalRaw",
    "best_max_p2SampRaw",
    "best_max_geomSegQueriedRaw",
    "final_target_ms_per_frame",
    "final_effective_max_ms",
    "final_rows",
    "final_stride",
    "renderstep_calls",
    "rows_advanced_total",
    "bands_committed",
    "scanline_counters_coarse",
    "budgetStops",
]

SMARTSCALE_RESULT_RE = re.compile(r"\[SmartScaleResult\]\s*(\{.*\})\s*$")
SMARTSCALE_SUMMARY_TAG = "[SmartScale][Summary]"
SMARTSCALE_PROBE_TAG = "[SmartScale][ProbeResult]"
FIXTURE_CURVATURE_DISABLED_TAG = "[FixtureWarn] Field curvature disabled"
TOKEN_RE = re.compile(r"([A-Za-z0-9_]+)=([^\s]+)")
INT_RE = re.compile(r"^-?\d+$")
FLOAT_RE = re.compile(r"^-?(?:\d+\.\d*|\d*\.\d+)$")
DATE_TIME_RE = re.compile(
    r"(?P<y>\d{4})-(?P<m>\d{2})-(?P<d>\d{2})[_T-](?P<h>\d{2})[-:](?P<mi>\d{2})[-:](?P<s>\d{2})"
)
DATE_ONLY_RE = re.compile(r"(?P<y>\d{4})-(?P<m>\d{2})-(?P<d>\d{2})")


def is_missing(value: Any) -> bool:
    return value is None or value == ""


def first_non_missing(*values: Any) -> Any:
    for value in values:
        if not is_missing(value):
            return value
    return None


def parse_scalar_token(text: str) -> Any:
    value = text.strip()
    lower = value.lower()
    if lower in ("", "na", "nan", "null", "none", "n/a"):
        return None
    if lower == "true":
        return True
    if lower == "false":
        return False
    if INT_RE.fullmatch(value):
        try:
            return int(value)
        except ValueError:
            return value
    if FLOAT_RE.fullmatch(value):
        try:
            return float(value)
        except ValueError:
            return value
    return value


def parse_kv_tokens(line: str) -> Dict[str, Any]:
    out: Dict[str, Any] = {}
    for key, raw_value in TOKEN_RE.findall(line):
        out[key] = parse_scalar_token(raw_value)
    return out


def to_float(value: Any) -> Optional[float]:
    if value is None or value == "":
        return None
    if isinstance(value, bool):
        return 1.0 if value else 0.0
    if isinstance(value, (int, float)):
        return float(value)
    try:
        return float(str(value).strip())
    except (TypeError, ValueError):
        return None


def numeric_equal(a: Any, b: Any) -> bool:
    fa = to_float(a)
    fb = to_float(b)
    if fa is None or fb is None:
        return False
    return abs(fa - fb) < 1e-9


def csv_cell(value: Any) -> str:
    if value is None:
        return ""
    if isinstance(value, bool):
        return "1" if value else "0"
    return str(value)


def normalize_bool_like(value: Any) -> Any:
    if value is None:
        return None
    if isinstance(value, bool):
        return 1 if value else 0
    text = str(value).strip().lower()
    if text in ("true", "1"):
        return 1
    if text in ("false", "0"):
        return 0
    return value


def extract_timestamp_from_filename(path: str) -> str:
    base = os.path.splitext(os.path.basename(path))[0]
    match = DATE_TIME_RE.search(base)
    if match:
        return (
            f"{match.group('y')}-{match.group('m')}-{match.group('d')}"
            f"T{match.group('h')}:{match.group('mi')}:{match.group('s')}"
        )
    match = DATE_ONLY_RE.search(base)
    if match:
        return f"{match.group('y')}-{match.group('m')}-{match.group('d')}T00:00:00"
    return datetime.now().isoformat(timespec="seconds")


def guess_fixture_from_path(path: str) -> Optional[str]:
    text = os.path.basename(path).lower()
    if "curved_minimal" in text or "curvedminimal" in text:
        return "CurvedMinimal"
    if "straight" in text:
        return "Straight"
    return None


def read_lines(path: str) -> List[str]:
    try:
        with open(path, "rb") as handle:
            raw = handle.read()
    except OSError:
        return []

    if not raw:
        return []

    encodings = ["utf-8", "utf-16", "utf-16-le", "utf-16-be"]
    for enc in encodings:
        try:
            text = raw.decode(enc)
        except UnicodeDecodeError:
            continue
        if "[SmartScale" in text or enc.startswith("utf-16"):
            return text.splitlines()

    return raw.decode("utf-8", errors="replace").splitlines()


def parse_last_smartscale_result(lines: List[str]) -> Optional[Dict[str, Any]]:
    last_result: Optional[Dict[str, Any]] = None
    for line in lines:
        match = SMARTSCALE_RESULT_RE.search(line.strip())
        if not match:
            continue
        payload = match.group(1).strip()
        try:
            parsed = json.loads(payload)
        except json.JSONDecodeError:
            continue
        if isinstance(parsed, dict):
            last_result = parsed
    return last_result


def parse_last_summary(lines: List[str]) -> Dict[str, Any]:
    last_line: Optional[str] = None
    for line in lines:
        if SMARTSCALE_SUMMARY_TAG in line:
            last_line = line
    if not last_line:
        return {}
    return parse_kv_tokens(last_line)


def parse_probe_results(lines: List[str]) -> List[Dict[str, Any]]:
    probes: List[Dict[str, Any]] = []
    for line in lines:
        if SMARTSCALE_PROBE_TAG in line:
            tokens = parse_kv_tokens(line)
            if tokens:
                probes.append(tokens)
    return probes


def parse_fixture_curvature_enabled(lines: List[str]) -> int:
    for line in lines:
        if FIXTURE_CURVATURE_DISABLED_TAG in line:
            return 0
    return 1


def pick_probe_by_name(entries: List[Dict[str, Any]], probe_name: Any) -> Optional[Dict[str, Any]]:
    if is_missing(probe_name):
        return None
    target = str(probe_name)
    for entry in reversed(entries):
        if str(entry.get("probe", "")) == target:
            return entry
    return None


def pick_best_entry(
    result: Dict[str, Any],
    summary: Dict[str, Any],
    path_entries: List[Dict[str, Any]],
    probe_entries: List[Dict[str, Any]],
) -> (Optional[str], Optional[Dict[str, Any]]):
    best_probe = first_non_missing(summary.get("best"), result.get("best_probe"), result.get("best"))
    best_entry = pick_probe_by_name(path_entries, best_probe)
    if best_entry is None:
        best_entry = pick_probe_by_name(probe_entries, best_probe)

    if best_entry is None:
        best_geom = first_non_missing(result.get("best_geomPix"), summary.get("best_geomPix"))
        if best_geom is not None:
            for entry in reversed(path_entries):
                if numeric_equal(entry.get("geomPix"), best_geom):
                    best_entry = entry
                    break
            if best_entry is None:
                for entry in reversed(probe_entries):
                    if numeric_equal(
                        first_non_missing(entry.get("geomPix"), entry.get("geomPixProcessedRaw")),
                        best_geom,
                    ):
                        best_entry = entry
                        break

    if best_entry is None and path_entries:
        best_entry = path_entries[-1]
    if best_entry is None and probe_entries:
        best_entry = probe_entries[-1]

    if is_missing(best_probe) and best_entry is not None:
        best_probe = best_entry.get("probe")

    return (str(best_probe) if not is_missing(best_probe) else None, best_entry)


def derive_probe_class(best_probe_class: Any, best_trust: Any) -> Any:
    if not is_missing(best_probe_class):
        return best_probe_class
    trust_val = to_float(best_trust)
    if trust_val is None:
        return None
    return "trusted" if int(trust_val) == 1 else "untrusted"


def build_record_from_lines(log_path: str, lines: List[str]) -> Dict[str, Any]:
    record: Dict[str, Any] = {key: None for key in COLUMNS}
    abs_path = os.path.abspath(log_path) if log_path and log_path.upper() != "NUL" else log_path
    record["timestamp"] = extract_timestamp_from_filename(log_path) if log_path else datetime.now().isoformat(
        timespec="seconds"
    )
    record["log_path"] = abs_path

    if not log_path or log_path.upper() == "NUL":
        return record

    result = parse_last_smartscale_result(lines)
    summary = parse_last_summary(lines)
    probe_entries = parse_probe_results(lines)

    if result is None:
        result = {}

    path_entries: List[Dict[str, Any]] = []
    raw_path = result.get("escalation_path")
    if isinstance(raw_path, list):
        for entry in raw_path:
            if isinstance(entry, dict):
                path_entries.append(entry)

    best_probe, best_entry = pick_best_entry(result, summary, path_entries, probe_entries)
    fallback_probe = pick_probe_by_name(probe_entries, best_probe)
    if fallback_probe is None and probe_entries:
        fallback_probe = probe_entries[-1]

    source_probe = best_entry if best_entry is not None else fallback_probe

    best_trust = first_non_missing(
        result.get("best_trust"),
        summary.get("best_trust"),
        source_probe.get("trust") if isinstance(source_probe, dict) else None,
    )
    best_probe_class = first_non_missing(
        result.get("best_probe_class"),
        source_probe.get("probe_class") if isinstance(source_probe, dict) else None,
    )
    best_probe_class = derive_probe_class(best_probe_class, best_trust)

    record["fixture"] = first_non_missing(result.get("fixture"), guess_fixture_from_path(log_path))
    record["fixture_curvature_enabled"] = parse_fixture_curvature_enabled(lines)
    record["camera_signature"] = result.get("camera_signature")
    record["goal"] = result.get("goal")
    record["budget_mode"] = first_non_missing(
        result.get("budget_mode"),
        summary.get("budget_mode"),
        source_probe.get("budget_mode") if isinstance(source_probe, dict) else None,
    )
    record["budget_n"] = first_non_missing(
        result.get("budget_n"),
        summary.get("budget_n"),
        source_probe.get("budget_n") if isinstance(source_probe, dict) else None,
    )
    record["best_probe"] = best_probe
    record["best_trust"] = best_trust
    record["best_probe_class"] = best_probe_class
    record["best_geomPix"] = first_non_missing(
        result.get("best_geomPix"),
        source_probe.get("geomPix") if isinstance(source_probe, dict) else None,
        source_probe.get("geomPixProcessedRaw") if isinstance(source_probe, dict) else None,
        summary.get("best_geomPix"),
    )
    record["best_raytest_deficit"] = (
        source_probe.get("raytest_deficit") if isinstance(source_probe, dict) else None
    )
    record["best_max_geomRayTestsTotalRaw"] = first_non_missing(
        source_probe.get("max_geomRayTestsTotalRaw") if isinstance(source_probe, dict) else None,
        source_probe.get("geomRayTestsTotalRaw") if isinstance(source_probe, dict) else None,
    )
    record["best_max_p2SampRaw"] = first_non_missing(
        source_probe.get("max_p2SampRaw") if isinstance(source_probe, dict) else None,
        source_probe.get("p2SampRaw") if isinstance(source_probe, dict) else None,
    )
    record["best_max_geomSegQueriedRaw"] = first_non_missing(
        source_probe.get("max_geomSegQueriedRaw") if isinstance(source_probe, dict) else None,
        source_probe.get("geomSegQueriedRaw") if isinstance(source_probe, dict) else None,
    )
    record["final_target_ms_per_frame"] = first_non_missing(
        result.get("final_target_ms_per_frame"),
        result.get("final_target_ms"),
        source_probe.get("targetMs") if isinstance(source_probe, dict) else None,
    )
    record["final_effective_max_ms"] = result.get("final_effective_max_ms")
    record["final_rows"] = first_non_missing(
        result.get("final_rows"),
        source_probe.get("rows") if isinstance(source_probe, dict) else None,
    )
    record["final_stride"] = first_non_missing(
        result.get("final_stride"),
        source_probe.get("stride") if isinstance(source_probe, dict) else None,
    )
    record["renderstep_calls"] = first_non_missing(
        result.get("renderstep_calls"),
        source_probe.get("renderstep_calls") if isinstance(source_probe, dict) else None,
    )
    record["rows_advanced_total"] = first_non_missing(
        result.get("rows_advanced_total"),
        source_probe.get("rows_advanced_total") if isinstance(source_probe, dict) else None,
    )
    record["bands_committed"] = first_non_missing(
        result.get("bands_committed"),
        source_probe.get("bands_committed") if isinstance(source_probe, dict) else None,
    )
    record["scanline_counters_coarse"] = normalize_bool_like(
        first_non_missing(
            result.get("scanline_counters_coarse"),
            source_probe.get("scanline_counters_coarse") if isinstance(source_probe, dict) else None,
        )
    )
    record["budgetStops"] = first_non_missing(
        source_probe.get("budgetStops") if isinstance(source_probe, dict) else None,
        source_probe.get("budgetStopCount") if isinstance(source_probe, dict) else None,
        summary.get("budgetStops"),
    )
    record["mode"] = first_non_missing(result.get("mode"), summary.get("mode"))
    record["gating_excluded"] = first_non_missing(
        source_probe.get("gating_excluded") if isinstance(source_probe, dict) else None,
        summary.get("gating_excluded"),
        result.get("gating_excluded"),
    )

    return record


def build_record(log_path: str) -> Dict[str, Any]:
    if not log_path or log_path.upper() == "NUL":
        return build_record_from_lines(log_path, [])
    try:
        lines = read_lines(log_path)
        return build_record_from_lines(log_path, lines)
    except Exception:
        # Keep extractor never-throw for production log pipelines.
        return build_record_from_lines(log_path, [])


def print_csv_header() -> None:
    writer = csv.writer(sys.stdout, lineterminator="\n")
    writer.writerow(COLUMNS)


def print_csv_row(record: Dict[str, Any]) -> None:
    writer = csv.writer(sys.stdout, lineterminator="\n")
    writer.writerow([csv_cell(record.get(col)) for col in COLUMNS])


def print_compact_row(record: Dict[str, Any], mode_label: Optional[str] = None) -> None:
    fields = [
        ("fixture", record.get("fixture")),
        ("fixture_curvature_enabled", record.get("fixture_curvature_enabled")),
        ("mode", first_non_missing(mode_label, record.get("mode"))),
        ("budget_mode", record.get("budget_mode")),
        ("budget_n", record.get("budget_n")),
        ("best_probe", record.get("best_probe")),
        ("best_probe_class", record.get("best_probe_class")),
        ("best_geomPix", record.get("best_geomPix")),
        ("best_trust", record.get("best_trust")),
        ("max_p2SampRaw", record.get("best_max_p2SampRaw")),
        ("max_geomRayTestsTotalRaw", record.get("best_max_geomRayTestsTotalRaw")),
        ("raytest_deficit", record.get("best_raytest_deficit")),
    ]
    gating_excluded = record.get("gating_excluded")
    if not is_missing(gating_excluded):
        fields.append(("gating_excluded", gating_excluded))

    parts: List[str] = []
    for key, value in fields:
        parts.append(f"{key}={csv_cell(value) if not is_missing(value) else 'na'}")
    print(" ".join(parts))


def run_selftest() -> int:
    sample_log = """
[SmartScale][ProbeResult] probe=trusted trust=1 probe_class=trusted geomPix=11600 max_p2SampRaw=120 max_geomRayTestsTotalRaw=460 rows=72 stride=1 budgetStops=0 budget_mode=renderstep_calls budget_n=60
[SmartScale][ProbeResult] probe=near_trusted trust=0 probe_class=near_trusted geomPix=10800 max_p2SampRaw=112 max_geomRayTestsTotalRaw=500 rows=68 stride=1 budgetStops=1 budget_mode=renderstep_calls budget_n=60
[SmartScale][Summary] best=trusted best_geomPix=11600 best_trust=1 budget_mode=renderstep_calls budget_n=60 budgetStops=0
[SmartScaleResult] {"fixture":"Straight","camera_signature":"camA","goal":"max_hits","budget_mode":"renderstep_calls","budget_n":60,"best_probe":"trusted","best_trust":1,"best_probe_class":"trusted","best_geomPix":11600,"final_target_ms_per_frame":16.6,"final_effective_max_ms":16.6,"final_rows":72,"final_stride":1,"renderstep_calls":60,"rows_advanced_total":2048,"bands_committed":17,"scanline_counters_coarse":true}
""".strip().splitlines()

    record = build_record_from_lines("logs\\render_test_straight_2026-02-27_12-00-00.txt", sample_log)
    checks = [
        record.get("best_probe") == "trusted",
        record.get("fixture") == "Straight",
        to_float(record.get("fixture_curvature_enabled")) == 1.0,
        record.get("budget_mode") == "renderstep_calls",
        to_float(record.get("best_geomPix")) == 11600.0,
        record.get("best_probe_class") == "trusted",
    ]
    if all(checks):
        print("SELFTEST PASS")
        return 0
    print("SELFTEST FAIL")
    return 1


def main() -> int:
    parser = argparse.ArgumentParser(description="Extract SmartScale summary data from a Godot log.")
    parser.add_argument("--json", action="store_true", dest="emit_json", help="emit JSON object to stdout")
    parser.add_argument(
        "--print-compact",
        default="0",
        help="set to 1 to emit compact one-line summary",
    )
    parser.add_argument(
        "--mode-label",
        default="",
        help="optional mode label to include in compact output",
    )
    parser.add_argument(
        "--print-header",
        default="0",
        help="set to 1 to emit CSV header before row output",
    )
    parser.add_argument("--selftest", default="0", help="set to 1 to run internal parser self-test")
    parser.add_argument("log_path", nargs="?", default="", help="path to a single run log")
    args = parser.parse_args()

    emit_header = str(args.print_header).strip().lower() in ("1", "true", "yes", "on")
    emit_compact = str(args.print_compact).strip().lower() in ("1", "true", "yes", "on")
    run_selftest_mode = str(args.selftest).strip().lower() in ("1", "true", "yes", "on")

    if run_selftest_mode:
        return run_selftest()

    if emit_compact:
        record = build_record(args.log_path)
        print_compact_row(record, mode_label=(args.mode_label or "").strip() or None)
        return 0

    if args.emit_json:
        record = build_record(args.log_path)
        print(json.dumps(record, ensure_ascii=True))
        return 0

    if emit_header:
        print_csv_header()

    if not args.log_path or args.log_path.upper() == "NUL":
        return 0

    record = build_record(args.log_path)
    print_csv_row(record)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
