#!/usr/bin/env python3
import argparse
import os
import sys
from dataclasses import dataclass
from typing import Dict, List, Optional, Sequence

from renderhealth_parse import is_na_token, is_trusted_window, load_entries, parse_num


RAY_FIELDS = ("geomRayTestsPerPxOn", "geomRayTestsPerPxOff", "geomRayTestsSavedPct")
CAND_BUCKET_FIELDS = ("cand0", "cand1to2", "cand3to8", "cand9to32", "cand33p")
PRUNE_ON_ONLY_FIELDS = (
    "geomCandAvg",
    "geomSegQueried",
    "geomSegWithCandidates",
    "geomSegZero",
    "geomSegZeroRatePct",
    "geomPixProcessed",
    "geomPixHadAnyCandidates",
    "geomPixNoCand",
    "geomPixNoCandRatePct",
    "geomPruneAuditSamp",
    "geomPruneAuditFalseNeg",
    "geomPruneAuditFalsePos",
    "geomPruneAuditCand0Hit",
    "geomPruneAuditFalseNegRate",
    *CAND_BUCKET_FIELDS,
)


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
    pass_count: int = 0


def mean(sum_value: float, count: int) -> str:
    if count <= 0:
        return "na"
    return f"{(sum_value / count):.3f}"


class InvariantFailure(RuntimeError):
    pass


def _ctx(data: Dict[str, str], index: int) -> str:
    return (
        f"entry={index} step={data.get('step', '?')} geomPrune={data.get('geomPrune', '?')} "
        f"geomPruneSwitched={data.get('geomPruneSwitched', '?')} "
        f"geomTrusted={data.get('geomTrusted', '?')} "
        f"geomHealthPartial={data.get('geomHealthPartial', '?')} "
        f"geomTrustReason={data.get('geomTrustReason', '?')}"
    )


def require(condition: bool, path: str, index: int, data: Dict[str, str], message: str) -> None:
    if condition:
        return
    raise InvariantFailure(f"{path}: {message}; {_ctx(data, index)}")


def _fields_are_na(data: Dict[str, str], fields: Sequence[str], path: str, index: int, reason: str) -> None:
    for key in fields:
        require(is_na_token(data.get(key)), path, index, data, f"{reason}: expected {key}=NA")


def _fields_are_numeric(data: Dict[str, str], fields: Sequence[str], path: str, index: int, reason: str) -> None:
    for key in fields:
        require(is_present_number(data.get(key)), path, index, data, f"{reason}: expected {key} numeric")


def validate_entry(
    data: Dict[str, str],
    path: str,
    index: int,
    stats: LogStats,
    allow_audit_mismatch: bool,
) -> None:
    mode = (data.get("geomPrune") or "").strip().lower()
    if mode not in ("on", "off"):
        return

    trusted = is_trusted_window(data)
    switched = to_int(data.get("geomPruneSwitched")) == 1
    partial = to_int(data.get("geomHealthPartial")) == 1
    geom_trusted_token = parse_num(data.get("geomTrusted"))

    stats.windows += 1
    if trusted:
        stats.trusted += 1
    if partial:
        stats.partial += 1

    # A) Any switch window must be untrusted/partial and hide gated/prune-on metrics.
    if switched:
        require(
            (geom_trusted_token == 0.0) or partial,
            path,
            index,
            data,
            "switch window requires geomTrusted=0 or geomHealthPartial=1",
        )
        _fields_are_na(data, RAY_FIELDS, path, index, "switch window")
        _fields_are_na(data, PRUNE_ON_ONLY_FIELDS, path, index, "switch window")

    # B) prune=off trusted windows: no candidate metrics, off baseline must be present.
    if mode == "off" and trusted:
        _fields_are_na(data, ("geomCandAvg", *CAND_BUCKET_FIELDS), path, index, "trusted prune=off")
        _fields_are_na(
            data,
            (
                "geomSegQueried",
                "geomSegWithCandidates",
                "geomSegZero",
                "geomSegZeroRatePct",
                "geomPixProcessed",
                "geomPixHadAnyCandidates",
                "geomPixNoCand",
                "geomPixNoCandRatePct",
                "geomPruneAuditSamp",
                "geomPruneAuditFalseNeg",
                "geomPruneAuditFalsePos",
                "geomPruneAuditCand0Hit",
                "geomPruneAuditFalseNegRate",
            ),
            path,
            index,
            "trusted prune=off",
        )
        require(
            is_present_number(data.get("geomRayTestsPerPxOff")),
            path,
            index,
            data,
            "trusted prune=off requires geomRayTestsPerPxOff numeric",
        )
        off_val = parse_num(data.get("geomRayTestsPerPxOff"))
        require(off_val is not None and off_val >= 0.0, path, index, data, "geomRayTestsPerPxOff must be >= 0")

    # C) prune=on trusted windows: candidate metrics present; saved% only with valid OFF baseline.
    if mode == "on" and trusted:
        _fields_are_numeric(data, ("geomCandAvg", *CAND_BUCKET_FIELDS), path, index, "trusted prune=on")
        require(
            is_present_number(data.get("geomRayTestsPerPxOn")),
            path,
            index,
            data,
            "trusted prune=on requires geomRayTestsPerPxOn numeric",
        )
        off_baseline = parse_num(data.get("geomRayTestsPerPxOff"))
        saved = parse_num(data.get("geomRayTestsSavedPct"))
        baseline_learned = off_baseline is not None and off_baseline > 0.0
        if baseline_learned:
            require(
                saved is not None,
                path,
                index,
                data,
                "saved% expected numeric when trusted prune=on and OFF baseline > 0",
            )
        else:
            require(
                is_na_token(data.get("geomRayTestsSavedPct")),
                path,
                index,
                data,
                "saved% must be NA until OFF baseline > 0 is learned",
            )

        on = parse_num(data.get("geomRayTestsPerPxOn"))
        if on is not None:
            stats.on_per_px_sum += on
            stats.on_per_px_count += 1
        if saved is not None:
            stats.on_saved_sum += saved
            stats.on_saved_count += 1

    if mode == "off" and trusted:
        off = parse_num(data.get("geomRayTestsPerPxOff"))
        if off is not None:
            stats.off_per_px_sum += off
            stats.off_per_px_count += 1

    # D) trusted prune=on audit totals must remain zero unless explicitly allowed.
    if mode == "on" and trusted:
        fn = parse_num(data.get("geomPruneAuditFalseNeg"))
        fp = parse_num(data.get("geomPruneAuditFalsePos"))
        fn_i = int(fn) if fn is not None else 0
        fp_i = int(fp) if fp is not None else 0
        stats.audit_false_neg_total += fn_i
        stats.audit_false_pos_total += fp_i
        if (fn_i != 0 or fp_i != 0) and not allow_audit_mismatch:
            raise InvariantFailure(
                f"{path}: audit mismatch without --allow-audit-mismatch; "
                f"geomPruneAuditFalseNeg={fn_i} geomPruneAuditFalsePos={fp_i}; {_ctx(data, index)}"
            )

    stats.pass_count += 1

def check_log(path: str, allow_audit_mismatch: bool) -> LogStats:
    stats = LogStats()
    entries = load_entries(path)
    for i, data in enumerate(entries, start=1):
        validate_entry(data, path, i, stats, allow_audit_mismatch)
    return stats


def print_report(path: str, stats: LogStats) -> None:
    print(f"PASS: {path} windows={stats.windows}, trusted={stats.trusted}, partial={stats.partial}")
    print(f"ON trusted mean per-px-on={mean(stats.on_per_px_sum, stats.on_per_px_count)}")
    print(f"OFF trusted mean per-px-off={mean(stats.off_per_px_sum, stats.off_per_px_count)}")
    print(f"ON saved% mean (baseline-ready)={mean(stats.on_saved_sum, stats.on_saved_count)}")
    print(f"Audit totals: falseNeg={stats.audit_false_neg_total} falsePos={stats.audit_false_pos_total}")
    print("")


def main() -> int:
    parser = argparse.ArgumentParser(description="Regression checks for [RenderHealth] pruning/trust invariants.")
    parser.add_argument("log_paths", nargs="+", help="One or more log files.")
    parser.add_argument(
        "--allow-audit-mismatch",
        action="store_true",
        help="Allow non-zero prune audit false-neg/false-pos in trusted prune-on windows.",
    )
    args = parser.parse_args()

    pass_logs = 0
    for log_path in args.log_paths:
        if not os.path.exists(log_path):
            print(f"FAIL: {log_path} (file not found)")
            return 1
        try:
            stats = check_log(log_path, args.allow_audit_mismatch)
            print_report(log_path, stats)
            pass_logs += 1
        except InvariantFailure as ex:
            print(f"FAIL: {ex}")
            print(f"summary: pass={pass_logs} fail=1")
            return 1
        except Exception as ex:
            print(f"FAIL: {log_path}: unexpected error: {ex}")
            print(f"summary: pass={pass_logs} fail=1")
            return 1

    print(f"summary: pass={pass_logs} fail=0")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
