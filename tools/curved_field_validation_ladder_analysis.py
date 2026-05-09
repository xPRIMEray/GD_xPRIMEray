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


def draw_label(panel: Image.Image, title: str, subtitle: str = "") -> None:
    draw = ImageDraw.Draw(panel)
    font = ImageFont.load_default()
    for y, text in ((8, title), (24, subtitle)):
        if not text:
            continue
        box = draw.textbbox((8, y), text, font=font)
        draw.rectangle((box[0] - 3, box[1] - 2, box[2] + 3, box[3] + 2), fill=(0, 0, 0, 190))
        draw.text((8, y), text, fill=(245, 248, 255, 255), font=font)


def make_storyboard(path: Path, panels: list[tuple[str, str, Path | None]]) -> None:
    cols, rows = 3, 2
    sheet = Image.new("RGBA", (PANEL_SIZE[0] * cols, PANEL_SIZE[1] * rows), (0, 0, 0, 255))
    for idx, (title, subtitle, image_path) in enumerate(panels[: cols * rows]):
        panel = fit_image(image_path)
        draw_label(panel, title, subtitle)
        sheet.alpha_composite(panel, ((idx % cols) * PANEL_SIZE[0], (idx // cols) * PANEL_SIZE[1]))
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(path)


def first_existing(paths: list[Path | None]) -> Path | None:
    for path in paths:
        if path and path.exists():
            return path
    return None


def make_cell_storyboard(cell_dir: Path, role_root: Path) -> None:
    panels = [
        ("render", cell_dir.name, find_beauty(cell_dir)),
        ("hit normals", "x/z projection", cell_dir / "hit_normal_vector_overlay.png"),
        ("graph seams", "ownership edges", cell_dir / "ownership_graph_seam_map.png"),
        ("unstable overlay", "if detected", first_existing([cell_dir / "unstable_subgraph_overlay.png", cell_dir / "layer3_risk_probe_markers.png"])),
        ("lineage", "role step ladder", role_root / "graph_ladder" / "graph_persistence_lineage.png"),
        ("quad panel", "diagnostic cockpit", cell_dir / "diagnostic_quad_panel.png"),
    ]
    make_storyboard(cell_dir / "diagnostic_storyboard.png", panels)


def make_root_storyboard(root: Path, primary_step: str) -> None:
    control_step = root / "control" / "steps" / f"step_{primary_step}"
    curved_step = root / "curved" / "steps" / f"step_{primary_step}"
    curved_oracle = root / "curved" / "oracle"
    panels = [
        ("1 control render", primary_step, find_beauty(control_step)),
        ("2 curved render", primary_step, find_beauty(curved_step)),
        ("3 curved hit normals", "x/z projection", curved_step / "hit_normal_vector_overlay.png"),
        ("4 ownership graph seams", "curved", curved_step / "ownership_graph_seam_map.png"),
        ("5 unresolved island", "oracle/island overlay", first_existing([
            curved_oracle / "epsilon_stability_map.png",
            curved_oracle / "unstable_subgraph_overlay.png",
            curved_step / "unstable_subgraph_overlay.png",
        ])),
        ("6 graph lineage", "merge/split view", first_existing([
            root / "curved" / "graph_ladder" / "graph_persistence_lineage.png",
            root / "curved" / "graph_ladder" / "merge_split_overlay.png",
        ])),
    ]
    make_storyboard(root / "curved_vs_control_storyboard.png", panels)


def write_readme(root: Path, summary: dict[str, Any]) -> None:
    lines = [
        "# How to read this validation ladder",
        "",
        "This packet is renderer-validation instrumentation, not a physics proof.",
        "",
        "## Visual story",
        "",
        "- `curved_vs_control_storyboard.png`: control render, curved render, curved hit normals, ownership seams, unresolved island overlay, and graph lineage.",
        "- Per-cell `diagnostic_quad_panel.png`: rendered frame, hit-normal overlay, camera cross-section minimap, and available transport/field overlay.",
        "- Per-cell `diagnostic_storyboard.png`: local six-panel cockpit for that capture cell.",
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
    write_csv(root / "curved_ladder_budget_saturation.csv", ladder_budget_rows, [
        "role",
        "step_length",
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

    for role in ("control", "curved"):
        role_root = root / role
        for cell in sorted((role_root / "steps").glob("step_*")):
            make_cell_storyboard(cell, role_root)
    make_root_storyboard(root, primary_step)

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
        f"- oracle comparisons: {oracle_json.get('comparison_count', '')}",
        f"- budget saturation ladder: `curved_ladder_budget_saturation.csv`",
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
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
