#!/usr/bin/env python3
"""Summarize curved-field validation ladder outputs and build storyboards.

This is post-process validation tooling. It does not modify renderer behavior
and does not treat renderer-reference diagnostics as physical truth.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import re
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont, ImageOps


VISIBLE_SUPPORT_WORDING = "visible band/support metrics"
GUARDRAIL = (
    "Do not describe visible band/support artifacts as caused by curvature unless "
    "comparison metrics support that claim; use 'associated with curved transport "
    "fixture under tested settings.'"
)
PANEL_SIZE = (320, 180)
STORYBOARD_SCHEMA_VERSION = 1
VISUAL_HIERARCHY = [
    "cartesian_geometry_projection",
    "hit_normals_and_transport_vectors",
    "ownership_topology_and_seams",
    "budget_saturation_and_unresolved_islands",
    "graph_lineage_and_phase_evolution",
]
STORYBOARD_TAGLINE = (
    "geometry -> transport -> topology -> quality/budget"
)
EVOLUTION_PANEL_SIZE = (240, 135)
PHASE_COLORS = {
    "underresolved": (220, 70, 70),
    "converging": (255, 196, 50),
    "plateau": (70, 190, 95),
    "budget_saturated": (155, 95, 235),
    "": (120, 125, 145),
}


def load_json(path: Path | None) -> dict[str, Any]:
    if not path or not path.exists():
        return {}
    try:
        return json.loads(path.read_text())
    except Exception:
        return {}


def load_csv(path: Path | None) -> list[dict[str, str]]:
    if not path or not path.exists():
        return []
    with path.open(newline="", encoding="utf-8-sig") as handle:
        return list(csv.DictReader(handle))


def write_csv(path: Path, rows: list[dict[str, Any]], cols: list[str]) -> None:
    with path.open("w", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=cols)
        writer.writeheader()
        for row in rows:
            writer.writerow({key: row.get(key, "") for key in cols})


def parse_float(value: Any, default: float = math.nan) -> float:
    try:
        if value in ("", None, "nan", "NaN"):
            return default
        return float(value)
    except Exception:
        return default


def parse_int(value: Any, default: int = 0) -> int:
    try:
        if value in ("", None):
            return default
        return int(round(float(value)))
    except Exception:
        return default


def parse_bool(value: Any) -> bool:
    if isinstance(value, bool):
        return value
    return str(value).strip().lower() in {"1", "true", "yes"}


def sha256_file(path: Path | None) -> str:
    if not path or not path.exists():
        return ""
    h = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def find_one(folder: Path, pattern: str) -> Path | None:
    matches = sorted(folder.glob(pattern))
    return matches[0] if matches else None


def find_beauty(folder: Path) -> Path | None:
    exact = sorted(folder.glob("*.png"))
    skip_prefixes = (
        "layer",
        "combined_",
        "diagnostic_",
        "transport_",
        "ownership_",
        "unstable_",
        "graph_",
        "merge_",
        "hit_normal_",
        "camera_",
        "epsilon_",
        "first_stable_",
        "decision_",
        "path_",
        "normal_",
        "island_",
        "precision_",
        "production_",
        "oracle_",
        "parent_",
        "convergence_",
    )
    candidates = [p for p in exact if not p.name.startswith(skip_prefixes) and "overlay" not in p.name]
    candidates.sort(key=lambda p: p.stat().st_size if p.exists() else 0, reverse=True)
    return candidates[0] if candidates else None


def latest_or_blank(paths: list[Path]) -> str:
    return str(paths[-1]) if paths else ""


def candidate_inventory(repo_root: Path, output_root: Path) -> list[dict[str, Any]]:
    patterns = [
        "test-*curv*.tscn",
        "test-*grin*.tscn",
        "test-*einstein*.tscn",
        "test-*blackhole*.tscn",
        "test-*boundary*.tscn",
        "Fixtures/fixture_*curv*.tscn",
        "Fixtures/fixture_*grin*.tscn",
        "Fixtures/fixture_*einstein*.tscn",
        "Fixtures/fixture_*blackhole*.tscn",
        "Fixtures/fixture_*boundary*.tscn",
        "Fixtures/fixture_*metric*.tscn",
    ]
    files: list[Path] = []
    for pattern in patterns:
        files.extend(repo_root.glob(pattern))
    seen: set[Path] = set()
    rows: list[dict[str, Any]] = []
    for path in sorted(files):
        if path in seen or not path.exists():
            continue
        seen.add(path)
        text = path.read_text(errors="ignore")
        lower = str(path).lower()
        field_node = "FieldSource3D" in text or "FieldPath" in text
        fingerprint = "CurvedMinimalFingerprint" in text or "curved_minimal" in lower
        nonzero_tokens = []
        for key in ("Strength", "CanonicalGamma", "DefaultGammaOverride", "Gamma", "BendScale", "FieldStrength", "OuterRadius"):
            for match in re.finditer(rf"\b{key}\s*=\s*(-?\d+(?:\.\d+)?)", text):
                if abs(parse_float(match.group(1), 0.0)) > 1e-9:
                    nonzero_tokens.append(f"{key}={match.group(1)}")
        prior = sorted(repo_root.glob(f"output/**/*{path.stem.replace('test-', '').replace('-', '_')}*"))
        rows.append({
            "scene_path": str(path),
            "field_node_present": field_node,
            "curvature_or_grin_params_nonzero": bool(nonzero_tokens or fingerprint),
            "nonzero_param_evidence": ";".join(nonzero_tokens[:8]) if nonzero_tokens else ("curved_minimal_fingerprint" if fingerprint else ""),
            "curved_transport_enabled_hint": "CurvedMinimalFingerprint" in text or "UseIntegratedField = true" in text or "TransportModel" in text,
            "hit_diagnostics_available": bool(list(repo_root.glob(f"output/**/*{path.stem}*.hit_diagnostics.csv"))),
            "prior_outputs_available": bool(prior),
            "prior_output_example": latest_or_blank(prior[-3:]),
        })
    return rows


def parse_curvature_evidence(log_path: Path | None) -> dict[str, Any]:
    if not log_path or not log_path.exists():
        return {
            "curvature_metric_log_present": False,
            "resolved_log_present": False,
            "curved_transport_enabled": False,
            "nonzero_curvature_params": False,
            "valid": False,
            "reason": "missing curved fixture log",
        }
    text = log_path.read_text(errors="replace")
    metric_line = next((line for line in text.splitlines() if "[CurvedFixture][CurvatureMetric]" in line), "")
    resolved_line = next((line for line in text.splitlines() if "[CurvedFixture][Resolved]" in line), "")
    field_accel = parse_float(re.search(r"field_accel_sum=([^\s]+)", metric_line).group(1), 0.0) if re.search(r"field_accel_sum=([^\s]+)", metric_line) else 0.0
    curvature_energy = parse_float(re.search(r"curvature_energy=([^\s]+)", metric_line).group(1), 0.0) if re.search(r"curvature_energy=([^\s]+)", metric_line) else 0.0
    use_integrated = "useIntegrated=1" in resolved_line
    bend = parse_float(re.search(r"bendScale=([^\s]+)", resolved_line).group(1), 0.0) if re.search(r"bendScale=([^\s]+)", resolved_line) else 0.0
    strength = parse_float(re.search(r"fieldStrength=([^\s]+)", resolved_line).group(1), 0.0) if re.search(r"fieldStrength=([^\s]+)", resolved_line) else 0.0
    valid = bool(metric_line and resolved_line and field_accel > 0.0 and curvature_energy > 0.0 and use_integrated and abs(bend * strength) > 0.0)
    return {
        "curvature_metric_log_present": bool(metric_line),
        "resolved_log_present": bool(resolved_line),
        "curved_transport_enabled": bool(use_integrated and abs(bend * strength) > 0.0),
        "nonzero_curvature_params": bool(field_accel > 0.0 and curvature_energy > 0.0),
        "field_accel_sum": round(field_accel, 6),
        "curvature_energy": round(curvature_energy, 6),
        "bend_scale": bend,
        "field_strength": strength,
        "valid": valid,
        "reason": "ok" if valid else "curved fixture log lacks required curvature evidence",
        "metric_line": metric_line,
        "resolved_line": resolved_line,
    }


def collect_cells(root: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for meta_path in sorted(root.glob("**/metadata.json")):
        meta = load_json(meta_path)
        cell_dir = Path(meta.get("cell_dir", meta_path.parent))
        if not cell_dir.is_absolute():
            cell_dir = meta_path.parent
        if meta.get("study") != "curved_field_validation_ladder":
            continue
        rows.append({"meta": meta, "cell_dir": cell_dir})
    return rows


def graph_metrics(folder: Path | None) -> dict[str, Any]:
    if not folder:
        return {}
    direct = load_json(folder / "transport_ownership_graph_metrics.json")
    if direct:
        return direct
    nested = sorted(folder.glob("**/transport_ownership_graph_metrics.json"))
    return load_json(nested[0]) if nested else {}


def count_rows(path: Path | None) -> int:
    return len(load_csv(path)) if path and path.exists() else 0


def read_role_step_metrics(root: Path, role: str, primary_step: str) -> dict[str, Any]:
    step_dir = root / role / "steps" / f"step_{primary_step}"
    metrics = graph_metrics(step_dir)
    ladder_metrics = graph_metrics(root / role / "graph_ladder")
    budget = load_json(step_dir / "budget_exhaustion_summary.json")
    return {
        "step_dir": str(step_dir),
        "graph_node_count": parse_int(metrics.get("graph_node_count"), 0),
        "graph_edge_count": parse_int(metrics.get("graph_edge_count"), 0),
        "seam_length_px_total": parse_int(metrics.get("seam_length_px_total"), 0),
        "unresolved_pixel_count": parse_int(metrics.get("unresolved_pixel_count"), 0),
        "high_discontinuity_edge_count": parse_int(metrics.get("high_discontinuity_edge_count"), 0),
        "budget_exhausted_pixel_count": parse_int(budget.get("budget_exhausted_pixel_count"), 0),
        "budget_exhausted_hit_count": parse_int(budget.get("budget_exhausted_hit_count"), 0),
        "budget_exhausted_no_hit_count": parse_int(budget.get("budget_exhausted_no_hit_count"), 0),
        "budget_exhaustion_percent": budget.get("budget_exhaustion_percent", ""),
        "step_quality_plateau_candidate": budget.get("step_quality_plateau_candidate", ""),
        "merge_split_count": count_rows(root / role / "graph_ladder" / "transport_ownership_graph_merges_splits.csv"),
        "ladder_persistence_basis": ladder_metrics.get("persistence_basis", ""),
        "node_persistence_rate": ladder_metrics.get("node_persistence_rate", ""),
    }


def collect_ladder_step_metrics(root: Path, role: str, steps: list[str]) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for step in steps:
        step_dir = root / role / "steps" / f"step_{step}"
        metrics = graph_metrics(step_dir)
        budget = load_json(step_dir / "budget_exhaustion_summary.json")
        hit_csv = find_one(step_dir, "*.hit_diagnostics.csv")
        hit_count = 0
        if hit_csv:
            for row in load_csv(hit_csv):
                if parse_bool(row.get("had_hit")):
                    hit_count += 1
        rows.append({
            "role": role,
            "step_length": step,
            "hit_count": hit_count,
            "unresolved_count": parse_int(metrics.get("unresolved_pixel_count"), 0),
            "graph_node_count": parse_int(metrics.get("graph_node_count"), 0),
            "graph_edge_count": parse_int(metrics.get("graph_edge_count"), 0),
            "seam_length_px_total": parse_int(metrics.get("seam_length_px_total"), 0),
            "merge_split_count": count_rows(root / role / "graph_ladder" / "transport_ownership_graph_merges_splits.csv"),
            "budget_exhausted_pixel_count": parse_int(budget.get("budget_exhausted_pixel_count"), 0),
            "budget_exhaustion_percent": budget.get("budget_exhaustion_percent", ""),
            "step_quality_plateau_candidate": budget.get("step_quality_plateau_candidate", ""),
        })
    return rows


def infer_diminishing_returns(rows: list[dict[str, Any]]) -> dict[str, Any]:
    if not rows:
        return {"likely_point_of_diminishing_returns": "", "reason": "no rows"}
    # Steps are supplied coarse -> fine in the runner. The first fine step with
    # budget exhaustion is where more precision may stop buying quality.
    previous: dict[str, Any] | None = None
    for row in rows:
        budget = parse_int(row.get("budget_exhausted_pixel_count"), 0)
        if budget > 0:
            prior_step = previous.get("step_length", "") if previous else ""
            return {
                "likely_point_of_diminishing_returns": row.get("step_length", ""),
                "last_pre_budget_step": prior_step,
                "reason": f"quality improves until step {prior_step or 'before first sampled step'}, then budget exhaustion appears at step {row.get('step_length', '')}",
            }
        previous = row
    return {
        "likely_point_of_diminishing_returns": "",
        "last_pre_budget_step": rows[-1].get("step_length", ""),
        "reason": "no traversal budget exhaustion detected across sampled steps",
    }


def step_key(value: Any) -> str:
    parsed = parse_float(value)
    if math.isfinite(parsed):
        return f"{parsed:.10g}"
    return str(value)


def load_oracle_agreement_by_step(root: Path, role: str) -> dict[str, dict[str, Any]]:
    grouped: dict[str, dict[str, Any]] = {}
    oracle_root = root / role / "oracle"
    for path in sorted(oracle_root.glob("*.reference_transport_oracle_comparisons.csv")):
        for row in load_csv(path):
            key = step_key(row.get("production_step_length", ""))
            if not key:
                continue
            bucket = grouped.setdefault(key, {
                "oracle_comparison_count": 0,
                "oracle_stable_count": 0,
                "oracle_unresolved_count": 0,
                "oracle_agreement_sum": 0.0,
                "oracle_agreement_rows": 0,
            })
            bucket["oracle_comparison_count"] += 1
            phase = str(row.get("epsilon_stability_class", "")).strip().lower()
            if phase == "stable":
                bucket["oracle_stable_count"] += 1
            elif phase:
                bucket["oracle_unresolved_count"] += 1
            agreement = parse_float(row.get("ownership_graph_agreement"))
            if math.isfinite(agreement):
                bucket["oracle_agreement_sum"] += agreement
                bucket["oracle_agreement_rows"] += 1
    for bucket in grouped.values():
        total = max(1, int(bucket["oracle_comparison_count"]))
        agreement_rows = int(bucket["oracle_agreement_rows"])
        bucket["oracle_stability_rate"] = round(float(bucket["oracle_stable_count"]) / total, 6)
        bucket["oracle_agreement_rate"] = round(float(bucket["oracle_agreement_sum"]) / agreement_rows, 6) if agreement_rows else ""
    return grouped


def classify_transport_quality_phase(row: dict[str, Any], prev: dict[str, Any] | None) -> str:
    budget_pct = parse_float(row.get("budget_exhaustion_percent"), 0.0)
    budget_count = parse_int(row.get("budget_exhausted_pixel_count"), 0)
    if budget_count > 0 or budget_pct > 0.0:
        return "budget_saturated"

    unresolved = parse_int(row.get("unresolved_count"), 0)
    oracle_stability = parse_float(row.get("oracle_stability_rate"))
    oracle_agreement = parse_float(row.get("oracle_agreement_rate"))
    if unresolved > 0:
        return "underresolved"
    if math.isfinite(oracle_stability) and oracle_stability < 0.75:
        return "underresolved"
    if math.isfinite(oracle_agreement) and oracle_agreement < 0.75:
        return "underresolved"

    if prev is None:
        return "converging"

    node_delta = abs(parse_int(row.get("graph_node_delta_prev"), 0))
    edge_delta = abs(parse_int(row.get("graph_edge_delta_prev"), 0))
    seam_delta = abs(parse_int(row.get("seam_length_delta_prev"), 0))
    unresolved_delta = abs(parse_int(row.get("unresolved_delta_prev"), 0))
    merge_delta = abs(parse_int(row.get("merge_split_delta_prev"), 0))
    hit_delta = abs(parse_int(row.get("hit_count_delta_prev"), 0))
    oracle_stability_delta = parse_float(row.get("oracle_stability_delta_prev"), 0.0)
    oracle_agreement_delta = parse_float(row.get("oracle_agreement_delta_prev"), 0.0)

    topology_changed = node_delta > 0 or edge_delta > 0 or seam_delta > 0 or merge_delta > 0
    scalar_improved = oracle_stability_delta > 0.05 or oracle_agreement_delta > 0.05 or unresolved_delta > 0
    hit_changed = hit_delta > 0
    if topology_changed or scalar_improved or hit_changed:
        return "converging"
    return "plateau"


def recommended_action_for_phase(phase: str) -> str:
    return {
        "budget_saturated": "increase max traversal/step budget or use adaptive budget scaling",
        "underresolved": "reduce step size or improve oracle/island focus",
        "converging": "continue ladder around neighboring steps",
        "plateau": "candidate operating window / diminishing returns region",
    }.get(phase, "")


def annotate_transport_quality_phases(root: Path, rows: list[dict[str, Any]], steps: list[str]) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    annotated: list[dict[str, Any]] = []
    summary: dict[str, Any] = {}
    for role in ("control", "curved"):
        oracle = load_oracle_agreement_by_step(root, role)
        role_by_step = {str(r.get("step_length")): r for r in rows if r.get("role") == role}
        role_rows: list[dict[str, Any]] = []
        prev: dict[str, Any] | None = None
        for step in steps:
            base = dict(role_by_step.get(step, {"role": role, "step_length": step}))
            oracle_bucket = oracle.get(step_key(step), {})
            base.update({
                "oracle_comparison_count": oracle_bucket.get("oracle_comparison_count", ""),
                "oracle_stable_count": oracle_bucket.get("oracle_stable_count", ""),
                "oracle_unresolved_count": oracle_bucket.get("oracle_unresolved_count", ""),
                "oracle_stability_rate": oracle_bucket.get("oracle_stability_rate", ""),
                "oracle_agreement_rate": oracle_bucket.get("oracle_agreement_rate", ""),
                "node_persistence_rate": read_role_step_metrics(root, role, step).get("node_persistence_rate", ""),
            })
            if prev is None:
                base.update({
                    "hit_count_delta_prev": "",
                    "graph_node_delta_prev": "",
                    "graph_edge_delta_prev": "",
                    "seam_length_delta_prev": "",
                    "unresolved_delta_prev": "",
                    "merge_split_delta_prev": "",
                    "oracle_stability_delta_prev": "",
                    "oracle_agreement_delta_prev": "",
                })
            else:
                base.update({
                    "hit_count_delta_prev": parse_int(base.get("hit_count"), 0) - parse_int(prev.get("hit_count"), 0),
                    "graph_node_delta_prev": parse_int(base.get("graph_node_count"), 0) - parse_int(prev.get("graph_node_count"), 0),
                    "graph_edge_delta_prev": parse_int(base.get("graph_edge_count"), 0) - parse_int(prev.get("graph_edge_count"), 0),
                    "seam_length_delta_prev": parse_int(base.get("seam_length_px_total"), 0) - parse_int(prev.get("seam_length_px_total"), 0),
                    "unresolved_delta_prev": parse_int(base.get("unresolved_count"), 0) - parse_int(prev.get("unresolved_count"), 0),
                    "merge_split_delta_prev": parse_int(base.get("merge_split_count"), 0) - parse_int(prev.get("merge_split_count"), 0),
                    "oracle_stability_delta_prev": round(parse_float(base.get("oracle_stability_rate"), 0.0) - parse_float(prev.get("oracle_stability_rate"), 0.0), 6)
                        if math.isfinite(parse_float(base.get("oracle_stability_rate"))) and math.isfinite(parse_float(prev.get("oracle_stability_rate"))) else "",
                    "oracle_agreement_delta_prev": round(parse_float(base.get("oracle_agreement_rate"), 0.0) - parse_float(prev.get("oracle_agreement_rate"), 0.0), 6)
                        if math.isfinite(parse_float(base.get("oracle_agreement_rate"))) and math.isfinite(parse_float(prev.get("oracle_agreement_rate"))) else "",
                })
            phase = classify_transport_quality_phase(base, prev)
            base["transport_quality_phase"] = phase
            base["recommended_next_action"] = recommended_action_for_phase(phase)
            role_rows.append(base)
            annotated.append(base)
            prev = base

        phase_counts: dict[str, int] = {}
        for row in role_rows:
            phase_counts[str(row.get("transport_quality_phase", ""))] = phase_counts.get(str(row.get("transport_quality_phase", "")), 0) + 1
        plateau_start = next((str(r.get("step_length")) for r in role_rows if r.get("transport_quality_phase") == "plateau"), "")
        budget_start = next((str(r.get("step_length")) for r in role_rows if r.get("transport_quality_phase") == "budget_saturated"), "")
        summary[role] = {
            "phase_counts": phase_counts,
            "plateau_start_step": plateau_start,
            "budget_saturation_start_step": budget_start,
        }
    return annotated, summary


def draw_transport_quality_phase_plot(path: Path, phase_rows: list[dict[str, Any]], steps: list[str]) -> None:
    roles = ["control", "curved"]
    colors = {
        "underresolved": (210, 70, 70, 255),
        "converging": (255, 190, 40, 255),
        "plateau": (60, 180, 90, 255),
        "budget_saturated": (145, 85, 230, 255),
    }
    cell_w = 86
    cell_h = 54
    left = 118
    top = 64
    width = left + cell_w * max(1, len(steps)) + 24
    height = top + cell_h * len(roles) + 88
    img = Image.new("RGBA", (width, height), (14, 16, 22, 255))
    draw = ImageDraw.Draw(img)
    font = ImageFont.load_default()
    draw.text((16, 14), "Transport Quality Phase Ladder", fill=(245, 248, 255, 255), font=font)
    draw.text((16, 30), "passive classifier: graph/seam/oracle/budget diagnostics", fill=(200, 205, 215, 255), font=font)
    row_by_key = {(r.get("role"), str(r.get("step_length"))): r for r in phase_rows}
    for i, step in enumerate(steps):
        x = left + i * cell_w
        draw.text((x + 4, top - 22), str(step), fill=(235, 235, 235, 255), font=font)
    for j, role in enumerate(roles):
        y = top + j * cell_h
        draw.text((16, y + 18), role, fill=(245, 248, 255, 255), font=font)
        for i, step in enumerate(steps):
            x = left + i * cell_w
            row = row_by_key.get((role, step), {})
            phase = str(row.get("transport_quality_phase", ""))
            color = colors.get(phase, (80, 80, 90, 255))
            draw.rectangle((x, y, x + cell_w - 8, y + cell_h - 10), fill=color, outline=(255, 255, 255, 90), width=1)
            label = phase.replace("_", "\n")
            draw.multiline_text((x + 6, y + 8), label, fill=(0, 0, 0, 235), font=font, spacing=1)
            budget = parse_float(row.get("budget_exhaustion_percent"))
            oracle = parse_float(row.get("oracle_stability_rate"))
            note = ""
            if math.isfinite(budget) and budget > 0:
                note = f"budget {budget:.2f}%"
            elif math.isfinite(oracle):
                note = f"oracle {oracle:.2f}"
            if note:
                draw.text((x + 4, y + cell_h - 22), note, fill=(20, 20, 25, 230), font=font)
    legend_x = 18
    legend_y = height - 58
    for idx, (name, color) in enumerate(colors.items()):
        x = legend_x + idx * 155
        draw.rectangle((x, legend_y, x + 14, legend_y + 10), fill=color)
        draw.text((x + 20, legend_y - 2), name, fill=(235, 238, 245, 255), font=font)
    path.parent.mkdir(parents=True, exist_ok=True)
    img.convert("RGB").save(path)


def graph_delta_vs_control(root: Path, primary_step: str) -> dict[str, Any]:
    curved = read_role_step_metrics(root, "curved", primary_step)
    control = read_role_step_metrics(root, "control", primary_step)
    return {
        "node_count_delta": curved["graph_node_count"] - control["graph_node_count"],
        "edge_count_delta": curved["graph_edge_count"] - control["graph_edge_count"],
        "seam_length_delta": curved["seam_length_px_total"] - control["seam_length_px_total"],
        "unresolved_count_delta": curved["unresolved_pixel_count"] - control["unresolved_pixel_count"],
        "merge_split_delta": curved["merge_split_count"] - control["merge_split_count"],
        "high_discontinuity_edge_delta": curved["high_discontinuity_edge_count"] - control["high_discontinuity_edge_count"],
    }


def metadata_for(root: Path, role: str, step: str) -> dict[str, Any]:
    return load_json(root / role / "steps" / f"step_{step}" / "metadata.json")


def compare_configs(
    root: Path,
    steps: list[str],
    control_comparison_type: str = "",
    control_comparison_reason: str = "",
) -> dict[str, Any]:
    keys = [
        "resolution",
        "camera_pose_key",
        "frames",
        "warmup",
        "stride",
        "traversal",
        "scheduler_mode",
        "resolver_flags",
        "step_ladder",
        "hit_diagnostics_flags",
    ]
    warnings: list[dict[str, Any]] = []
    comparisons: list[dict[str, Any]] = []
    for step in steps:
        cmeta = metadata_for(root, "control", step)
        umeta = metadata_for(root, "curved", step)
        if not cmeta or not umeta:
            warnings.append({"step": step, "field": "cell_presence", "control": bool(cmeta), "curved": bool(umeta)})
            continue
        for key in keys:
            cv = cmeta.get(key, "")
            uv = umeta.get(key, "")
            matched = cv == uv
            comparisons.append({"step": step, "field": key, "control": cv, "curved": uv, "matched": matched})
            if not matched:
                warnings.append({"step": step, "field": key, "control": cv, "curved": uv})
    status = "matched" if not warnings else "warning"
    return {
        "comparability_status": status,
        "control_comparison_type": control_comparison_type,
        "control_comparison_reason": control_comparison_reason,
        "warnings": warnings,
        "comparisons": comparisons,
    }


def fit_image(path: Path | None, size: tuple[int, int] = PANEL_SIZE) -> Image.Image:
    base = Image.new("RGBA", size, (12, 14, 20, 255))
    if path and path.exists():
        with Image.open(path) as img:
            fitted = ImageOps.contain(img.convert("RGBA"), size)
        base.alpha_composite(fitted, ((size[0] - fitted.width) // 2, (size[1] - fitted.height) // 2))
    else:
        draw = ImageDraw.Draw(base)
        draw.text((24, size[1] // 2 - 8), "missing", fill=(230, 230, 230, 255), font=ImageFont.load_default())
    return base


def draw_label(
    panel: Image.Image,
    title: str,
    subtitle: str = "",
    layer: str = "",
    accent: tuple[int, int, int] = (120, 220, 255),
    number: int | None = None,
) -> None:
    draw = ImageDraw.Draw(panel)
    font = ImageFont.load_default()
    draw.rectangle((0, 0, panel.width, 42), fill=(0, 0, 0, 165))
    if number is not None:
        draw.rounded_rectangle((8, 7, 27, 27), radius=3, outline=accent + (255,), width=1, fill=(12, 12, 22, 230))
        draw.text((14, 11), str(number), fill=accent + (255,), font=font)
        x0 = 36
    else:
        x0 = 8
    draw.text((x0, 8), title.upper(), fill=accent + (255,), font=font)
    if subtitle:
        draw.text((x0, 23), subtitle, fill=(232, 228, 245, 255), font=font)
    if layer:
        box = draw.textbbox((0, 0), layer, font=font)
        lx = max(x0, panel.width - (box[2] - box[0]) - 10)
        draw.text((lx, 8), layer, fill=(185, 190, 215, 220), font=font)
    draw.rectangle((0, 0, panel.width - 1, panel.height - 1), outline=accent + (180,), width=1)


def draw_storyboard_background(size: tuple[int, int], title: str, subtitle: str) -> Image.Image:
    img = Image.new("RGBA", size, (3, 4, 12, 255))
    draw = ImageDraw.Draw(img)
    font = ImageFont.load_default()
    # Subtle observatory grid: diagnostic atmosphere without hiding data.
    for x in range(0, size[0], 48):
        draw.line((x, 0, x, size[1]), fill=(22, 34, 58, 90), width=1)
    for y in range(0, size[1], 48):
        draw.line((0, y, size[0], y), fill=(22, 34, 58, 90), width=1)
    draw.rectangle((0, 0, size[0], 78), fill=(5, 6, 16, 245))
    draw.text((24, 14), title.upper(), fill=(245, 248, 255, 255), font=font)
    draw.text((24, 34), "Grant Sanderson geometry alignment  |  transport observability  |  topology first", fill=(194, 194, 215, 255), font=font)
    draw.text((24, 54), subtitle, fill=(120, 230, 255, 255), font=font)
    return img


def normalize_panel(panel: Any) -> dict[str, Any]:
    if isinstance(panel, dict):
        return panel
    title, subtitle, image_path = panel
    return {
        "title": title,
        "subtitle": subtitle,
        "image_path": image_path,
        "layer": "",
        "accent": (120, 220, 255),
    }


def make_storyboard(
    path: Path,
    panels: list[Any],
    title: str = "xPRIMEray diagnostic storyboard",
    subtitle: str = STORYBOARD_TAGLINE,
) -> None:
    cols, rows = 3, 2
    pad = 18
    gap = 10
    header = 82
    footer = 58
    width = pad * 2 + PANEL_SIZE[0] * cols + gap * (cols - 1)
    height = header + PANEL_SIZE[1] * rows + gap * (rows - 1) + footer
    sheet = draw_storyboard_background((width, height), title, subtitle)
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()
    for idx, raw_panel in enumerate(panels[: cols * rows]):
        panel_data = normalize_panel(raw_panel)
        x = pad + (idx % cols) * (PANEL_SIZE[0] + gap)
        y = header + (idx // cols) * (PANEL_SIZE[1] + gap)
        accent = tuple(panel_data.get("accent") or (120, 220, 255))
        panel = fit_image(panel_data.get("image_path"))
        draw_label(
            panel,
            str(panel_data.get("title", "")),
            str(panel_data.get("subtitle", "")),
            str(panel_data.get("layer", "")),
            accent,  # type: ignore[arg-type]
            idx + 1,
        )
        sheet.alpha_composite(panel, (x, y))
    footer_y = height - footer + 12
    principles = [
        "1. establish geometry",
        "2. add transport vectors",
        "3. reveal ownership topology",
        "4. diagnose budget/islands",
        "5. track lineage/phase",
    ]
    draw.text((pad, footer_y), "Visual hierarchy: " + "  |  ".join(principles), fill=(215, 215, 235, 255), font=font)
    draw.text((pad, footer_y + 18), "Post-process only. No render scheduling, hit selection, shading, resolver, or adaptive precision consumes these images.", fill=(165, 170, 190, 255), font=font)
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(path)


def first_existing(paths: list[Path | None]) -> Path | None:
    for path in paths:
        if path and path.exists():
            return path
    return None


def storyboard_image_metrics(path: Path | None) -> dict[str, Any]:
    if not path or not path.exists():
        return {"exists": False, "mean_luma": 0.0, "contrast": 0.0}
    try:
        with Image.open(path) as img:
            gray = img.convert("L").resize((64, 36))
            if hasattr(gray, "get_flattened_data"):
                vals = list(gray.get_flattened_data())  # Pillow 14+
            else:
                vals = list(gray.getdata())
    except Exception:
        return {"exists": True, "mean_luma": 0.0, "contrast": 0.0}
    if not vals:
        return {"exists": True, "mean_luma": 0.0, "contrast": 0.0}
    mean = sum(vals) / len(vals)
    variance = sum((v - mean) ** 2 for v in vals) / len(vals)
    return {"exists": True, "mean_luma": round(mean, 3), "contrast": round(math.sqrt(variance), 3)}


def representative_cell_score(root: Path, role: str, step: str) -> dict[str, Any]:
    cell_dir = root / role / "steps" / f"step_{step}"
    metrics = graph_metrics(cell_dir)
    budget = load_json(cell_dir / "budget_exhaustion_summary.json")
    hit_csv = find_one(cell_dir, "*.hit_diagnostics.csv")
    hit_count = 0
    total_rows = 0
    if hit_csv:
        for row in load_csv(hit_csv):
            total_rows += 1
            if parse_bool(row.get("had_hit")):
                hit_count += 1
    overlays = {
        "cartesian": (cell_dir / "layer1_cartesian_wireframe.png").exists(),
        "hit_normals": (cell_dir / "hit_normal_vector_overlay.png").exists(),
        "continuity": (cell_dir / "layer5_transport_continuity_vectors.png").exists(),
        "ownership": (cell_dir / "ownership_graph_seam_map.png").exists(),
        "budget": (cell_dir / "budget_exhaustion_heatmap.png").exists(),
        "unresolved": (cell_dir / "unstable_subgraph_overlay.png").exists(),
        "quad_panel": (cell_dir / "diagnostic_quad_panel.png").exists(),
    }
    beauty = find_beauty(cell_dir)
    image_metrics = storyboard_image_metrics(beauty)
    graph_nodes = parse_int(metrics.get("graph_node_count"), 0)
    graph_edges = parse_int(metrics.get("graph_edge_count"), 0)
    seam_length = parse_int(metrics.get("seam_length_px_total"), 0)
    unresolved = parse_int(metrics.get("unresolved_pixel_count"), 0)
    budget_pct = parse_float(budget.get("budget_exhaustion_percent"), 0.0)

    score = 0.0
    reasons: list[str] = []
    if beauty and image_metrics["contrast"] > 2.0:
        score += 12.0
        reasons.append("visible_render")
    if hit_count > 0:
        score += min(30.0, math.log10(hit_count + 1.0) * 9.0)
        reasons.append("visible_hits")
    else:
        score -= 35.0
        reasons.append("low_hit_cell")
    if graph_nodes > 0:
        score += min(18.0, graph_nodes * 3.0)
        reasons.append("ownership_nodes")
    if graph_edges > 0:
        score += min(20.0, graph_edges * 5.0)
        reasons.append("seam_edges")
    if seam_length > 0:
        score += min(14.0, math.log10(seam_length + 1.0) * 4.0)
        reasons.append("seam_support")
    overlay_count = sum(1 for ok in overlays.values() if ok)
    score += overlay_count * 4.0
    if overlay_count:
        reasons.append("nonempty_overlays")
    if unresolved > 0 and hit_count > 0:
        score += 5.0
        reasons.append("informative_unresolved_signal")
    if budget_pct >= 75.0:
        score -= 40.0
        reasons.append("mostly_budget_exhausted")
    elif budget_pct > 0.0:
        score -= min(18.0, budget_pct * 1.5)
        reasons.append("budget_exhaustion_penalty")

    return {
        "role": role,
        "step_length": step,
        "cell_dir": str(cell_dir),
        "score": round(score, 4),
        "hit_count": hit_count,
        "hit_row_count": total_rows,
        "graph_node_count": graph_nodes,
        "graph_edge_count": graph_edges,
        "seam_length_px_total": seam_length,
        "unresolved_pixel_count": unresolved,
        "budget_exhaustion_percent": budget_pct if math.isfinite(budget_pct) else "",
        "overlay_availability": overlays,
        "image_metrics": image_metrics,
        "selection_reasons": reasons,
    }


def choose_representative_step(root: Path, role: str, steps: list[str], fallback_step: str) -> dict[str, Any]:
    candidates = [representative_cell_score(root, role, step) for step in steps]
    candidates = [row for row in candidates if Path(row["cell_dir"]).exists()]
    if not candidates:
        return {
            "role": role,
            "selected_step": fallback_step,
            "score": 0.0,
            "selection_reasons": ["fallback_primary_step"],
            "candidates": [],
        }
    selected = max(candidates, key=lambda row: (parse_float(row.get("score"), -9999.0), -parse_float(row.get("budget_exhaustion_percent"), 0.0)))
    return {
        "role": role,
        "selected_step": selected["step_length"],
        "score": selected["score"],
        "selection_reasons": selected["selection_reasons"],
        "candidates": candidates,
    }


def panel_item(title: str, subtitle: str, image_path: Path | None, layer: str, accent: tuple[int, int, int]) -> dict[str, Any]:
    return {
        "title": title,
        "subtitle": subtitle,
        "image_path": image_path,
        "layer": layer,
        "accent": accent,
    }


def make_cell_storyboard(cell_dir: Path, role_root: Path, root: Path | None = None) -> None:
    phase_plot = root / "transport_quality_phase_plot.png" if root else None
    panels = [
        panel_item("Rendered Frame", "what the camera sees", find_beauty(cell_dir), "Level 0 Beauty", (170, 120, 255)),
        panel_item("Cartesian Wireframe", "Layer 1 geometry anchor", cell_dir / "layer1_cartesian_wireframe.png", "Level 1 Geometry", (20, 230, 255)),
        panel_item("Hit Normals / Vectors", "surface orientation and local transport", first_existing([
            cell_dir / "hit_normal_vector_overlay.png",
            cell_dir / "layer5_transport_continuity_vectors.png",
        ]), "Level 2 Transport", (100, 255, 170)),
        panel_item("Ownership Graph Seams", "topology boundaries and domains", cell_dir / "ownership_graph_seam_map.png", "Level 3 Topology", (255, 215, 30)),
        panel_item("Budget / Islands", "quality limits and unresolved support", first_existing([
            cell_dir / "budget_exhaustion_heatmap.png",
            cell_dir / "budget_exhaustion_overlay.png",
            cell_dir / "unstable_subgraph_overlay.png",
            cell_dir / "layer3_risk_probe_markers.png",
        ]), "Level 4 Quality", (255, 80, 120)),
        panel_item("Lineage / Phase", "evolution across step levels", first_existing([
            role_root / "graph_ladder" / "graph_persistence_lineage.png",
            phase_plot,
            role_root / "graph_ladder" / "graph_persistence_ladder.png",
        ]), "Step Evolution", (170, 90, 255)),
    ]
    make_storyboard(
        cell_dir / "diagnostic_storyboard.png",
        panels,
        title="xPRIMEray diagnostic storyboard",
        subtitle=f"{cell_dir.parent.parent.name} / {cell_dir.name} - {STORYBOARD_TAGLINE}",
    )


def make_root_storyboard(root: Path, primary_step: str, storyboard_selection: dict[str, Any] | None = None) -> None:
    curved_step_value = primary_step
    control_step_value = primary_step
    if storyboard_selection:
        curved_step_value = str((storyboard_selection.get("curved") or {}).get("selected_step") or primary_step)
        control_step_value = str((storyboard_selection.get("control") or {}).get("selected_step") or primary_step)
    control_step = root / "control" / "steps" / f"step_{control_step_value}"
    curved_step = root / "curved" / "steps" / f"step_{curved_step_value}"
    curved_oracle = root / "curved" / "oracle"
    panels = [
        panel_item("Rendered Reference", f"control step {control_step_value}", find_beauty(control_step), "Level 0 Beauty", (170, 120, 255)),
        panel_item("Cartesian Wireframe", f"curved step {curved_step_value}", curved_step / "layer1_cartesian_wireframe.png", "Level 1 Geometry", (20, 230, 255)),
        panel_item("Hit Normals / Vectors", "curved transport orientation", first_existing([
            curved_step / "hit_normal_vector_overlay.png",
            curved_step / "layer5_transport_continuity_vectors.png",
        ]), "Level 2 Transport", (100, 255, 170)),
        panel_item("Ownership Graph Seams", "curved topology boundaries", curved_step / "ownership_graph_seam_map.png", "Level 3 Topology", (255, 215, 30)),
        panel_item("Budget / Islands", "constraints and unresolved support", first_existing([
            curved_step / "budget_exhaustion_heatmap.png",
            curved_oracle / "epsilon_stability_map.png",
            curved_oracle / "unstable_subgraph_overlay.png",
            curved_step / "unstable_subgraph_overlay.png",
        ]), "Level 4 Quality", (255, 80, 120)),
        panel_item("Graph Lineage / Phase", "step evolution", first_existing([
            root / "curved" / "graph_ladder" / "graph_persistence_lineage.png",
            root / "curved" / "graph_ladder" / "merge_split_overlay.png",
            root / "transport_quality_phase_plot.png",
        ]), "Step Evolution", (170, 90, 255)),
    ]
    make_storyboard(
        root / "curved_vs_control_storyboard.png",
        panels,
        title="xPRIMEray curved/control diagnostic storyboard",
        subtitle=(
            f"representative steps: control={control_step_value}, curved={curved_step_value} - "
            f"{STORYBOARD_TAGLINE}"
        ),
    )


def phase_lookup(phase_rows: list[dict[str, Any]]) -> dict[tuple[str, str], dict[str, Any]]:
    return {(str(row.get("role", "")), str(row.get("step_length", ""))): row for row in phase_rows}


def evolution_artifact(root: Path, role: str, step: str, kind: str) -> Path | None:
    cell = root / role / "steps" / f"step_{step}"
    if kind == "topology":
        return first_existing([
            cell / "ownership_graph_seam_map.png",
            cell / "layer2_transport_ownership.png",
            cell / "transport_shape_regions_overlay.png",
        ])
    if kind == "transport_phase":
        return first_existing([
            cell / "layer5_transport_continuity_vectors.png",
            cell / "hit_normal_vector_overlay.png",
            cell / "diagnostic_overlay_contact_sheet.png",
        ])
    if kind == "budget":
        return first_existing([
            cell / "budget_exhaustion_heatmap.png",
            cell / "budget_exhaustion_overlay.png",
            cell / "unstable_subgraph_overlay.png",
            cell / "layer3_risk_probe_markers.png",
        ])
    if kind == "storyboard":
        return first_existing([
            cell / "diagnostic_storyboard.png",
            cell / "diagnostic_quad_panel.png",
            cell / "diagnostic_overlay_contact_sheet.png",
        ])
    return None


def annotated_evolution_frame(
    image_path: Path | None,
    role: str,
    step: str,
    kind: str,
    phase_row: dict[str, Any],
    size: tuple[int, int] = EVOLUTION_PANEL_SIZE,
) -> Image.Image:
    panel = fit_image(image_path, size)
    draw = ImageDraw.Draw(panel)
    font = ImageFont.load_default()
    phase = str(phase_row.get("transport_quality_phase", ""))
    color = PHASE_COLORS.get(phase, PHASE_COLORS[""])
    budget = parse_float(phase_row.get("budget_exhaustion_percent"), 0.0)
    unresolved = parse_int(phase_row.get("unresolved_count"), 0)
    meta = load_json(image_path.parent / "metadata.json") if image_path and image_path.exists() else {}
    frames = meta.get("frames", "")
    marker = ""
    if budget > 0:
        marker = f" budget {budget:.2f}%"
    elif unresolved > 0:
        marker = f" unresolved {unresolved}"
    elif phase:
        marker = f" {phase}"
    draw.rectangle((0, 0, size[0], 34), fill=(0, 0, 0, 190))
    draw.text((8, 6), f"{role} step {step}", fill=(245, 248, 255, 255), font=font)
    draw.rounded_rectangle((8, 20, min(size[0] - 8, 112), 31), radius=2, fill=color + (230,))
    draw.text((14, 20), phase or "unclassified", fill=(0, 0, 0, 235), font=font)
    if marker:
        draw.text((118, 20), marker.strip(), fill=(240, 235, 245, 255), font=font)
    if frames != "":
        draw.rectangle((size[0] - 68, size[1] - 18, size[0] - 5, size[1] - 5), fill=(0, 0, 0, 170))
        draw.text((size[0] - 64, size[1] - 17), f"frames {frames}", fill=(210, 218, 240, 255), font=font)
    draw.rectangle((0, 0, size[0] - 1, size[1] - 1), outline=color + (190,), width=1)
    if not image_path or not image_path.exists():
        draw.text((28, size[1] // 2), f"{kind} unavailable", fill=(225, 225, 230, 255), font=font)
    return panel


def make_evolution_strip(
    path: Path,
    root: Path,
    roles: list[str],
    steps: list[str],
    kind: str,
    phase_rows: list[dict[str, Any]],
    title: str,
) -> dict[str, Any]:
    lookup = phase_lookup(phase_rows)
    pad = 16
    gap = 8
    header = 58
    label_w = 76
    cols = max(1, len(steps))
    rows = max(1, len(roles))
    width = pad * 2 + label_w + cols * EVOLUTION_PANEL_SIZE[0] + (cols - 1) * gap
    height = header + rows * EVOLUTION_PANEL_SIZE[1] + (rows - 1) * gap + 24
    sheet = draw_storyboard_background((width, height), title, STORYBOARD_TAGLINE)
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()
    frames_used = 0
    missing: list[str] = []
    for col, step in enumerate(steps):
        x = pad + label_w + col * (EVOLUTION_PANEL_SIZE[0] + gap)
        draw.text((x + 8, header - 18), f"step {step}", fill=(210, 230, 255, 255), font=font)
    for row_idx, role in enumerate(roles):
        y = header + row_idx * (EVOLUTION_PANEL_SIZE[1] + gap)
        draw.text((pad, y + 8), role, fill=(245, 248, 255, 255), font=font)
        for col, step in enumerate(steps):
            artifact = evolution_artifact(root, role, step, kind)
            if artifact and artifact.exists():
                frames_used += 1
            else:
                missing.append(f"{role}/step_{step}/{kind}")
            frame = annotated_evolution_frame(artifact, role, step, kind, lookup.get((role, step), {}))
            x = pad + label_w + col * (EVOLUTION_PANEL_SIZE[0] + gap)
            sheet.alpha_composite(frame, (x, y))
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(path)
    return {
        "path": str(path),
        "kind": kind,
        "roles": roles,
        "steps": steps,
        "frames_used": frames_used,
        "missing": missing,
    }


def image_difference_score(prev: Path | None, cur: Path | None) -> float:
    if not prev or not cur or not prev.exists() or not cur.exists():
        return 0.0
    try:
        with Image.open(prev) as a, Image.open(cur) as b:
            aa = a.convert("L").resize((48, 27))
            bb = b.convert("L").resize((48, 27))
            av = list(aa.getdata())
            bv = list(bb.getdata())
    except Exception:
        return 0.0
    if not av or len(av) != len(bv):
        return 0.0
    return sum(abs(x - y) for x, y in zip(av, bv)) / (255.0 * len(av))


def temporal_role_score(root: Path, role: str, steps: list[str], phase_rows: list[dict[str, Any]]) -> dict[str, Any]:
    lookup = phase_lookup(phase_rows)
    present = 0
    topology_delta = 0.0
    phase_values: set[str] = set()
    budget_values: list[float] = []
    prev_topology: Path | None = None
    for step in steps:
        cell = root / role / "steps" / f"step_{step}"
        if cell.exists():
            present += 1
        row = lookup.get((role, step), {})
        phase_values.add(str(row.get("transport_quality_phase", "")))
        budget_values.append(parse_float(row.get("budget_exhaustion_percent"), 0.0))
        topology = evolution_artifact(root, role, step, "topology")
        if prev_topology and topology:
            topology_delta += image_difference_score(prev_topology, topology)
        if topology:
            prev_topology = topology
    budget_range = (max(budget_values) - min(budget_values)) if budget_values else 0.0
    score = present * 8.0 + topology_delta * 35.0 + max(0, len(phase_values) - 1) * 10.0 + min(20.0, budget_range * 3.0)
    return {
        "role": role,
        "score": round(score, 5),
        "step_count": present,
        "topology_delta_score": round(topology_delta, 5),
        "phase_diversity": sorted(v for v in phase_values if v),
        "budget_range": round(budget_range, 6),
        "selection_factors": [
            "best_temporal_coherence",
            "strongest_visible_evolution",
            "highest_explanatory_value",
        ],
    }


def choose_temporal_evolution_role(root: Path, steps: list[str], phase_rows: list[dict[str, Any]]) -> dict[str, Any]:
    candidates = [temporal_role_score(root, role, steps, phase_rows) for role in ("curved", "control")]
    selected = max(candidates, key=lambda row: (parse_float(row.get("score"), -9999.0), 1 if row.get("role") == "curved" else 0))
    return {
        "selected_role": selected["role"],
        "selection_reason": "highest combined temporal coherence, visible evolution, and explanatory value score",
        "candidates": candidates,
    }


def make_evolution_gif(
    path: Path,
    root: Path,
    role: str,
    steps: list[str],
    kind: str,
    phase_rows: list[dict[str, Any]],
    duration_ms: int = 850,
) -> dict[str, Any]:
    lookup = phase_lookup(phase_rows)
    frames: list[Image.Image] = []
    missing: list[str] = []
    for step in steps:
        artifact = evolution_artifact(root, role, step, kind)
        if not artifact:
            missing.append(f"{role}/step_{step}/{kind}")
        frame = annotated_evolution_frame(artifact, role, step, kind, lookup.get((role, step), {}), PANEL_SIZE)
        frames.append(frame.convert("RGB"))
    path.parent.mkdir(parents=True, exist_ok=True)
    if frames:
        frames[0].save(path, save_all=True, append_images=frames[1:], duration=duration_ms, loop=0)
    return {
        "path": str(path),
        "role": role,
        "kind": kind,
        "frame_count": len(frames),
        "duration_ms": duration_ms,
        "missing": missing,
        "interpolation": "none_real_frames_only",
    }


def build_temporal_evolution_outputs(root: Path, steps: list[str], phase_rows: list[dict[str, Any]]) -> dict[str, Any]:
    roles = ["control", "curved"]
    role_selection = choose_temporal_evolution_role(root, steps, phase_rows)
    selected_role = str(role_selection.get("selected_role", "curved"))
    strips = {
        "topology_evolution_strip": make_evolution_strip(
            root / "topology_evolution_strip.png",
            root,
            roles,
            steps,
            "topology",
            phase_rows,
            "Topology evolution strip",
        ),
        "transport_phase_evolution_strip": make_evolution_strip(
            root / "transport_phase_evolution_strip.png",
            root,
            roles,
            steps,
            "transport_phase",
            phase_rows,
            "Transport phase evolution strip",
        ),
        "budget_evolution_strip": make_evolution_strip(
            root / "budget_evolution_strip.png",
            root,
            roles,
            steps,
            "budget",
            phase_rows,
            "Budget / unresolved evolution strip",
        ),
    }
    gifs = {
        "ownership_graph_evolution": make_evolution_gif(
            root / "ownership_graph_evolution.gif",
            root,
            selected_role,
            steps,
            "topology",
            phase_rows,
        ),
        "budget_heatmap_evolution": make_evolution_gif(
            root / "budget_heatmap_evolution.gif",
            root,
            selected_role,
            steps,
            "budget",
            phase_rows,
        ),
        "diagnostic_storyboard_evolution": make_evolution_gif(
            root / "diagnostic_storyboard_evolution.gif",
            root,
            selected_role,
            steps,
            "storyboard",
            phase_rows,
            duration_ms=1200,
        ),
    }
    summary = {
        "schema_version": STORYBOARD_SCHEMA_VERSION,
        "visual_sequence": STORYBOARD_TAGLINE,
        "real_frames_only": True,
        "fake_interpolation": False,
        "evolution_axes": {
            "step_ladder": True,
            "frame_progression": False,
            "frame_progression_reason": "current ladder artifacts expose final per-step captures, not per-frame image sequences",
            "convergence_stages": True,
        },
        "steps": steps,
        "roles": roles,
        "temporal_role_selection": role_selection,
        "strips": strips,
        "animated_outputs": gifs,
        "annotations": [
            "role",
            "frame_count_when_available",
            "step",
            "transport_quality_phase",
            "budget_saturation_marker",
            "unresolved_marker",
        ],
    }
    (root / "temporal_observability_summary.json").write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n")
    return summary


def write_readme(root: Path, summary: dict[str, Any]) -> None:
    lines = [
        "# How to read this validation ladder",
        "",
        "This packet is renderer-validation instrumentation, not a physics proof.",
        "",
        "## Visual story",
        "",
        "- `curved_vs_control_storyboard.png`: representative six-panel story using the sequence geometry -> transport -> topology -> quality/budget.",
        "- `topology_evolution_strip.png`, `transport_phase_evolution_strip.png`, and `budget_evolution_strip.png`: step-ladder evolution strips built from real rendered diagnostic artifacts.",
        "- `ownership_graph_evolution.gif`, `budget_heatmap_evolution.gif`, and `diagnostic_storyboard_evolution.gif`: animated observability outputs using real step frames only; no fake interpolation.",
        "- Per-cell `diagnostic_storyboard.png`: rendered frame, Cartesian wireframe, hit normals/vectors, ownership seams, budget/islands, and lineage/phase.",
        "- Per-cell `diagnostic_quad_panel.png`: rendered frame, hit-normal overlay, camera cross-section minimap, and available transport/field overlay.",
        "",
        "## Visual hierarchy principles",
        "",
        "1. Establish Cartesian geometry projection first. This gives the observer a stable coordinate-space anchor.",
        "2. Add hit normals and transport vectors second, so transport behavior is read against the geometry anchor.",
        "3. Reveal ownership topology and seams third.",
        "4. Diagnose budget saturation and unresolved islands fourth.",
        "5. Track graph lineage and phase evolution last.",
        "",
        "## Representative frame selection",
        "",
        "- Prefer cells with visible geometry, visible hits, coherent transport structure, informative topology, and non-empty overlays.",
        "- Avoid low-hit cells, visually empty cells, mostly budget-exhausted cells, and unresolved samples with no observable geometry.",
        "- Selection details are written to `storyboard_selection.json`.",
        "- Temporal role selection is written to `temporal_observability_summary.json` and scores best temporal coherence, strongest visible evolution, and explanatory value.",
        "",
        "## Evidence tiers",
        "",
        "- Tier A: fixture curvature engaged. The curved fixture log must include nonzero curvature evidence and enabled curved transport.",
        "- Tier B: renderer diagnostics changed. This can include image hash, graph, seam, normal, or visible band/support metric changes.",
        "- Tier C: topology changed across the step ladder. This comes from graph persistence, merge/split, appear/disappear, or seam changes.",
        "- Tier D: unresolved island sealed or persisted. This comes from ReferenceTransportOracle/island outputs.",
        "",
        "## Guardrail",
        "",
        f"{GUARDRAIL}",
        "",
        "Use 'associated with curved transport fixture under tested settings' unless the comparison metrics justify stronger causal language.",
        "",
        "## Current status",
        "",
        f"- Comparability status: {summary.get('comparability_status', '')}",
        f"- Control comparison type: {summary.get('control_comparison_type', '')}",
        f"- Control comparison reason: {summary.get('control_comparison_reason', '')}",
        f"- Curved validation status: {summary.get('curved_validation_status', '')}",
    ]
    (root / "README.md").write_text("\n".join(lines) + "\n")


def analyze(root: Path, repo_root: Path) -> dict[str, Any]:
    root.mkdir(parents=True, exist_ok=True)
    run_meta = load_json(root / "run_metadata.json")
    steps = [s for s in str(run_meta.get("step_ladder", "")).split(",") if s]
    primary_step = str(run_meta.get("primary_step", steps[0] if steps else "0.015"))
    requested_control_mode = str(run_meta.get("requested_control_mode", "scene_control"))
    control_comparison_type = str(run_meta.get("control_comparison_type", "scene_control"))
    control_comparison_reason = str(run_meta.get("control_comparison_reason", "configured scene-control fixture"))
    inventory = candidate_inventory(repo_root, root)
    write_csv(root / "curved_fixture_inventory.csv", inventory, [
        "scene_path",
        "field_node_present",
        "curvature_or_grin_params_nonzero",
        "nonzero_param_evidence",
        "curved_transport_enabled_hint",
        "hit_diagnostics_available",
        "prior_outputs_available",
        "prior_output_example",
    ])
    (root / "curved_fixture_inventory.json").write_text(json.dumps(inventory, indent=2, sort_keys=True) + "\n")
    (root / "curved_fixture_inventory.md").write_text(
        "# Curved Fixture Inventory\n\n"
        "| Scene | Field node? | Nonzero curvature/GRIN hint? | Curved transport hint? | Prior outputs? |\n"
        "|---|---:|---:|---:|---:|\n"
        + "\n".join(
            f"| `{row['scene_path']}` | {row['field_node_present']} | {row['curvature_or_grin_params_nonzero']} | {row['curved_transport_enabled_hint']} | {row['prior_outputs_available']} |"
            for row in inventory
        )
        + "\n"
    )

    curved_primary_log = root / "curved" / "steps" / f"step_{primary_step}" / "run.log"
    curvature_evidence = parse_curvature_evidence(curved_primary_log)
    comparability = compare_configs(root, steps, control_comparison_type, control_comparison_reason)
    if not curvature_evidence["valid"]:
        comparability["comparability_status"] = "invalid"
    delta = graph_delta_vs_control(root, primary_step)
    curved_metrics = read_role_step_metrics(root, "curved", primary_step)
    control_metrics = read_role_step_metrics(root, "control", primary_step)
    ladder_budget_rows = collect_ladder_step_metrics(root, "control", steps) + collect_ladder_step_metrics(root, "curved", steps)
    phase_rows, phase_summary = annotate_transport_quality_phases(root, ladder_budget_rows, steps)
    draw_transport_quality_phase_plot(root / "transport_quality_phase_plot.png", phase_rows, steps)
    write_csv(root / "transport_quality_phase.csv", phase_rows, [
        "role",
        "step_length",
        "transport_quality_phase",
        "recommended_next_action",
        "hit_count",
        "hit_count_delta_prev",
        "unresolved_count",
        "unresolved_delta_prev",
        "graph_node_count",
        "graph_node_delta_prev",
        "graph_edge_count",
        "graph_edge_delta_prev",
        "seam_length_px_total",
        "seam_length_delta_prev",
        "merge_split_count",
        "merge_split_delta_prev",
        "node_persistence_rate",
        "budget_exhausted_pixel_count",
        "budget_exhaustion_percent",
        "oracle_comparison_count",
        "oracle_stable_count",
        "oracle_unresolved_count",
        "oracle_stability_rate",
        "oracle_stability_delta_prev",
        "oracle_agreement_rate",
        "oracle_agreement_delta_prev",
    ])
    write_csv(root / "curved_ladder_budget_saturation.csv", phase_rows, [
        "role",
        "step_length",
        "transport_quality_phase",
        "hit_count",
        "unresolved_count",
        "graph_node_count",
        "graph_edge_count",
        "seam_length_px_total",
        "merge_split_count",
        "budget_exhausted_pixel_count",
        "budget_exhaustion_percent",
        "step_quality_plateau_candidate",
    ])
    budget_diminishing_returns = {
        "control": infer_diminishing_returns([r for r in ladder_budget_rows if r.get("role") == "control"]),
        "curved": infer_diminishing_returns([r for r in ladder_budget_rows if r.get("role") == "curved"]),
    }
    curved_oracle_summary = load_json(root / "curved" / "oracle" / "unresolved_island_summary.json")
    oracle_packet = next(iter(sorted((root / "curved" / "oracle").glob("*.reference_transport_oracle.json"))), None)
    oracle_json = load_json(oracle_packet)
    beauty_hashes = {
        "control": sha256_file(find_beauty(root / "control" / "steps" / f"step_{primary_step}")),
        "curved": sha256_file(find_beauty(root / "curved" / "steps" / f"step_{primary_step}")),
    }
    diagnostics_changed = any(v != 0 for v in delta.values()) or (beauty_hashes["control"] and beauty_hashes["curved"] and beauty_hashes["control"] != beauty_hashes["curved"])
    merge_split_count = count_rows(root / "curved" / "graph_ladder" / "transport_ownership_graph_merges_splits.csv")
    persistence_count = count_rows(root / "curved" / "graph_ladder" / "transport_ownership_graph_persistence.csv")
    sealed = parse_bool(curved_oracle_summary.get("sealed_at_0.00625"))
    unresolved_at_ref = parse_int(curved_oracle_summary.get("unresolved_at_0.003125_count"), 0)
    evidence_tiers = {
        "tier_a_fixture_curvature_engaged": "pass" if curvature_evidence["valid"] else "invalid",
        "tier_b_renderer_diagnostics_changed": "pass" if diagnostics_changed else "not_detected",
        "tier_c_topology_changed_across_step_ladder": "pass" if merge_split_count or persistence_count else "not_detected",
        "tier_d_unresolved_island_sealed_or_persisted": "sealed" if sealed else ("persisted" if unresolved_at_ref > 0 else "unknown"),
    }
    validation_status = "invalid" if not curvature_evidence["valid"] else ("warning" if comparability["warnings"] else "valid")

    storyboard_selection = {
        "schema_version": STORYBOARD_SCHEMA_VERSION,
        "visual_hierarchy": VISUAL_HIERARCHY,
        "selection_policy": {
            "prefer": [
                "visible geometry",
                "visible hit samples",
                "coherent transport structure",
                "nonempty topology overlays",
                "informative seams or unresolved support",
            ],
            "avoid": [
                "low-hit cells",
                "visually empty cells",
                "mostly budget-exhausted cells",
                "unresolved samples without observable geometry",
            ],
        },
        "control": choose_representative_step(root, "control", steps, primary_step),
        "curved": choose_representative_step(root, "curved", steps, primary_step),
    }
    (root / "storyboard_selection.json").write_text(json.dumps(storyboard_selection, indent=2, sort_keys=True) + "\n")

    for role in ("control", "curved"):
        role_root = root / role
        for cell in sorted((role_root / "steps").glob("step_*")):
            make_cell_storyboard(cell, role_root, root)
    make_root_storyboard(root, primary_step, storyboard_selection)
    temporal_observability = build_temporal_evolution_outputs(root, steps, phase_rows)

    rows = [
        {
            "role": "control",
            "primary_step": primary_step,
            "requested_control_mode": requested_control_mode,
            "control_comparison_type": control_comparison_type,
            "control_comparison_reason": control_comparison_reason,
            "beauty_hash": beauty_hashes["control"],
            **control_metrics,
        },
        {
            "role": "curved",
            "primary_step": primary_step,
            "requested_control_mode": requested_control_mode,
            "control_comparison_type": control_comparison_type,
            "control_comparison_reason": control_comparison_reason,
            "beauty_hash": beauty_hashes["curved"],
            **curved_metrics,
        },
    ]
    write_csv(root / "curved_ladder_summary.csv", rows, [
        "role",
        "primary_step",
        "requested_control_mode",
        "control_comparison_type",
        "control_comparison_reason",
        "beauty_hash",
        "step_dir",
        "graph_node_count",
        "graph_edge_count",
        "seam_length_px_total",
        "unresolved_pixel_count",
        "high_discontinuity_edge_count",
        "budget_exhausted_pixel_count",
        "budget_exhausted_hit_count",
        "budget_exhausted_no_hit_count",
        "budget_exhaustion_percent",
        "step_quality_plateau_candidate",
        "merge_split_count",
        "ladder_persistence_basis",
        "node_persistence_rate",
    ])

    summary = {
        "study": "curved_field_validation_ladder",
        "guardrail": GUARDRAIL,
        "visible_support_wording": VISIBLE_SUPPORT_WORDING,
        "curved_validation_status": validation_status,
        "comparability_status": comparability["comparability_status"],
        "requested_control_mode": requested_control_mode,
        "control_comparison_type": control_comparison_type,
        "control_comparison_reason": control_comparison_reason,
        "comparability_warnings": comparability["warnings"],
        "curvature_evidence": curvature_evidence,
        "validation_inference": {
            "diagnostics_changed_vs_control": diagnostics_changed,
            "ownership_topology_differs_from_control": any(delta[k] != 0 for k in ("node_count_delta", "edge_count_delta", "seam_length_delta")),
            "unresolved_island_summary_available": bool(curved_oracle_summary),
            "oracle_comparison_count": oracle_json.get("comparison_count", ""),
        },
        "evidence_tiers": evidence_tiers,
        "graph_delta_vs_control": delta,
        "budget_saturation_ladder_csv": str(root / "curved_ladder_budget_saturation.csv"),
        "budget_diminishing_returns": budget_diminishing_returns,
        "transport_quality_phase_csv": str(root / "transport_quality_phase.csv"),
        "transport_quality_phase_plot": str(root / "transport_quality_phase_plot.png"),
        "transport_quality_phase_summary": phase_summary,
        "storyboard_selection": storyboard_selection,
        "temporal_observability": temporal_observability,
        "visual_hierarchy": VISUAL_HIERARCHY,
        "beauty_hashes": beauty_hashes,
        "curved_metrics": curved_metrics,
        "control_metrics": control_metrics,
    }
    (root / "curved_ladder_summary.json").write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n")
    (root / "comparability_report.json").write_text(json.dumps(comparability, indent=2, sort_keys=True) + "\n")
    write_readme(root, summary)

    md = [
        "# Curved-Field Validation Ladder Summary",
        "",
        "Renderer-validation grounded. This report does not make physical-truth claims.",
        "",
        f"- Curved validation status: **{validation_status}**",
        f"- Comparability status: **{comparability['comparability_status']}**",
        f"- Requested control mode: **{requested_control_mode}**",
        f"- Control comparison type: **{control_comparison_type}**",
        f"- Control comparison reason: {control_comparison_reason}",
        f"- Storyboard: `curved_vs_control_storyboard.png`",
        f"- Storyboard selection: `storyboard_selection.json`",
        f"- Temporal observability: `topology_evolution_strip.png`, `transport_phase_evolution_strip.png`, `budget_evolution_strip.png`",
        f"- Animated observability: `ownership_graph_evolution.gif`, `budget_heatmap_evolution.gif`, `diagnostic_storyboard_evolution.gif`",
        "",
        "## Visual Hierarchy",
        "",
        "- The storyboard sequence is geometry -> transport -> topology -> quality/budget.",
        "- Cartesian wireframe projection is treated as the foundational coordinate-space anchor.",
        "- Representative frames are selected by visible geometry, hit support, topology signal, overlay availability, and budget-exhaustion penalties.",
        "- Temporal outputs use real captured step artifacts only. No cinematic interpolation or inferred in-between frames are generated.",
        "",
        "## Evidence Tiers",
        "",
    ]
    for key, value in evidence_tiers.items():
        md.append(f"- {key}: {value}")
    md.extend([
        "",
        "## Curvature Evidence",
        "",
        f"- curvature metric log present: {curvature_evidence['curvature_metric_log_present']}",
        f"- resolved log present: {curvature_evidence['resolved_log_present']}",
        f"- nonzero curvature params: {curvature_evidence['nonzero_curvature_params']}",
        f"- curved transport enabled: {curvature_evidence['curved_transport_enabled']}",
        f"- status reason: {curvature_evidence['reason']}",
        "",
        "## Validation Inference",
        "",
        f"- diagnostics changed vs control: {diagnostics_changed}",
        f"- graph_delta_vs_control: `{json.dumps(delta, sort_keys=True)}`",
        f"- budget diminishing returns: `{json.dumps(budget_diminishing_returns, sort_keys=True)}`",
        f"- transport quality phases: `{json.dumps(phase_summary, sort_keys=True)}`",
        f"- storyboard representatives: control step `{storyboard_selection['control'].get('selected_step')}`, curved step `{storyboard_selection['curved'].get('selected_step')}`",
        f"- temporal evolution role: `{temporal_observability['temporal_role_selection'].get('selected_role')}`",
        f"- oracle comparisons: {oracle_json.get('comparison_count', '')}",
        f"- budget saturation ladder: `curved_ladder_budget_saturation.csv`",
        f"- transport quality phase plot: `transport_quality_phase_plot.png`",
        "",
        "## Phase Interpretation",
        "",
        "- `underresolved`: transport evidence is still missing or disagrees with oracle/sample diagnostics; reduce step size or focus the oracle/island microscope.",
        "- `converging`: graph, seam, hit, or oracle metrics are still changing; continue the ladder around neighboring step values.",
        "- `plateau`: metrics are locally stable without budget saturation; treat this as a candidate operating window or diminishing-returns region.",
        "- `budget_saturated`: traversal budget exhaustion is present; increase max traversal/step budget or use adaptive budget scaling before trusting smaller-step conclusions.",
        "",
        "## Comparability Warnings",
        "",
    ])
    if comparability["warnings"]:
        for warning in comparability["warnings"][:40]:
            md.append(f"- step={warning.get('step')} field={warning.get('field')} control={warning.get('control')} curved={warning.get('curved')}")
    else:
        md.append("- none")
    md.extend(["", "## Guardrail", "", GUARDRAIL, ""])
    (root / "curved_ladder_summary.md").write_text("\n".join(md) + "\n")
    return summary


def format_phase_counts(counts: dict[str, Any]) -> str:
    ordered = ["underresolved", "converging", "plateau", "budget_saturated"]
    return " ".join(f"{phase}={parse_int(counts.get(phase), 0)}" for phase in ordered)


def top_recommended_action(rows: list[dict[str, str]], role: str) -> str:
    priority = ["budget_saturated", "underresolved", "converging", "plateau"]
    role_rows = [row for row in rows if row.get("role") == role]
    for phase in priority:
        for row in role_rows:
            if row.get("transport_quality_phase") == phase and row.get("recommended_next_action"):
                return str(row.get("recommended_next_action"))
    for row in role_rows:
        if row.get("recommended_next_action"):
            return str(row.get("recommended_next_action"))
    return ""


def print_terminal_summary(root: Path, summary: dict[str, Any]) -> None:
    phase_summary = summary.get("transport_quality_phase_summary") or {}
    phase_rows = load_csv(root / "transport_quality_phase.csv")
    print(f"[output] {root}")
    print(f"[status] curved_validation={summary.get('curved_validation_status', '')}")
    print(f"[control] comparison_type={summary.get('control_comparison_type', '')}")
    for role in ("curved", "control"):
        role_summary = phase_summary.get(role) or {}
        print(f"[plateau] {role}: {role_summary.get('plateau_start_step', '')}")
        print(f"[budget-saturation] {role}: {role_summary.get('budget_saturation_start_step', '')}")
    for role in ("curved", "control"):
        role_summary = phase_summary.get(role) or {}
        print(f"[transport-quality] {role}: {format_phase_counts(role_summary.get('phase_counts') or {})}")
    for role in ("curved", "control"):
        print(f"[next-action] {role}: {top_recommended_action(phase_rows, role)}")
    print("[plot] transport_quality_phase_plot.png")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    args = parser.parse_args()
    summary = analyze(args.root, args.repo_root)
    print(
        f"[curved-ladder-analysis] root={args.root} "
        f"status={summary['curved_validation_status']} comparability={summary['comparability_status']}"
    )
    print_terminal_summary(args.root, summary)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
