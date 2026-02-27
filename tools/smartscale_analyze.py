#!/usr/bin/env python3
import argparse
import csv
import io
import os
import statistics
from collections import Counter
from typing import Dict, List, Optional


COLUMNS = [
    "timestamp",
    "log_path",
    "fixture",
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


def read_rows(path: str) -> List[Dict[str, str]]:
    if not os.path.exists(path):
        return []

    try:
        with open(path, "rb") as handle:
            raw = handle.read()
    except OSError:
        return []

    if not raw:
        return []

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
    text = text.lstrip("\ufeff")

    reader = csv.reader(io.StringIO(text))
    raw_rows = [row for row in reader if any(cell.strip() for cell in row)]

    if not raw_rows:
        return []

    has_header = raw_rows[0] == COLUMNS
    data_rows = raw_rows[1:] if has_header else raw_rows

    rows: List[Dict[str, str]] = []
    for raw in data_rows:
        row = {name: "" for name in COLUMNS}
        for idx, name in enumerate(COLUMNS):
            if idx < len(raw):
                row[name] = raw[idx].strip()
        rows.append(row)
    return rows


def fmt_num(value: Optional[float], digits: int = 3) -> str:
    if value is None:
        return "na"
    return f"{value:.{digits}f}"


def mean_median(rows: List[Dict[str, str]], key: str) -> (Optional[float], Optional[float]):
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


def print_summary(rows: List[Dict[str, str],], label: str) -> None:
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


def main() -> int:
    parser = argparse.ArgumentParser(description="Quick analytics for SmartScale extractor CSV output.")
    parser.add_argument("csv_path", help="path to extractor CSV")
    args = parser.parse_args()

    rows = read_rows(args.csv_path)
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
