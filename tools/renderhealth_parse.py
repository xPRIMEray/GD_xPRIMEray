#!/usr/bin/env python3
import argparse
import re
import sys
from dataclasses import dataclass, field
from typing import Dict, Optional


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


def parse_renderhealth_file(path: str, include_partial: bool) -> Dict[str, ModeAgg]:
    groups: Dict[str, ModeAgg] = {"on": ModeAgg(), "off": ModeAgg()}
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        for raw in f:
            line = raw.strip()
            if not line.startswith("[RenderHealth]"):
                continue
            data = {k: v for k, v in TOKEN_RE.findall(line)}
            mode = data.get("geomPrune")
            if mode not in groups:
                continue

            g = groups[mode]
            partial = int(parse_num(data.get("geomHealthPartial")) or 0) == 1
            g.windows += 1
            if partial:
                g.partial_windows += 1
            if partial and not include_partial:
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
    return groups


def print_summary(groups: Dict[str, ModeAgg], include_partial: bool) -> None:
    scope = "all windows" if include_partial else "excluding partial windows"
    print(f"RenderHealth summary ({scope})")
    header = (
        "mode  windows partial "
        "rayPerPxOn rayPerPxOff savedPct candAvg segZeroPct pixNoCandPct auditFalseNeg auditFalsePos"
    )
    print(header)
    for mode in ("off", "on"):
        g = groups[mode]
        row = (
            f"{mode:<4} "
            f"{g.windows:<7} "
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


def main() -> int:
    parser = argparse.ArgumentParser(description="Parse [RenderHealth] logs into compact mode summaries.")
    parser.add_argument("log_path", help="Path to a text log file containing [RenderHealth] lines.")
    parser.add_argument(
        "--include-partial",
        action="store_true",
        help="Include geomHealthPartial=1 windows (default excludes them).",
    )
    args = parser.parse_args()

    try:
        groups = parse_renderhealth_file(args.log_path, args.include_partial)
        print_summary(groups, args.include_partial)
        return 0
    except FileNotFoundError:
        print(f"error: file not found: {args.log_path}", file=sys.stderr)
        return 2
    except Exception as ex:
        print(f"error: failed to parse log: {ex}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
