#!/usr/bin/env python3
import argparse
import csv
import re
import sys
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Tuple


TOKEN_RE = re.compile(r"([A-Za-z0-9_]+)=([^\s]+)")


def parse_num(value: Optional[str]) -> Optional[float]:
    if value is None:
        return None
    v = value.strip().lower()
    if v in ("na", "nan", ""):
        return None
    try:
        return float(v)
    except ValueError:
        return None


@dataclass
class MetricAgg:
    total: float = 0.0
    count: int = 0

    def add(self, val: Optional[float]) -> None:
        if val is None:
            return
        self.total += val
        self.count += 1

    def mean(self) -> Optional[float]:
        if self.count <= 0:
            return None
        return self.total / self.count


@dataclass
class ModeAgg:
    windows: int = 0
    trusted_windows: int = 0
    partial_windows: int = 0
    ray_on: MetricAgg = field(default_factory=MetricAgg)
    ray_off: MetricAgg = field(default_factory=MetricAgg)
    saved_pct: MetricAgg = field(default_factory=MetricAgg)
    cand_avg: MetricAgg = field(default_factory=MetricAgg)
    seg_zero_rate: MetricAgg = field(default_factory=MetricAgg)
    pix_nocand_rate: MetricAgg = field(default_factory=MetricAgg)
    audit_false_neg_total: int = 0
    audit_false_pos_total: int = 0


def fmt_mean(val: Optional[float], digits: int = 3) -> str:
    if val is None:
        return "na"
    return f"{val:.{digits}f}"


def sanitize_csv_value(value: Optional[str]) -> str:
    if value is None:
        return ""
    v = value.strip()
    if v.lower() in ("na", "nan", ""):
        return ""
    return v


def is_trusted_window(data: Dict[str, str]) -> bool:
    trusted = parse_num(data.get("geomTrusted"))
    if trusted is not None:
        return int(trusted) == 1
    partial = parse_num(data.get("geomHealthPartial"))
    if partial is not None:
        return int(partial) != 1
    return True


def parse_renderhealth_file(path: str, require_trusted: bool) -> Tuple[Dict[str, ModeAgg], List[Dict[str, str]]]:
    groups: Dict[str, ModeAgg] = {"on": ModeAgg(), "off": ModeAgg()}
    rows: List[Dict[str, str]] = []
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        for raw in f:
            line = raw.strip()
            if not line.startswith("[RenderHealth]"):
                continue
            data = {k: v for k, v in TOKEN_RE.findall(line)}
            mode = data.get("geomPrune")
            if mode not in groups:
                continue

            trusted = is_trusted_window(data)
            g = groups[mode]
            g.windows += 1
            if trusted:
                g.trusted_windows += 1
            else:
                g.partial_windows += 1

            rows.append(
                {
                    "step": sanitize_csv_value(data.get("step")),
                    "geomPrune": sanitize_csv_value(data.get("geomPrune")),
                    "geomTrusted": "1" if trusted else "0",
                    "geomHealthPartial": sanitize_csv_value(data.get("geomHealthPartial")),
                    "geomPruneSwitched": sanitize_csv_value(data.get("geomPruneSwitched")),
                    "geomTrustReason": sanitize_csv_value(data.get("geomTrustReason")),
                    "geomRayTestsPerPxOn": sanitize_csv_value(data.get("geomRayTestsPerPxOn")),
                    "geomRayTestsPerPxOff": sanitize_csv_value(data.get("geomRayTestsPerPxOff")),
                    "geomRayTestsSavedPct": sanitize_csv_value(data.get("geomRayTestsSavedPct")),
                    "geomCandAvg": sanitize_csv_value(data.get("geomCandAvg")),
                    "cand0": sanitize_csv_value(data.get("cand0")),
                    "cand1to2": sanitize_csv_value(data.get("cand1to2")),
                    "cand3to8": sanitize_csv_value(data.get("cand3to8")),
                    "cand9to32": sanitize_csv_value(data.get("cand9to32")),
                    "cand33p": sanitize_csv_value(data.get("cand33p")),
                    "geomSegZeroRatePct": sanitize_csv_value(data.get("geomSegZeroRatePct")),
                    "geomPixNoCandRatePct": sanitize_csv_value(data.get("geomPixNoCandRatePct")),
                }
            )

            if require_trusted and not trusted:
                continue

            g.ray_on.add(parse_num(data.get("geomRayTestsPerPxOn")))
            g.ray_off.add(parse_num(data.get("geomRayTestsPerPxOff")))
            g.saved_pct.add(parse_num(data.get("geomRayTestsSavedPct")))
            g.cand_avg.add(parse_num(data.get("geomCandAvg")))
            g.seg_zero_rate.add(parse_num(data.get("geomSegZeroRatePct")))
            g.pix_nocand_rate.add(parse_num(data.get("geomPixNoCandRatePct")))

            fn = parse_num(data.get("geomPruneAuditFalseNeg"))
            fp = parse_num(data.get("geomPruneAuditFalsePos"))
            g.audit_false_neg_total += int(fn) if fn is not None else 0
            g.audit_false_pos_total += int(fp) if fp is not None else 0
    return groups, rows


def print_summary(groups: Dict[str, ModeAgg], require_trusted: bool) -> None:
    scope = "trusted windows only" if require_trusted else "all windows"
    print(f"RenderHealth summary ({scope})")
    header = (
        "mode  windows trusted partial "
        "rayPerPxOn rayPerPxOff savedPct candAvg segZeroPct pixNoCandPct auditFalseNeg auditFalsePos"
    )
    print(header)
    for mode in ("off", "on"):
        g = groups[mode]
        row = (
            f"{mode:<4} "
            f"{g.windows:<7} "
            f"{g.trusted_windows:<7} "
            f"{g.partial_windows:<7} "
            f"{fmt_mean(g.ray_on.mean()):<10} "
            f"{fmt_mean(g.ray_off.mean()):<11} "
            f"{fmt_mean(g.saved_pct.mean(), 2):<8} "
            f"{fmt_mean(g.cand_avg.mean()):<7} "
            f"{fmt_mean(g.seg_zero_rate.mean(), 2):<10} "
            f"{fmt_mean(g.pix_nocand_rate.mean(), 2):<12} "
            f"{g.audit_false_neg_total:<13} "
            f"{g.audit_false_pos_total:<13}"
        )
        print(row)
    print("")
    print("B.3 mode means (trusted semantics)")
    print(
        "off: "
        f"windows={groups['off'].windows} "
        f"trusted={groups['off'].trusted_windows} "
        f"partial={groups['off'].partial_windows} "
        f"meanGeomRayTestsPerPxOff={fmt_mean(groups['off'].ray_off.mean())}"
    )
    print(
        "on:  "
        f"windows={groups['on'].windows} "
        f"trusted={groups['on'].trusted_windows} "
        f"partial={groups['on'].partial_windows} "
        f"meanGeomRayTestsPerPxOn={fmt_mean(groups['on'].ray_on.mean())} "
        f"meanGeomRayTestsSavedPct={fmt_mean(groups['on'].saved_pct.mean(), 2)}"
    )


def write_csv(path: str, rows: List[Dict[str, str]]) -> None:
    fieldnames = [
        "step",
        "geomPrune",
        "geomTrusted",
        "geomHealthPartial",
        "geomPruneSwitched",
        "geomTrustReason",
        "geomRayTestsPerPxOn",
        "geomRayTestsPerPxOff",
        "geomRayTestsSavedPct",
        "geomCandAvg",
        "cand0",
        "cand1to2",
        "cand3to8",
        "cand9to32",
        "cand33p",
        "geomSegZeroRatePct",
        "geomPixNoCandRatePct",
    ]
    with open(path, "w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow(row)


def main() -> int:
    parser = argparse.ArgumentParser(description="Parse [RenderHealth] logs into compact mode summaries.")
    parser.add_argument("log_path", help="Path to a text log file containing [RenderHealth] lines.")
    parser.add_argument(
        "--require-trusted",
        dest="require_trusted",
        action="store_true",
        default=True,
        help="Exclude untrusted/partial windows from means (default: enabled).",
    )
    parser.add_argument(
        "--no-require-trusted",
        dest="require_trusted",
        action="store_false",
        help="Include untrusted/partial windows in means.",
    )
    parser.add_argument(
        "--csv",
        dest="csv_path",
        default="",
        help="Optional CSV output path (one row per [RenderHealth] line).",
    )
    args = parser.parse_args()

    try:
        groups, rows = parse_renderhealth_file(args.log_path, args.require_trusted)
        print_summary(groups, args.require_trusted)
        if args.csv_path:
            write_csv(args.csv_path, rows)
            print(f"csv_written={args.csv_path} rows={len(rows)}")
        return 0
    except FileNotFoundError:
        print(f"error: file not found: {args.log_path}", file=sys.stderr)
        return 2
    except Exception as ex:
        print(f"error: failed to parse log: {ex}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
