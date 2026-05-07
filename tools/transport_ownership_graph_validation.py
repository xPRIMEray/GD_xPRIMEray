#!/usr/bin/env python3
"""Validate Transport Ownership Graph extractor outputs."""

from __future__ import annotations

import argparse
import csv
import json
from pathlib import Path
from typing import Any


def load_csv(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        return []
    with path.open(newline="", encoding="utf-8-sig") as handle:
        return list(csv.DictReader(handle))


def load_json(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {}
    try:
        return json.loads(path.read_text())
    except Exception:
        return {}


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


def add_check(checks: list[dict[str, Any]], check_id: str, status: str, message: str, details: dict[str, Any] | None = None) -> None:
    checks.append({
        "check_id": check_id,
        "status": status,
        "message": message,
        "details": details or {},
    })


def find_hit_csv(folder: Path) -> Path | None:
    matches = sorted(folder.glob("*.hit_diagnostics.csv"))
    return matches[0] if matches else None


def count_labeled_pixels_from_hit_csv(path: Path | None, metrics: dict[str, Any], graph_json: dict[str, Any]) -> int | None:
    if path is None or not path.exists():
        candidates: list[Path] = []
        for node in graph_json.get("nodes", []):
            step = str(node.get("source_step_length", ""))
            traversal = str(node.get("traversal", ""))
            stride = str(node.get("stride", ""))
            if step and traversal:
                candidates.extend(sorted(Path(graph_json.get("_folder", ".")).glob(f"**/step_{step}/{traversal}_stride_{stride}/*.hit_diagnostics.csv")))
        path = candidates[0] if candidates else None
    if path is None or not path.exists():
        return None
    roi = metrics.get("roi_bbox")
    rows = load_csv(path)
    if not roi:
        return len(rows)
    try:
        x0, y0, x1, y1 = [int(v) for v in roi]
    except Exception:
        return None
    count = 0
    for row in rows:
        x, y = parse_int(row.get("x"), -999999), parse_int(row.get("y"), -999999)
        if x0 <= x <= x1 and y0 <= y <= y1:
            count += 1
    return count


def validate(folder: Path) -> dict[str, Any]:
    nodes_path = folder / "transport_ownership_graph_nodes.csv"
    edges_path = folder / "transport_ownership_graph_edges.csv"
    persistence_path = folder / "transport_ownership_graph_persistence.csv"
    unstable_path = folder / "unstable_subgraphs.csv"
    metrics_path = folder / "transport_ownership_graph_metrics.json"
    lineage_path = folder / "graph_persistence_lineage.png"
    graph_packet_path = folder / "transport_ownership_graph.json"

    nodes = load_csv(nodes_path)
    edges = load_csv(edges_path)
    persistence = load_csv(persistence_path)
    unstable = load_csv(unstable_path)
    metrics = load_json(metrics_path)
    graph_json = load_json(graph_packet_path)
    graph_json["_folder"] = str(folder)
    checks: list[dict[str, Any]] = []

    node_ids = [parse_int(row.get("node_id"), -1) for row in nodes]
    node_id_set = set(node_ids)
    duplicate_node_ids = sorted({node_id for node_id in node_ids if node_ids.count(node_id) > 1})
    metric_node_count = parse_int(metrics.get("graph_node_count"), len(nodes))
    metric_edge_count = parse_int(metrics.get("graph_edge_count"), len(edges))

    if metrics_path.exists():
        add_check(checks, "metrics_exists", "pass", "Metrics JSON exists.")
    else:
        add_check(checks, "metrics_exists", "fail", "Missing transport_ownership_graph_metrics.json.")

    if nodes_path.exists():
        add_check(checks, "nodes_csv_exists", "pass", "Nodes CSV exists.")
    else:
        add_check(checks, "nodes_csv_exists", "fail", "Missing transport_ownership_graph_nodes.csv.")

    if edges_path.exists():
        add_check(checks, "edges_csv_exists", "pass", "Edges CSV exists.")
    else:
        add_check(checks, "edges_csv_exists", "fail", "Missing transport_ownership_graph_edges.csv.")

    if duplicate_node_ids:
        add_check(checks, "node_ids_unique", "fail", "Node IDs contain duplicates.", {"duplicate_node_ids": duplicate_node_ids})
    else:
        add_check(checks, "node_ids_unique", "pass", "Node IDs are unique.")

    if metric_node_count == len(nodes):
        add_check(checks, "node_count_matches_metrics", "pass", "Node row count matches graph_node_count.", {"count": len(nodes)})
    else:
        add_check(checks, "node_count_matches_metrics", "fail", "Node row count does not match graph_node_count.", {"rows": len(nodes), "metric": metric_node_count})

    if metric_edge_count == len(edges):
        add_check(checks, "edge_count_matches_metrics", "pass", "Edge row count matches graph_edge_count.", {"count": len(edges)})
    else:
        add_check(checks, "edge_count_matches_metrics", "fail", "Edge row count does not match graph_edge_count.", {"rows": len(edges), "metric": metric_edge_count})

    invalid_edges = []
    self_edges = []
    for row in edges:
        edge_id = row.get("edge_id", "")
        a, b = parse_int(row.get("node_a"), -1), parse_int(row.get("node_b"), -1)
        if a == b:
            self_edges.append(edge_id)
        if a not in node_id_set or b not in node_id_set:
            invalid_edges.append(edge_id)
    if invalid_edges:
        add_check(checks, "edges_reference_valid_nodes", "fail", "Some edges reference missing node IDs.", {"edge_ids": invalid_edges})
    else:
        add_check(checks, "edges_reference_valid_nodes", "pass", "Every edge references valid node IDs.")
    if self_edges:
        add_check(checks, "edges_not_self_edges", "fail", "Some edges connect a node to itself.", {"edge_ids": self_edges})
    else:
        add_check(checks, "edges_not_self_edges", "pass", "No self-edges found.")

    negative_areas = [row.get("node_id", "") for row in nodes if parse_int(row.get("area_px"), 0) <= 0]
    if negative_areas:
        add_check(checks, "node_areas_positive", "fail", "Some nodes have non-positive area.", {"node_ids": negative_areas})
    else:
        add_check(checks, "node_areas_positive", "pass", "All node areas are positive or no nodes are present.")

    full_frame_graph_basis = parse_bool(metrics.get("full_frame_graph_basis"))
    oracle_sample_only = parse_bool(metrics.get("oracle_sample_only"))
    area_total = sum(parse_int(row.get("area_px"), 0) for row in nodes)
    hit_pixel_count = count_labeled_pixels_from_hit_csv(find_hit_csv(folder), metrics, graph_json)
    if full_frame_graph_basis:
        if hit_pixel_count is None:
            add_check(checks, "node_area_matches_labeled_pixels", "warn", "Full-frame graph basis set, but hit diagnostics were not available to verify pixel area.")
        elif area_total == hit_pixel_count:
            add_check(checks, "node_area_matches_labeled_pixels", "pass", "Node area total matches labeled hit-diagnostic pixels.", {"area_total": area_total, "labeled_pixels": hit_pixel_count})
        else:
            add_check(checks, "node_area_matches_labeled_pixels", "fail", "Node area total does not match labeled hit-diagnostic pixels.", {"area_total": area_total, "labeled_pixels": hit_pixel_count})
    else:
        add_check(checks, "node_area_matches_labeled_pixels", "skip", "No full-frame graph basis; node area total is not a full-frame claim.")

    unresolved_metric = parse_int(metrics.get("unresolved_pixel_count"), 0)
    if unstable and "classification" in unstable[0]:
        unresolved_rows = sum(1 for row in unstable if row.get("classification") not in {"", "stable", "plateaued"})
    elif unstable and "is_unresolved_island_member" in unstable[0]:
        unresolved_rows = sum(1 for row in unstable if parse_int(row.get("is_unresolved_island_member"), 0) != 0)
    else:
        unresolved_rows = 0
    if unresolved_rows == unresolved_metric:
        add_check(checks, "unstable_subgraphs_match_unresolved_metric", "pass", "unstable_subgraphs.csv agrees with unresolved_pixel_count.", {"unresolved_rows": unresolved_rows})
    else:
        add_check(checks, "unstable_subgraphs_match_unresolved_metric", "fail", "unstable_subgraphs.csv does not agree with unresolved_pixel_count.", {"unresolved_rows": unresolved_rows, "metric": unresolved_metric})

    persistence_basis = str(metrics.get("persistence_basis", "unavailable") or "unavailable")
    if persistence and persistence_basis == "unavailable":
        add_check(checks, "persistence_basis_allows_rows", "fail", "Persistence rows exist while persistence_basis is unavailable.", {"rows": len(persistence)})
    elif persistence:
        add_check(checks, "persistence_basis_allows_rows", "pass", "Persistence rows exist with a non-unavailable basis.", {"basis": persistence_basis, "rows": len(persistence)})
    else:
        add_check(checks, "persistence_basis_allows_rows", "pass", "No persistence rows exist.")

    sealed = str(metrics.get("sampled_topology_sealed_step", "") or "")
    if sealed and not full_frame_graph_basis and not oracle_sample_only:
        add_check(checks, "sampled_topology_sealed_step_scope", "fail", "sampled_topology_sealed_step exists without graph or oracle basis.", {"sampled_topology_sealed_step": sealed})
    elif sealed and oracle_sample_only:
        add_check(checks, "sampled_topology_sealed_step_scope", "pass", "Sealed step is explicitly sample-only under oracle_sample_only=true.", {"sampled_topology_sealed_step": sealed})
    elif sealed and full_frame_graph_basis:
        add_check(checks, "sampled_topology_sealed_step_scope", "pass", "Sealed step is reported as sampled while dense hit diagnostics exist.", {"sampled_topology_sealed_step": sealed})
    else:
        add_check(checks, "sampled_topology_sealed_step_scope", "pass", "No sampled sealed step reported.")

    if persistence and lineage_path.exists():
        add_check(checks, "lineage_png_for_persistence", "pass", "graph_persistence_lineage.png exists for multi-step persistence.")
    elif persistence:
        add_check(checks, "lineage_png_for_persistence", "fail", "Persistence rows exist but graph_persistence_lineage.png is missing.")
    else:
        add_check(checks, "lineage_png_for_persistence", "skip", "No persistence rows; lineage PNG not required.")

    if oracle_sample_only and full_frame_graph_basis:
        add_check(checks, "confidence_flags_mutually_consistent", "fail", "oracle_sample_only and full_frame_graph_basis are both true.")
    else:
        add_check(checks, "confidence_flags_mutually_consistent", "pass", "Confidence flags are mutually consistent.")

    status_counts = {status: sum(1 for check in checks if check["status"] == status) for status in ("pass", "warn", "fail", "skip")}
    overall_status = "fail" if status_counts["fail"] else ("warn" if status_counts["warn"] else "pass")
    return {
        "folder": str(folder),
        "overall_status": overall_status,
        "status_counts": status_counts,
        "metrics": {
            "graph_node_count": metric_node_count,
            "graph_edge_count": metric_edge_count,
            "node_area_total": area_total,
            "hit_diagnostic_labeled_pixels": hit_pixel_count,
            "unresolved_pixel_count": unresolved_metric,
            "persistence_row_count": len(persistence),
            "full_frame_graph_basis": full_frame_graph_basis,
            "oracle_sample_only": oracle_sample_only,
            "persistence_basis": persistence_basis,
            "sampled_topology_sealed_step": sealed,
            "lineage_png_exists": lineage_path.exists(),
        },
        "checks": checks,
    }


def write_report(folder: Path, summary: dict[str, Any]) -> None:
    lines = [
        "# Transport Ownership Graph Validation Report",
        "",
        "Post-process validation only. This report does not modify renderer behavior.",
        "",
        f"- Overall status: **{summary['overall_status']}**",
        f"- Folder: `{summary['folder']}`",
        "",
        "## Summary Metrics",
        "",
    ]
    for key, value in summary["metrics"].items():
        lines.append(f"- {key}: {value}")
    lines.extend(["", "## Checks", "", "| Check | Status | Message |", "|---|---|---|"])
    for check in summary["checks"]:
        lines.append(f"| `{check['check_id']}` | {check['status']} | {check['message']} |")
    (folder / "transport_ownership_graph_validation_report.md").write_text("\n".join(lines) + "\n")
    (folder / "transport_ownership_graph_validation_summary.json").write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("graph_output_folder", type=Path)
    args = parser.parse_args()
    folder = args.graph_output_folder
    summary = validate(folder)
    write_report(folder, summary)
    print(f"[transport-ownership-graph-validation] status={summary['overall_status']} folder={folder}")
    return 1 if summary["overall_status"] == "fail" else 0


if __name__ == "__main__":
    raise SystemExit(main())
