#!/usr/bin/env python3
import argparse
import csv
import io
import json
import os
import re
import statistics
from collections import Counter
from typing import Any, Dict, List, Optional, Tuple


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

TARGET_CLASSES = ("trusted", "near_trusted", "productive_partial", "untrusted")
SMARTSCALE_RESULT_RE = re.compile(r"\[SmartScaleResult\]\s*(\{.*\})\s*$")
SMARTSCALE_PROBE_TAG = "[SmartScale][ProbeResult]"
TOKEN_RE = re.compile(r"([A-Za-z0-9_]+)=([^\s]+)")


def parse_float(value: Optional[str]) -> Optional[float]:
    if value is None:
        return None
    text = value.strip()
    if text == "":
        return None
    lower = text.lower()
    if lower in ("na", "nan", "none", "null", "n/a"):
        return None
    try:
        return float(text)
    except ValueError:
        return None


def normalize_class(value: Optional[str]) -> str:
    if value is None:
        return ""
    text = value.strip().lower().replace("-", "_").replace(" ", "_")
    aliases = {
        "neartrusted": "near_trusted",
        "productivepartial": "productive_partial",
    }
    return aliases.get(text, text)


def parse_kv_tokens(line: str) -> Dict[str, str]:
    out: Dict[str, str] = {}
    for key, value in TOKEN_RE.findall(line):
        out[key] = value
    return out


def decode_text(raw: bytes) -> str:
    if not raw:
        return ""
    text = ""
    for enc in ("utf-8", "utf-16", "utf-16-le", "utf-16-be"):
        try:
            text = raw.decode(enc)
        except UnicodeDecodeError:
            continue
        if "\x00" not in text:
            break
    if not text:
        text = raw.decode("utf-8", errors="replace")
    return text.lstrip("\ufeff")


def rows_from_text(text: str) -> Tuple[List[Dict[str, str]], Optional[str]]:
    try:
        reader = csv.reader(io.StringIO(text))
        raw_rows = [row for row in reader if any(cell.strip() for cell in row)]
    except csv.Error as exc:
        return ([], f"CSV parse error: {exc}")

    if not raw_rows:
        return ([], None)

    has_header = raw_rows[0] == COLUMNS
    data_rows = raw_rows[1:] if has_header else raw_rows

    rows: List[Dict[str, str]] = []
    for raw in data_rows:
        row = {name: "" for name in COLUMNS}
        for idx, name in enumerate(COLUMNS):
            if idx < len(raw):
                row[name] = raw[idx].strip()
        rows.append(row)
    return (rows, None)


def read_rows(path: str) -> Tuple[List[Dict[str, str]], Optional[str]]:
    if not os.path.exists(path):
        return ([], f"CSV not found: {path}")

    try:
        with open(path, "rb") as handle:
            raw = handle.read()
    except OSError as exc:
        return ([], f"Failed to read CSV: {exc}")

    text = decode_text(raw)
    return rows_from_text(text)


def fmt_num(value: Optional[float], digits: int = 3) -> str:
    if value is None:
        return "na"
    return f"{value:.{digits}f}"


def mean_median(rows: List[Dict[str, str]], key: str) -> Tuple[Optional[float], Optional[float]]:
    vals = [v for v in (parse_float(row.get(key)) for row in rows) if v is not None]
    if not vals:
        return (None, None)
    return (statistics.mean(vals), statistics.median(vals))


def print_frequency(counter: Counter, title: str) -> None:
    print(title)
    if not counter:
        print("  (none)")
        return
    for key, count in counter.most_common():
        label = key if key else "(missing)"
        print(f"  {label}: {count}")


def print_class_percentages(rows: List[Dict[str, str]]) -> None:
    total = len(rows)
    counts = Counter(normalize_class(row.get("best_probe_class")) for row in rows)
    print("best_probe_class percentages")
    for cls in TARGET_CLASSES:
        count = counts.get(cls, 0)
        pct = (100.0 * count / total) if total else 0.0
        print(f"  {cls}: {pct:.1f}% ({count}/{total})")


def print_summary(rows: List[Dict[str, str]], label: str) -> None:
    print(f"=== {label} ===")
    print(f"total_rows: {len(rows)}")

    probe_counter = Counter((row.get("best_probe") or "").strip() for row in rows)
    class_counter = Counter(normalize_class(row.get("best_probe_class")) for row in rows)
    print_frequency(probe_counter, "winner frequency (best_probe)")
    print_frequency(class_counter, "winner frequency (best_probe_class)")
    print_class_percentages(rows)

    mean_geom, med_geom = mean_median(rows, "best_geomPix")
    mean_p2, med_p2 = mean_median(rows, "best_max_p2SampRaw")
    mean_ray, med_ray = mean_median(rows, "best_max_geomRayTestsTotalRaw")
    print("metric summary")
    print(f"  best_geomPix mean={fmt_num(mean_geom)} median={fmt_num(med_geom)}")
    print(f"  best_max_p2SampRaw mean={fmt_num(mean_p2)} median={fmt_num(med_p2)}")
    print(
        f"  best_max_geomRayTestsTotalRaw mean={fmt_num(mean_ray)} median={fmt_num(med_ray)}"
    )

    near_trusted_deficits = []
    for row in rows:
        if normalize_class(row.get("best_probe_class")) != "near_trusted":
            continue
        val = parse_float(row.get("best_raytest_deficit"))
        if val is not None:
            near_trusted_deficits.append(val)
    mean_near = statistics.mean(near_trusted_deficits) if near_trusted_deficits else None
    print(f"mean best_raytest_deficit for near_trusted: {fmt_num(mean_near)}")
    print("")


def run_selftest() -> int:
    sample_log = """
[SmartScale][ProbeResult] probe=trusted trust=1 probe_class=trusted geomPix=11600 max_p2SampRaw=120 max_geomRayTestsTotalRaw=460 raytest_deficit=0
[SmartScale][ProbeResult] probe=near_trusted trust=0 probe_class=near_trusted geomPix=10800 max_p2SampRaw=112 max_geomRayTestsTotalRaw=500 raytest_deficit=5
[SmartScale][Summary] best=trusted best_geomPix=11600 best_trust=1 budget_mode=renderstep_calls budget_n=60 budgetStops=0
[SmartScaleResult] {"fixture":"Straight","camera_signature":"camA","goal":"max_hits","budget_mode":"renderstep_calls","budget_n":60,"best_probe":"trusted","best_trust":1,"best_probe_class":"trusted","best_geomPix":11600,"final_target_ms_per_frame":16.6,"final_effective_max_ms":16.6,"final_rows":72,"final_stride":1,"renderstep_calls":60,"rows_advanced_total":2048,"bands_committed":17,"scanline_counters_coarse":true}
""".strip().splitlines()

    result: Dict[str, Any] = {}
    probe: Dict[str, str] = {}
    for line in sample_log:
        if SMARTSCALE_PROBE_TAG in line and not probe:
            probe = parse_kv_tokens(line)
        match = SMARTSCALE_RESULT_RE.search(line.strip())
        if not match:
            continue
        try:
            parsed = json.loads(match.group(1).strip())
        except json.JSONDecodeError:
            continue
        if isinstance(parsed, dict):
            result = parsed

    row = {name: "" for name in COLUMNS}
    row["timestamp"] = "2026-02-27T12:00:00"
    row["log_path"] = "selftest.log"
    row["fixture"] = str(result.get("fixture", ""))
    row["camera_signature"] = str(result.get("camera_signature", ""))
    row["goal"] = str(result.get("goal", ""))
    row["budget_mode"] = str(result.get("budget_mode", ""))
    row["budget_n"] = str(result.get("budget_n", ""))
    row["best_probe"] = str(result.get("best_probe", probe.get("probe", "")))
    row["best_trust"] = str(result.get("best_trust", probe.get("trust", "")))
    row["best_probe_class"] = str(result.get("best_probe_class", probe.get("probe_class", "")))
    row["best_geomPix"] = str(result.get("best_geomPix", probe.get("geomPix", "")))
    row["best_raytest_deficit"] = probe.get("raytest_deficit", "")
    row["best_max_geomRayTestsTotalRaw"] = probe.get("max_geomRayTestsTotalRaw", "")
    row["best_max_p2SampRaw"] = probe.get("max_p2SampRaw", "")

    buf = io.StringIO()
    writer = csv.writer(buf, lineterminator="\n")
    writer.writerow(COLUMNS)
    writer.writerow([row[col] for col in COLUMNS])

    parsed_rows, error = rows_from_text(buf.getvalue())
    if error:
        print(f"SELFTEST FAIL: {error}")
        return 1
    if len(parsed_rows) != 1:
        print("SELFTEST FAIL: expected 1 row")
        return 1
    parsed = parsed_rows[0]
    checks = [
        parsed.get("best_probe") == "trusted",
        normalize_class(parsed.get("best_probe_class")) == "trusted",
        parse_float(parsed.get("best_geomPix")) == 11600.0,
    ]
    if not all(checks):
        print("SELFTEST FAIL: parsed fields mismatch")
        return 1
    print("SELFTEST PASS")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Quick analytics for SmartScale extractor CSV output.")
    parser.add_argument("--selftest", default="0", help="set to 1 to run internal parser self-test")
    parser.add_argument("csv_path", nargs="?", help="path to extractor CSV")
    args = parser.parse_args()

    run_selftest_mode = str(args.selftest).strip().lower() in ("1", "true", "yes", "on")
    if run_selftest_mode:
        return run_selftest()

    if not args.csv_path:
        print("Missing csv_path.")
        return 2

    rows, error = read_rows(args.csv_path)
    if error:
        print(error)
        return 1
    if not rows:
        print("No rows found.")
        return 0

    print_summary(rows, "Overall")

    fixtures = sorted({(row.get("fixture") or "").strip() for row in rows if (row.get("fixture") or "").strip()})
    if len(fixtures) > 1:
        for fixture in fixtures:
            grouped = [row for row in rows if (row.get("fixture") or "").strip() == fixture]
            print_summary(grouped, f"Fixture: {fixture}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
