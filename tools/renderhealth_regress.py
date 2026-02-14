#!/usr/bin/env python3
import argparse
import os
import sys
from dataclasses import dataclass, field
from typing import Dict, List, Optional

from renderhealth_parse import is_na_token, is_trusted_window, load_renderhealth_entries, parse_num


RAY_FIELDS = ("geomRayTestsPerPxOn", "geomRayTestsPerPxOff", "geomRayTestsSavedPct")
CAND_BUCKET_FIELDS = ("cand0", "cand1to2", "cand3to8", "cand9to32", "cand33p")


def to_int(value: Optional[str], default: int = 0) -> int:
    v = parse_num(value)
    if v is None:
        return default
    return int(v)


def is_present_number(value: Optional[str]) -> bool:
    return parse_num(value) is not None


@dataclass
class LogStats:
    windows: int = 0
    trusted: int = 0
    partial: int = 0
    on_per_px_sum: float = 0.0
    on_per_px_count: int = 0
    off_per_px_sum: float = 0.0
    off_per_px_count: int = 0
    on_saved_sum: float = 0.0
    on_saved_count: int = 0
    audit_false_neg_total: int = 0
    audit_false_pos_total: int = 0
    errors: List[str] = field(default_factory=list)
    audit_mismatches: List[str] = field(default_factory=list)


def mean(sum_value: float, count: int) -> str:
    if count <= 0:
        return "na"
    return f"{(sum_value / count):.3f}"


def validate_entry(data: Dict[str, str], log_label: str, line_no: int, stats: LogStats) -> None:
    mode = (data.get("geomPrune") or "").strip().lower()
    if mode not in ("on", "off"):
        return

    trusted = is_trusted_window(data)
    switched = to_int(data.get("geomPruneSwitched")) == 1
    partial = to_int(data.get("geomHealthPartial")) == 1

    stats.windows += 1
    if trusted:
        stats.trusted += 1
    if partial:
        stats.partial += 1

    for_audit = mode == "on" and trusted

    if switched or (not trusted) or partial:
        for key in RAY_FIELDS:
            if not is_na_token(data.get(key)):
                stats.errors.append(
                    f"{log_label}:{line_no} expected {key}=na for switched/untrusted/partial window"
                )

    if mode == "off" and trusted:
        if not is_present_number(data.get("geomRayTestsPerPxOff")):
            stats.errors.append(f"{log_label}:{line_no} expected geomRayTestsPerPxOff to be present for trusted off")
        elif parse_num(data.get("geomRayTestsPerPxOff")) < 0:
            stats.errors.append(f"{log_label}:{line_no} expected geomRayTestsPerPxOff >= 0")

        if not is_na_token(data.get("geomCandAvg")):
            stats.errors.append(f"{log_label}:{line_no} expected geomCandAvg=na for trusted off")
        for bucket in CAND_BUCKET_FIELDS:
            if not is_na_token(data.get(bucket)):
                stats.errors.append(f"{log_label}:{line_no} expected {bucket}=na for trusted off")

    if mode == "on" and trusted:
        if not is_present_number(data.get("geomCandAvg")):
            stats.errors.append(f"{log_label}:{line_no} expected geomCandAvg to be present for trusted on")
        if not is_present_number(data.get("geomSegQueried")):
            stats.errors.append(f"{log_label}:{line_no} expected geomSegQueried to be present for trusted on")
        if not is_present_number(data.get("geomPixProcessed")):
            stats.errors.append(f"{log_label}:{line_no} expected geomPixProcessed to be present for trusted on")

    if mode == "on" and trusted:
        on = parse_num(data.get("geomRayTestsPerPxOn"))
        if on is not None:
            stats.on_per_px_sum += on
            stats.on_per_px_count += 1
        saved = parse_num(data.get("geomRayTestsSavedPct"))
        if saved is not None:
            stats.on_saved_sum += saved
            stats.on_saved_count += 1

    if mode == "off" and trusted:
        off = parse_num(data.get("geomRayTestsPerPxOff"))
        if off is not None:
            stats.off_per_px_sum += off
            stats.off_per_px_count += 1

    if for_audit:
        fn = parse_num(data.get("geomPruneAuditFalseNeg"))
        fp = parse_num(data.get("geomPruneAuditFalsePos"))
        fn_i = int(fn) if fn is not None else 0
        fp_i = int(fp) if fp is not None else 0
        stats.audit_false_neg_total += fn_i
        stats.audit_false_pos_total += fp_i
        if fn_i != 0 or fp_i != 0:
            stats.audit_mismatches.append(
                f"{log_label}:{line_no} geomPruneAuditFalseNeg={fn_i} geomPruneAuditFalsePos={fp_i}"
            )


def check_log(path: str) -> LogStats:
    stats = LogStats()
    entries = load_renderhealth_entries(path)
    for i, data in enumerate(entries, start=1):
        validate_entry(data, path, i, stats)
    return stats


def print_report(path: str, stats: LogStats, allow_audit_mismatch: bool) -> bool:
    failed = bool(stats.errors)
    if stats.audit_mismatches and not allow_audit_mismatch:
        failed = True

    status = "PASS" if not failed else "FAIL"
    print(f"{status}: {path} windows={stats.windows}, trusted={stats.trusted}, partial={stats.partial}")
    print(f"ON trusted mean per-px-on={mean(stats.on_per_px_sum, stats.on_per_px_count)}")
    print(f"OFF trusted mean per-px-off={mean(stats.off_per_px_sum, stats.off_per_px_count)}")
    print(f"ON saved% mean (baseline-ready)={mean(stats.on_saved_sum, stats.on_saved_count)}")
    print(f"Audit totals: falseNeg={stats.audit_false_neg_total} falsePos={stats.audit_false_pos_total}")

    if stats.errors:
        print("Invariant failures:")
        for msg in stats.errors:
            print(f"  - {msg}")

    if stats.audit_mismatches:
        print("AUDIT MISMATCHES:")
        for msg in stats.audit_mismatches:
            print(f"  - {msg}")
        if not allow_audit_mismatch:
            print("Set --allow-audit-mismatch to bypass this failure mode.")

    print("")
    return not failed


def main() -> int:
    parser = argparse.ArgumentParser(description="Regression checks for [RenderHealth] pruning/trust invariants.")
    parser.add_argument("log_paths", nargs="+", help="One or more log files.")
    parser.add_argument(
        "--allow-audit-mismatch",
        action="store_true",
        help="Allow non-zero prune audit false-neg/false-pos in trusted prune-on windows.",
    )
    args = parser.parse_args()

    all_ok = True
    for log_path in args.log_paths:
        if not os.path.exists(log_path):
            print(f"FAIL: {log_path} (file not found)", file=sys.stderr)
            all_ok = False
            continue
        stats = check_log(log_path)
        if not print_report(log_path, stats, args.allow_audit_mismatch):
            all_ok = False

    return 0 if all_ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
