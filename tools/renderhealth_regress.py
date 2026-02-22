#!/usr/bin/env python3
import argparse
import csv
import json
import os
import sys
from collections import Counter
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional, Sequence

from renderhealth_parse import is_na_token, is_trusted_window, parse_num


RAY_FIELDS = ("geomRayTestsPerPxOn", "geomRayTestsPerPxOff", "geomRayTestsSavedPct")
CAND_BUCKET_FIELDS = ("cand0", "cand1to2", "cand3to8", "cand9to32", "cand33p")
PRUNING_ONLY_COUNTERS = ("geomPixProcessed", "geomPixNoCand", "geomPixHadAnyCandidates")
COUNT_FIELDS_NONNEG = (
    "rowsAdv",
    "bands",
    "qray0",
    "hybridFallback",
    "hybridFallbackHit",
    "hybridFallbackMiss",
    "noCandidates",
    "geomSegQueried",
    "geomSegWithCandidates",
    "geomSegZero",
    "geomPixProcessed",
    "geomPixHadAnyCandidates",
    "geomPixNoCand",
    "geomHitOk",
    "geomHitReject",
    "geomRayTestsTotal",
    "geomRayTestsAccepted",
    "geomRayTestsRejected",
    "p2Samp",
    "cand0",
    "cand1to2",
    "cand3to8",
    "cand9to32",
    "cand33p",
    "geomPruneAuditSamp",
    "geomPruneAuditFalseNeg",
    "geomPruneAuditFalsePos",
    "geomPruneAuditCand0Hit",
)
RATIO_FIELDS_01 = ("hitRate", "geomPruneAuditFalseNegRate")
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
TRUST_GATED_FIELDS = (
    *RAY_FIELDS,
    *PRUNE_ON_ONLY_FIELDS,
    "geomHitOk",
    "geomHitReject",
    "geomRayTestsTotal",
    "geomRayTestsAccepted",
    "geomRayTestsRejected",
)
UNTRUSTED_REASONS = ("mode_switch", "low_mode_samples", "low_p2samp")
CANONICAL_RENDERHEALTH_PREFIX = "[RenderHealth] "
NONCANONICAL_RENDERHEALTH_DEBUG_PREFIX = "[RenderHealth]["
TRUST_GATE_DEBUG_PREFIX = "[RenderHealth][TrustGateDebug]"
GEOM_COVERAGE_PREFIX = "[RenderHealth][GeomCoverage]"
TRUST_CFG_PREFIX = "[RenderHealth][TrustCfg]"
EFFECTIVE_CFG_PREFIX = "[EffectiveCfg]"
RUN_START_PREFIX = "[RenderTest][RUN START]"
RUN_END_PREFIX = "[RenderTest][RUN END]"
RUN_SUMMARY_PREFIX = "[RenderTest][RUN SUMMARY]"
TRUST_ATTACH_STEP_DELTA = 128


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


@dataclass
class WindowSnapshot:
    step: Optional[int]
    renderhealth: Dict[str, str]
    trust_debug: Dict[str, str] = field(default_factory=dict)
    geom_coverage: Dict[str, str] = field(default_factory=dict)
    trust_cfg: Dict[str, str] = field(default_factory=dict)


@dataclass
class RunAggregate:
    name: str
    log_path: str
    prune_mode: Optional[str] = None
    mean_ms: Optional[float] = None
    p95_ms: Optional[float] = None
    frames: Optional[int] = None
    warmup: Optional[int] = None
    samples: Optional[int] = None
    effective_cfg: Dict[str, str] = field(default_factory=dict)
    latest_trust_cfg: Dict[str, str] = field(default_factory=dict)
    windows: List[WindowSnapshot] = field(default_factory=list)
    _last_window_idx: Optional[int] = None
    _step_to_idx: Dict[int, int] = field(default_factory=dict)

    def add_window(self, data: Dict[str, str]) -> None:
        step_num = parse_num(data.get("step"))
        step = int(step_num) if step_num is not None else None
        snap = WindowSnapshot(step=step, renderhealth=data)
        self.windows.append(snap)
        idx = len(self.windows) - 1
        self._last_window_idx = idx
        if step is not None:
            self._step_to_idx[step] = idx

    def _window_for_step(self, step: Optional[int]) -> Optional[WindowSnapshot]:
        if step is not None and step in self._step_to_idx:
            return self.windows[self._step_to_idx[step]]
        if self._last_window_idx is None:
            return None
        return self.windows[self._last_window_idx]

    def _window_for_attach(self, step: Optional[int], max_back_delta: int = TRUST_ATTACH_STEP_DELTA) -> Optional[WindowSnapshot]:
        if not self.windows:
            return None
        if step is None:
            return self._window_for_step(None)
        exact = self._window_for_step(step)
        if exact is not None and exact.step == step:
            return exact
        best_idx: Optional[int] = None
        best_delta: Optional[int] = None
        for idx in range(len(self.windows) - 1, -1, -1):
            w_step = self.windows[idx].step
            if w_step is None:
                continue
            delta = step - w_step
            if delta < 0:
                continue
            if delta <= max_back_delta and (best_delta is None or delta < best_delta):
                best_delta = delta
                best_idx = idx
                if delta == 0:
                    break
        if best_idx is not None:
            return self.windows[best_idx]
        return self._window_for_step(None)

    def attach_geom_coverage(self, data: Dict[str, str]) -> None:
        step_num = parse_num(data.get("step"))
        step = int(step_num) if step_num is not None else None
        target = self._window_for_step(step)
        if target is not None:
            target.geom_coverage.update(data)

    def attach_trust_debug(self, data: Dict[str, str]) -> None:
        step_num = parse_num(data.get("step"))
        step = int(step_num) if step_num is not None else None
        target = self._window_for_attach(step)
        if target is not None:
            target.trust_debug.update(data)

    def attach_trust_cfg(self, data: Dict[str, str]) -> None:
        self.latest_trust_cfg.update(data)
        step_num = parse_num(data.get("step"))
        step = int(step_num) if step_num is not None else None
        target = self._window_for_attach(step)
        if target is not None:
            target.trust_cfg.update(data)


@dataclass
class DoeSummary:
    runs: List[RunAggregate]
    global_trust_hist: Counter
    per_run_trust_hist: Dict[str, Counter]
    trust_flip_rows: List[Dict[str, Any]]
    low_raytests_occurrences: List[Dict[str, Any]]
    worst_windows: List[Dict[str, Any]]


def mean(sum_value: float, count: int) -> str:
    if count <= 0:
        return "na"
    return f"{(sum_value / count):.3f}"


def mean_num(values: List[float]) -> Optional[float]:
    if not values:
        return None
    return sum(values) / float(len(values))


class InvariantFailure(RuntimeError):
    pass


def parse_tokens(line: str) -> Dict[str, str]:
    data: Dict[str, str] = {}
    for token in line.strip().split():
        if "=" not in token:
            continue
        key, value = token.split("=", 1)
        data[key] = value
    return data


def _norm_mode(value: Optional[str]) -> Optional[str]:
    if value is None:
        return None
    v = value.strip().lower()
    if v in ("on", "off"):
        return v
    if v in ("1", "true"):
        return "on"
    if v in ("0", "false"):
        return "off"
    return None


def _fmt_opt(value: Optional[float], digits: int = 3) -> str:
    if value is None:
        return "na"
    return f"{value:.{digits}f}"


def _value_from_window(window: WindowSnapshot, key: str) -> Optional[float]:
    for src in (window.trust_debug, window.geom_coverage, window.renderhealth):
        val = parse_num(src.get(key))
        if val is not None:
            return val
    return None


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
    trust_reason = (data.get("geomTrustReason") or "").strip().lower()

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
        require(
            trust_reason in ("", "mode_switch"),
            path,
            index,
            data,
            "switch window should report geomTrustReason=mode_switch",
        )
        mode_samples = parse_num(data.get("geomHealthModeSamples"))
        require(
            mode_samples is None or mode_samples <= 1.0,
            path,
            index,
            data,
            "switch window requires mode-window reset (geomHealthModeSamples should be <= 1)",
        )

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

    # C0) prune=off windows must not increment pruning-only counters.
    if mode == "off":
        for key in PRUNING_ONLY_COUNTERS:
            val = parse_num(data.get(key))
            require(
                val is None or val == 0.0,
                path,
                index,
                data,
                f"prune=off must keep {key} at NA/0",
            )

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

    # C2) saved% can only be numeric when OFF baseline exists.
    saved_global = parse_num(data.get("geomRayTestsSavedPct"))
    off_baseline_global = parse_num(data.get("geomRayTestsPerPxOff"))
    if saved_global is not None:
        require(
            off_baseline_global is not None and off_baseline_global > 0.0,
            path,
            index,
            data,
            "geomRayTestsSavedPct requires geomRayTestsPerPxOff baseline > 0",
        )

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

    # E) ratio fields must be in [0,1] when present.
    for key in RATIO_FIELDS_01:
        val = parse_num(data.get(key))
        if val is None:
            continue
        require(0.0 <= val <= 1.0, path, index, data, f"{key} must be in [0,1]")

    # F) count fields must never be negative.
    for key in COUNT_FIELDS_NONNEG:
        val = parse_num(data.get(key))
        if val is None:
            continue
        require(val >= 0.0, path, index, data, f"{key} must be >= 0")

    # G) Explicit untrusted reasons must emit trust-gated fields as NA.
    if trust_reason in UNTRUSTED_REASONS:
        _fields_are_na(data, TRUST_GATED_FIELDS, path, index, f"untrusted reason={trust_reason}")

    stats.pass_count += 1


def load_canonical_entries(path: str) -> List[Dict[str, str]]:
    entries: List[Dict[str, str]] = []
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        for raw_line in f:
            line = raw_line
            # Sanity guard: skip debug/helper channels that intentionally use "[RenderHealth][...".
            if line.startswith(NONCANONICAL_RENDERHEALTH_DEBUG_PREFIX):
                continue
            # Canonical parser: only accept exact "[RenderHealth] " prefix.
            if not line.startswith(CANONICAL_RENDERHEALTH_PREFIX):
                continue
            data: Dict[str, str] = {}
            for token in line.strip().split():
                if "=" not in token:
                    continue
                key, value = token.split("=", 1)
                data[key] = value
            entries.append(data)
    return entries


def check_log(path: str, allow_audit_mismatch: bool) -> LogStats:
    stats = LogStats()
    entries = load_canonical_entries(path)
    for i, data in enumerate(entries, start=1):
        validate_entry(data, path, i, stats, allow_audit_mismatch)
    return stats


def _ensure_run(
    current: Optional[RunAggregate],
    runs: List[RunAggregate],
    path: str,
    create_if_missing: bool = True,
) -> Optional[RunAggregate]:
    if current is not None:
        return current
    if runs:
        return runs[-1]
    if not create_if_missing:
        return None
    fallback = RunAggregate(name=f"__unscoped__:{os.path.basename(path)}", log_path=path)
    runs.append(fallback)
    return fallback


def parse_doe_runs(path: str) -> List[RunAggregate]:
    runs: List[RunAggregate] = []
    run_by_name: Dict[str, RunAggregate] = {}
    current: Optional[RunAggregate] = None

    with open(path, "r", encoding="utf-8", errors="replace") as f:
        for raw_line in f:
            line = raw_line.strip()
            if not line:
                continue

            if line.startswith(RUN_START_PREFIX):
                data = parse_tokens(line)
                name = data.get("name") or f"run_{len(runs) + 1}"
                current = RunAggregate(
                    name=name,
                    log_path=path,
                    prune_mode=_norm_mode(data.get("prune")),
                    frames=to_int(data.get("frames"), default=0) if parse_num(data.get("frames")) is not None else None,
                    warmup=to_int(data.get("warmup"), default=0) if parse_num(data.get("warmup")) is not None else None,
                )
                runs.append(current)
                run_by_name[name] = current
                continue

            if line.startswith(RUN_SUMMARY_PREFIX):
                data = parse_tokens(line)
                name = data.get("name")
                target = run_by_name.get(name) if name else current
                if target is not None:
                    mm = parse_num(data.get("meanMsPerFrame"))
                    pp = parse_num(data.get("p95MsPerFrame"))
                    ss = parse_num(data.get("samples"))
                    if mm is not None:
                        target.mean_ms = mm
                    if pp is not None:
                        target.p95_ms = pp
                    if ss is not None:
                        target.samples = int(ss)
                continue

            if line.startswith(RUN_END_PREFIX):
                data = parse_tokens(line)
                end_name = data.get("name")
                target = run_by_name.get(end_name) if end_name else current
                if target is not None:
                    mm = parse_num(data.get("meanMs"))
                    pp = parse_num(data.get("p95Ms"))
                    ff = parse_num(data.get("frames"))
                    ww = parse_num(data.get("warmup"))
                    ss = parse_num(data.get("samples"))
                    if mm is not None:
                        target.mean_ms = mm
                    if pp is not None:
                        target.p95_ms = pp
                    if ff is not None:
                        target.frames = int(ff)
                    if ww is not None:
                        target.warmup = int(ww)
                    if ss is not None:
                        target.samples = int(ss)
                if current is not None and (end_name is None or current.name == end_name):
                    current = None
                continue

            if line.startswith(EFFECTIVE_CFG_PREFIX):
                target = _ensure_run(current, runs, path, create_if_missing=False)
                if target is not None and not target.effective_cfg:
                    target.effective_cfg = parse_tokens(line)
                continue

            if line.startswith(NONCANONICAL_RENDERHEALTH_DEBUG_PREFIX):
                target = _ensure_run(current, runs, path, create_if_missing=False)
                if target is None:
                    continue
                data = parse_tokens(line)
                if line.startswith(TRUST_GATE_DEBUG_PREFIX):
                    target.attach_trust_debug(data)
                elif line.startswith(GEOM_COVERAGE_PREFIX):
                    target.attach_geom_coverage(data)
                elif line.startswith(TRUST_CFG_PREFIX):
                    target.attach_trust_cfg(data)
                continue

            if line.startswith(CANONICAL_RENDERHEALTH_PREFIX):
                target = _ensure_run(current, runs, path)
                target.add_window(parse_tokens(line))
                continue

    return runs


def build_doe_summary(runs: List[RunAggregate]) -> DoeSummary:
    global_hist: Counter = Counter()
    per_run_hist: Dict[str, Counter] = {}
    trust_flip_rows: List[Dict[str, Any]] = []
    low_ray: List[Dict[str, Any]] = []
    worst: List[Dict[str, Any]] = []

    for run in runs:
        run_hist: Counter = Counter()
        trusted_windows = 0
        first_trusted_step: Optional[int] = None

        for w in run.windows:
            trusted = is_trusted_window(w.renderhealth)
            if trusted:
                trusted_windows += 1
                if first_trusted_step is None:
                    first_trusted_step = w.step

            reason = (w.renderhealth.get("geomTrustReason") or "none").strip().lower()
            if not reason:
                reason = "none"
            run_hist[reason] += 1
            global_hist[reason] += 1

            if reason != "low_raytests":
                continue

            ray_raw = _value_from_window(w, "geomRayTestsTotalRaw")
            min_ray = _value_from_window(w, "minRayTests")
            pix_raw = _value_from_window(w, "geomPixProcessedRaw")
            min_pix = _value_from_window(w, "minGeomPix")
            cand_pix = _value_from_window(w, "geomPixHadAnyCandidatesRaw")
            no_cand = _value_from_window(w, "geomPixNoCandRaw")
            p2_raw = _value_from_window(w, "p2SampRaw")
            min_p2 = _value_from_window(w, "minP2SamplesForTrust")

            ray_per_pix = None
            if ray_raw is not None and pix_raw is not None and pix_raw > 0:
                ray_per_pix = ray_raw / pix_raw

            cand_rate = None
            no_cand_rate = None
            if cand_pix is not None and no_cand is not None and (cand_pix + no_cand) > 0:
                denom = cand_pix + no_cand
                cand_rate = cand_pix / denom
                no_cand_rate = no_cand / denom

            low_row = {
                "runName": run.name,
                "logPath": run.log_path,
                "step": w.step,
                "geomRayTestsTotalRaw": ray_raw,
                "minRayTests": min_ray,
                "geomPixProcessedRaw": pix_raw,
                "minGeomPix": min_pix,
                "candPix": cand_pix,
                "noCand": no_cand,
                "rayTestsPerGeomPix": ray_per_pix,
                "candidateRate": cand_rate,
                "noCandidateRate": no_cand_rate,
            }
            low_ray.append(low_row)

            ray_deficit = (min_ray - ray_raw) if (min_ray is not None and ray_raw is not None) else None
            pix_deficit = (min_pix - pix_raw) if (min_pix is not None and pix_raw is not None) else None
            p2_deficit = (min_p2 - p2_raw) if (min_p2 is not None and p2_raw is not None) else None
            has_any = (
                (ray_deficit is not None and ray_deficit > 0)
                or (pix_deficit is not None and pix_deficit > 0)
                or (p2_deficit is not None and p2_deficit > 0)
            )
            if has_any:
                worst.append(
                    {
                        "runName": run.name,
                        "logPath": run.log_path,
                        "step": w.step,
                        "trustReason": reason,
                        "raytestsDeficit": max(ray_deficit, 0.0) if ray_deficit is not None else None,
                        "geomPixDeficit": max(pix_deficit, 0.0) if pix_deficit is not None else None,
                        "p2Deficit": max(p2_deficit, 0.0) if p2_deficit is not None else None,
                        "geomRayTestsTotalRaw": ray_raw,
                        "minRayTests": min_ray,
                        "geomPixProcessedRaw": pix_raw,
                        "minGeomPix": min_pix,
                        "p2SampRaw": p2_raw,
                        "minP2SamplesForTrust": min_p2,
                    }
                )

        per_run_hist[run.name] = run_hist
        trust_flip_rows.append(
            {
                "runName": run.name,
                "logPath": run.log_path,
                "firstTrustedStep": first_trusted_step,
                "trustedWindows": trusted_windows,
                "totalWindows": len(run.windows),
            }
        )

    def _score(item: Dict[str, Any]) -> float:
        score = 0.0
        for key in ("raytestsDeficit", "geomPixDeficit", "p2Deficit"):
            val = item.get(key)
            if isinstance(val, (int, float)):
                score += float(val)
        return score

    worst_sorted = sorted(worst, key=_score, reverse=True)[:10]
    return DoeSummary(
        runs=runs,
        global_trust_hist=global_hist,
        per_run_trust_hist=per_run_hist,
        trust_flip_rows=trust_flip_rows,
        low_raytests_occurrences=low_ray,
        worst_windows=worst_sorted,
    )


def _cfg_short(cfg: Dict[str, str]) -> str:
    if not cfg:
        return ""
    keys = (
        "broadphase",
        "policy",
        "quick",
        "overlap",
        "softgate",
        "score",
        "stride",
        "resScale",
        "rows",
        "geomPrune",
        "maxDist",
    )
    parts: List[str] = []
    for key in keys:
        val = cfg.get(key)
        if val is not None:
            parts.append(f"{key}={val}")
    return ";".join(parts)


def _step_sort_key(step: Optional[int]) -> int:
    return -1 if step is None else step


def _percentile(values: List[float], pct: float) -> Optional[float]:
    if not values:
        return None
    if len(values) == 1:
        return values[0]
    p = max(0.0, min(100.0, pct))
    s = sorted(values)
    n = len(s)
    pos = (p / 100.0) * (n - 1)
    lo = int(pos)
    hi = min(lo + 1, n - 1)
    frac = pos - lo
    return s[lo] * (1.0 - frac) + s[hi] * frac


def _fmt_int_opt(value: Optional[float]) -> str:
    if value is None:
        return "na"
    return str(int(value))


def _window_reason(window: WindowSnapshot) -> str:
    reason = (window.renderhealth.get("geomTrustReason") or "none").strip().lower()
    return reason if reason else "none"


def _window_trust_cfg(window: WindowSnapshot, run: RunAggregate) -> Dict[str, str]:
    if window.trust_cfg:
        return window.trust_cfg
    return run.latest_trust_cfg


def _collect_reason_hist(summary: DoeSummary) -> Counter:
    hist: Counter = Counter()
    for run in summary.runs:
        for w in run.windows:
            hist[_window_reason(w)] += 1
    return hist


def print_global_summary(summary: DoeSummary) -> None:
    total_windows = sum(len(run.windows) for run in summary.runs)
    trusted_windows = 0
    partial_windows = 0
    for run in summary.runs:
        for w in run.windows:
            if is_trusted_window(w.renderhealth):
                trusted_windows += 1
            if to_int(w.renderhealth.get("geomHealthPartial")) == 1:
                partial_windows += 1
    print("GLOBAL SUMMARY")
    print(
        f"  runs={len(summary.runs)} windows={total_windows} "
        f"trusted={trusted_windows} partial={partial_windows}"
    )
    print("")


def print_run_report(summary: DoeSummary) -> None:
    print("RUN REPORT")
    if not summary.runs:
        print("  none")
        print("")
        return

    for run in summary.runs:
        trusted_steps: List[int] = []
        reason_hist: Counter = Counter()
        per_px_off_vals: List[float] = []
        per_px_on_vals: List[float] = []
        saved_vals: List[float] = []
        low_ray_windows: List[WindowSnapshot] = []
        low_p2_windows: List[WindowSnapshot] = []
        partial_count = 0

        for w in run.windows:
            if is_trusted_window(w.renderhealth) and w.step is not None:
                trusted_steps.append(w.step)
            if to_int(w.renderhealth.get("geomHealthPartial")) == 1:
                partial_count += 1

            reason = _window_reason(w)
            reason_hist[reason] += 1
            if reason == "low_raytests":
                low_ray_windows.append(w)
            elif reason == "low_p2samp":
                low_p2_windows.append(w)

            v = parse_num(w.renderhealth.get("geomRayTestsPerPxOff"))
            if v is not None:
                per_px_off_vals.append(v)
            v = parse_num(w.renderhealth.get("geomRayTestsPerPxOn"))
            if v is not None:
                per_px_on_vals.append(v)
            v = parse_num(w.renderhealth.get("geomRayTestsSavedPct"))
            if v is not None:
                saved_vals.append(v)

        first_trusted = min(trusted_steps) if trusted_steps else None
        last_trusted = max(trusted_steps) if trusted_steps else None
        trusted_count = len(trusted_steps)
        top_reasons = reason_hist.most_common(3)
        top_text = ", ".join([f"{k}={v}" for k, v in top_reasons]) if top_reasons else "none"
        run_frames = run.frames if run.frames is not None else "na"
        run_warmup = run.warmup if run.warmup is not None else "na"
        run_samples = run.samples if run.samples is not None else "na"

        print(
            f"  run={run.name} frames={run_frames} warmup={run_warmup} samples={run_samples} "
            f"windows={len(run.windows)} trusted={trusted_count} partial={partial_count}"
        )
        print(
            f"    firstTrustedStep={first_trusted if first_trusted is not None else 'na'} "
            f"lastTrustedStep={last_trusted if last_trusted is not None else 'na'}"
        )
        print(f"    topReasons={top_text}")
        print(
            "    perPxOff(mean,p95)="
            f"{_fmt_opt(mean_num(per_px_off_vals))},{_fmt_opt(_percentile(per_px_off_vals, 95.0))} "
            "perPxOn(mean,p95)="
            f"{_fmt_opt(mean_num(per_px_on_vals))},{_fmt_opt(_percentile(per_px_on_vals, 95.0))} "
            f"savedPctMean={_fmt_opt(mean_num(saved_vals))}"
        )

        if low_ray_windows:
            print("    low_raytests examples:")
            for w in sorted(low_ray_windows, key=lambda x: _step_sort_key(x.step))[:3]:
                print(
                    "      "
                    f"step={w.step if w.step is not None else 'na'} "
                    f"geomRayTestsTotalRaw={_fmt_int_opt(_value_from_window(w, 'geomRayTestsTotalRaw'))} "
                    f"minRayTests={_fmt_int_opt(_value_from_window(w, 'minRayTests'))} "
                    f"trustRayTestsMet={_fmt_int_opt(_value_from_window(w, 'trustRayTestsMet'))} "
                    f"geomPixProcessedRaw={_fmt_int_opt(_value_from_window(w, 'geomPixProcessedRaw'))} "
                    f"minGeomPix={_fmt_int_opt(_value_from_window(w, 'minGeomPix'))}"
                )

        if low_p2_windows:
            print("    low_p2samp examples:")
            for w in sorted(low_p2_windows, key=lambda x: _step_sort_key(x.step))[:3]:
                cfg = _window_trust_cfg(w, run)
                print(
                    "      "
                    f"step={w.step if w.step is not None else 'na'} "
                    f"p2SampRaw={_fmt_int_opt(_value_from_window(w, 'p2SampRaw'))} "
                    f"minP2={_fmt_int_opt(parse_num(cfg.get('minP2SamplesForTrust')))} "
                    f"p2Every={_fmt_int_opt(parse_num(cfg.get('p2SampleEveryNSeg')))}"
                )

    print("")


def print_reason_histogram(summary: DoeSummary) -> None:
    reason_hist = _collect_reason_hist(summary)
    trusted_hist: Counter = Counter()
    prune_hist: Counter = Counter()
    pix_met_ray_not_met = 0

    for run in summary.runs:
        for w in run.windows:
            trusted_hist["trusted=1" if is_trusted_window(w.renderhealth) else "trusted=0"] += 1
            mode = _norm_mode(w.renderhealth.get("geomPrune"))
            if mode == "on":
                prune_hist["prune=on"] += 1
            elif mode == "off":
                prune_hist["prune=off"] += 1
            met_pix = parse_num(w.trust_debug.get("trustGeomPixMet"))
            met_ray = parse_num(w.trust_debug.get("trustRayTestsMet"))
            if met_pix == 1 and met_ray == 0:
                pix_met_ray_not_met += 1

    print("REASON HISTOGRAM")
    if not reason_hist:
        print("  reasons: none")
    else:
        print("  reasons:")
        for reason, count in reason_hist.most_common():
            print(f"    {reason}: {count}")

    print(
        "  trusted: "
        f"trusted=1={trusted_hist.get('trusted=1', 0)} "
        f"trusted=0={trusted_hist.get('trusted=0', 0)}"
    )
    print(
        "  prune: "
        f"prune=on={prune_hist.get('prune=on', 0)} "
        f"prune=off={prune_hist.get('prune=off', 0)}"
    )
    print(f"  trustGateDebug metPix=1 metRay=0: {pix_met_ray_not_met}")
    print("")


def print_brief_report(summary: DoeSummary) -> None:
    print_global_summary(summary)
    print("PER-RUN FIRST TRUSTED")
    if not summary.trust_flip_rows:
        print("  none")
    else:
        for row in summary.trust_flip_rows:
            first = row.get("firstTrustedStep")
            print(f"  {row['runName']}: firstTrustedStep={first if first is not None else 'na'}")
    print("")
    print_reason_histogram(summary)


def print_doe_report(summary: DoeSummary) -> None:
    print("DOE summary:")
    print("A) TrustReason histogram (global)")
    if not summary.global_trust_hist:
        print("  none")
    else:
        for reason, count in summary.global_trust_hist.most_common():
            print(f"  {reason}: {count}")
    print("")

    print("A) TrustReason histogram (per run)")
    for run in summary.runs:
        hist = summary.per_run_trust_hist.get(run.name, Counter())
        if not hist:
            print(f"  {run.name}: none")
            continue
        parts = [f"{k}={v}" for k, v in hist.most_common()]
        print(f"  {run.name}: " + ", ".join(parts))
    print("")

    print("B) Trust flip timeline")
    for row in summary.trust_flip_rows:
        total = row["totalWindows"]
        trusted = row["trustedWindows"]
        pct = (100.0 * trusted / total) if total else 0.0
        flip = row["firstTrustedStep"]
        flip_text = "none" if flip is None else str(flip)
        print(f"  {row['runName']}: firstTrustedStep={flip_text} trusted_windows={trusted}/{total} ({pct:.1f}%)")
    print("")

    lows = summary.low_raytests_occurrences
    print(f"C) low_raytests diagnostics: count={len(lows)}")
    for item in lows[:5]:
        cand = item.get("candPix")
        noc = item.get("noCand")
        cand_pair = "na/na"
        if cand is not None and noc is not None:
            cand_pair = f"{int(cand)}/{int(noc)}"
        print(
            "  "
            f"run={item['runName']} step={item.get('step')} "
            f"geomRayTestsTotalRaw={item.get('geomRayTestsTotalRaw')} minRayTests={item.get('minRayTests')} "
            f"geomPixProcessedRaw={item.get('geomPixProcessedRaw')} minGeomPix={item.get('minGeomPix')} "
            f"candPix/noCand={cand_pair} "
            f"rayTestsPerGeomPix={_fmt_opt(item.get('rayTestsPerGeomPix'))} "
            f"candRate={_fmt_opt(item.get('candidateRate'))} noCandRate={_fmt_opt(item.get('noCandidateRate'))}"
        )
    print("")

    print("D) Top 10 worst trust failures by deficit")
    if not summary.worst_windows:
        print("  none")
    else:
        for item in summary.worst_windows:
            print(
                "  "
                f"run={item.get('runName')} step={item.get('step')} "
                f"raytestsDeficit={_fmt_opt(item.get('raytestsDeficit'))} "
                f"geomPixDeficit={_fmt_opt(item.get('geomPixDeficit'))} "
                f"p2Deficit={_fmt_opt(item.get('p2Deficit'))}"
            )
    print("")


def write_doe_artifacts(summary: DoeSummary) -> Dict[str, str]:
    base_dir = os.path.dirname(os.path.abspath(__file__))
    csv_path = os.path.join(base_dir, "renderhealth_summary.csv")
    json_path = os.path.join(base_dir, "renderhealth_summary.json")

    csv_fields = [
        "runName",
        "pruneMode",
        "meanMs",
        "p95Ms",
        "totalWindows",
        "trustedWindows",
        "pctLowRaytests",
        "meanPerPxOffNumeric",
        "meanPerPxOnNumeric",
        "meanSavedPctNumeric",
        "meanP2Samp",
        "meanHitRate",
        "effectiveCfgShort",
    ]
    run_rows: List[Dict[str, Any]] = []

    for run in summary.runs:
        trusted_windows = sum(1 for w in run.windows if is_trusted_window(w.renderhealth))
        total_windows = len(run.windows)
        low_count = summary.per_run_trust_hist.get(run.name, Counter()).get("low_raytests", 0)

        per_px_off_vals: List[float] = []
        per_px_on_vals: List[float] = []
        saved_vals: List[float] = []
        p2_vals: List[float] = []
        hit_vals: List[float] = []
        for w in run.windows:
            v = parse_num(w.renderhealth.get("geomRayTestsPerPxOff"))
            if v is not None:
                per_px_off_vals.append(v)
            v = parse_num(w.renderhealth.get("geomRayTestsPerPxOn"))
            if v is not None:
                per_px_on_vals.append(v)
            v = parse_num(w.renderhealth.get("geomRayTestsSavedPct"))
            if v is not None:
                saved_vals.append(v)
            v = parse_num(w.renderhealth.get("p2Samp"))
            if v is not None:
                p2_vals.append(v)
            v = parse_num(w.renderhealth.get("hitRate"))
            if v is not None:
                hit_vals.append(v)

        prune_mode = run.prune_mode
        if prune_mode is None and run.effective_cfg:
            prune_mode = _norm_mode(run.effective_cfg.get("geomPrune"))
        if prune_mode is None and run.windows:
            prune_mode = _norm_mode(run.windows[0].renderhealth.get("geomPrune"))

        run_rows.append(
            {
                "runName": run.name,
                "pruneMode": prune_mode or "",
                "meanMs": _fmt_opt(run.mean_ms),
                "p95Ms": _fmt_opt(run.p95_ms),
                "totalWindows": total_windows,
                "trustedWindows": trusted_windows,
                "pctLowRaytests": _fmt_opt((100.0 * low_count / total_windows) if total_windows else None),
                "meanPerPxOffNumeric": _fmt_opt(mean_num(per_px_off_vals)),
                "meanPerPxOnNumeric": _fmt_opt(mean_num(per_px_on_vals)),
                "meanSavedPctNumeric": _fmt_opt(mean_num(saved_vals)),
                "meanP2Samp": _fmt_opt(mean_num(p2_vals)),
                "meanHitRate": _fmt_opt(mean_num(hit_vals)),
                "effectiveCfgShort": _cfg_short(run.effective_cfg),
            }
        )

    with open(csv_path, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=csv_fields)
        writer.writeheader()
        for row in run_rows:
            writer.writerow(row)

    json_payload = {
        "runs": run_rows,
        "global": {
            "totalRuns": len(summary.runs),
            "totalWindows": sum(len(run.windows) for run in summary.runs),
            "trustReasonHistogram": dict(summary.global_trust_hist),
        },
        "worst_windows": summary.worst_windows,
    }
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump(json_payload, f, indent=2)

    return {"csv": csv_path, "json": json_path}


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
    parser.add_argument(
        "--brief",
        action="store_true",
        help="Print only compact global/per-run trust summary sections after PASS/FAIL results.",
    )
    args = parser.parse_args()

    pass_logs = 0
    all_runs: List[RunAggregate] = []
    for log_path in args.log_paths:
        if not os.path.exists(log_path):
            print(f"FAIL: {log_path} (file not found)")
            return 1
        try:
            stats = check_log(log_path, args.allow_audit_mismatch)
            print_report(log_path, stats)
            try:
                all_runs.extend(parse_doe_runs(log_path))
            except Exception as ex:
                print(f"WARN: DOE parsing skipped for {log_path}: {ex}")
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

    doe_summary = build_doe_summary(all_runs)
    print("")
    if args.brief:
        print_brief_report(doe_summary)
    else:
        print_doe_report(doe_summary)
        print_run_report(doe_summary)
        print_reason_histogram(doe_summary)
    try:
        artifacts = write_doe_artifacts(doe_summary)
        print(f"CSV written: {artifacts['csv']}")
        print(f"JSON written: {artifacts['json']}")
    except Exception as ex:
        print(f"WARN: failed to write DOE artifacts: {ex}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
