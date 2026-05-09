#!/usr/bin/env python3
"""Generate hit-normal overlays and a descriptive report for ownership graph packets."""

from __future__ import annotations

import argparse
import csv
import json
import math
import shutil
import tempfile
from pathlib import Path
from types import SimpleNamespace
from typing import Any

import hit_normal_vector_overlay as hno


DIAGNOSTIC_PNG_NAMES = {
    "ownership_graph_node_map.png",
    "ownership_graph_seam_map.png",
    "unstable_subgraph_overlay.png",
    "graph_persistence_ladder.png",
    "graph_persistence_lineage.png",
    "merge_split_overlay.png",
    "transport_ownership_graph_validation_report.md",
}


def load_json(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {}
    try:
        return json.loads(path.read_text())
    except Exception:
        return {}


def load_csv(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        return []
    with path.open(newline="", encoding="utf-8-sig") as handle:
        return list(csv.DictReader(handle))


def parse_int(value: Any, default: int = 0) -> int:
    try:
        if value in ("", None):
            return default
        return int(round(float(value)))
    except Exception:
        return default


def parse_float(value: Any, default: float = math.nan) -> float:
    try:
        if value in ("", None, "nan", "NaN"):
            return default
        return float(value)
    except Exception:
        return default


def parse_bbox(text: str | None) -> tuple[int, int, int, int] | None:
    if not text:
        return None
    parts = [int(round(float(p))) for p in text.replace(";", ",").split(",") if p.strip()]
    if len(parts) != 4:
        raise argparse.ArgumentTypeError("bbox must be x0,y0,x1,y1")
    x0, y0, x1, y1 = parts
    return min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)


def fmt_bbox(bbox: tuple[int, int, int, int] | None) -> str:
    if not bbox:
        return ""
    return ",".join(str(v) for v in bbox)


def clamp_bbox(bbox: tuple[int, int, int, int], pad: int = 0) -> tuple[int, int, int, int]:
    return max(0, bbox[0] - pad), max(0, bbox[1] - pad), max(0, bbox[2] + pad), max(0, bbox[3] + pad)


def bbox_from_rows(rows: list[dict[str, str]]) -> tuple[int, int, int, int] | None:
    boxes = []
    points = []
    for row in rows:
        if all(row.get(k, "") != "" for k in ("bbox_x0", "bbox_y0", "bbox_x1", "bbox_y1")):
            boxes.append((parse_int(row["bbox_x0"]), parse_int(row["bbox_y0"]), parse_int(row["bbox_x1"]), parse_int(row["bbox_y1"])))
        elif row.get("x", "") != "" and row.get("y", "") != "":
            x, y = parse_int(row["x"]), parse_int(row["y"])
            points.append((x, y))
    if boxes:
        return min(b[0] for b in boxes), min(b[1] for b in boxes), max(b[2] for b in boxes), max(b[3] for b in boxes)
    if points:
        return min(p[0] for p in points), min(p[1] for p in points), max(p[0] for p in points), max(p[1] for p in points)
    return None


def find_image_for_csv(hit_csv: Path) -> Path | None:
    stem = hit_csv.name.replace(".hit_diagnostics.csv", "")
    exact = hit_csv.parent / f"{stem}.png"
    if exact.exists():
        return exact
    layer = hit_csv.parent / "layer0_beauty.png"
    if layer.exists():
        return layer
    candidates = []
    for path in hit_csv.parent.glob("*.png"):
        if path.name in DIAGNOSTIC_PNG_NAMES or "overlay" in path.name or path.name.startswith("layer"):
            continue
        candidates.append(path)
    candidates.sort(key=lambda p: p.stat().st_size, reverse=True)
    return candidates[0] if candidates else None


def find_source_paths(folder: Path, graph: dict[str, Any], image_arg: Path | None, hit_csv_arg: Path | None) -> tuple[Path | None, Path | None, list[str]]:
    notes: list[str] = []
    if hit_csv_arg:
        hit_csv = hit_csv_arg
        image = image_arg or find_image_for_csv(hit_csv)
        return image, hit_csv, notes
    if image_arg:
        notes.append("Image was provided without hit CSV; hit CSV will be auto-discovered.")

    nodes = graph.get("nodes", [])
    candidates: list[Path] = []
    if nodes:
        node = nodes[0]
        step = str(node.get("source_step_length", ""))
        traversal = str(node.get("traversal", ""))
        stride = str(node.get("stride", ""))
        if step and traversal:
            candidates.extend(sorted(folder.glob(f"**/step_{step}/{traversal}_stride_{stride}/*.hit_diagnostics.csv")))
    candidates.extend(sorted(folder.glob("*.hit_diagnostics.csv")))
    candidates.extend(sorted(folder.glob("**/*.hit_diagnostics.csv")))
    seen: set[Path] = set()
    unique = []
    for path in candidates:
        if path not in seen:
            unique.append(path)
            seen.add(path)
    hit_csv = unique[0] if unique else None
    image = image_arg or (find_image_for_csv(hit_csv) if hit_csv else None)
    if not hit_csv:
        notes.append("No hit diagnostics CSV found; normal overlays cannot be generated.")
    if not image:
        notes.append("No rendered frame PNG found; normal overlays cannot be generated.")
    return image, hit_csv, notes


def run_overlay(
    image: Path,
    hit_csv: Path,
    out_png: Path,
    stride: int,
    scale: float,
    bbox: tuple[int, int, int, int] | None,
    mode: str,
    projection: str,
    flip_y: int,
    min_projected_magnitude: float,
) -> dict[str, Any]:
    with tempfile.TemporaryDirectory(prefix="xprime_hit_normals_") as tmp:
        tmpdir = Path(tmp)
        args = SimpleNamespace(
            image=image,
            hit_csv=hit_csv,
            compare_csv=None,
            roi_bbox=bbox,
            stride=stride,
            scale=scale,
            mode=mode,
            projection=projection,
            flip_y=flip_y,
            min_projected_magnitude=min_projected_magnitude,
            debug_normals=0,
            width=1,
            mark_no_hit=1,
            out=tmpdir,
        )
        hno.analyze(args)
        shutil.copyfile(tmpdir / "hit_normal_vector_overlay.png", out_png)
        return load_json(tmpdir / "hit_normal_vector_overlay_summary.json")


def normal_angle(a: dict[str, Any], b: dict[str, Any]) -> float:
    ax, ay, az = a["normal_x"], a["normal_y"], a["normal_z"]
    bx, by, bz = b["normal_x"], b["normal_y"], b["normal_z"]
    la = math.sqrt(ax * ax + ay * ay + az * az)
    lb = math.sqrt(bx * bx + by * by + bz * bz)
    if la <= 1e-9 or lb <= 1e-9:
        return 0.0
    dot = max(-1.0, min(1.0, (ax * bx + ay * by + az * bz) / (la * lb)))
    return math.degrees(math.acos(dot))


def load_hit_rows(path: Path) -> dict[tuple[int, int], dict[str, Any]]:
    return hno.load_hit_rows(path)


def node_normal_coherence(nodes: list[dict[str, Any]], rows: dict[tuple[int, int], dict[str, Any]]) -> list[dict[str, Any]]:
    out = []
    for node in nodes:
        bbox = (
            parse_int(node.get("bbox_x0")),
            parse_int(node.get("bbox_y0")),
            parse_int(node.get("bbox_x1")),
            parse_int(node.get("bbox_y1")),
        )
        samples = [
            row for (x, y), row in rows.items()
            if bbox[0] <= x <= bbox[2] and bbox[1] <= y <= bbox[3] and row["had_hit"]
        ]
        if len(samples) < 2:
            out.append({
                "node_id": node.get("node_id", ""),
                "hit_sample_count": len(samples),
                "mean_normal_angle_from_mean_deg": "",
                "max_normal_angle_from_mean_deg": "",
                "coherence_label": "no-hit-or-insufficient-samples",
            })
            continue
        mx = sum(s["normal_x"] for s in samples) / len(samples)
        my = sum(s["normal_y"] for s in samples) / len(samples)
        mz = sum(s["normal_z"] for s in samples) / len(samples)
        mean_row = {"normal_x": mx, "normal_y": my, "normal_z": mz}
        angles = [normal_angle(s, mean_row) for s in samples]
        mean_angle = sum(angles) / len(angles)
        max_angle = max(angles)
        label = "coherent" if mean_angle <= 5.0 and max_angle <= 20.0 else ("mixed" if mean_angle <= 20.0 else "incoherent")
        out.append({
            "node_id": node.get("node_id", ""),
            "hit_sample_count": len(samples),
            "mean_normal_angle_from_mean_deg": round(mean_angle, 6),
            "max_normal_angle_from_mean_deg": round(max_angle, 6),
            "coherence_label": label,
        })
    return out


def seam_alignment(edges: list[dict[str, Any]]) -> dict[str, Any]:
    if not edges:
        return {
            "edge_count": 0,
            "edges_with_normal_delta": 0,
            "max_normal_angle_delta": "",
            "assessment": "No graph seam edges were present in this graph packet.",
        }
    deltas = [parse_float(e.get("max_normal_angle_delta")) for e in edges if e.get("max_normal_angle_delta", "") != ""]
    high = [d for d in deltas if d >= 15.0]
    return {
        "edge_count": len(edges),
        "edges_with_normal_delta": len(deltas),
        "max_normal_angle_delta": round(max(deltas), 6) if deltas else "",
        "assessment": "Some seam edges align with visible normal discontinuities." if high else "No strong seam/normal-discontinuity alignment was measured.",
    }


def write_report(folder: Path, report: dict[str, Any]) -> None:
    lines = [
        "# Graph Plus Hit Normals Report",
        "",
        "Post-process diagnostic report only. This does not modify renderer behavior and does not claim physical truth.",
        "",
        "## Outputs",
        "",
    ]
    for key in ("full_frame_hit_normals", "roi_hit_normals", "unstable_subgraph_hit_normals", "merge_split_hit_normals"):
        value = report.get("outputs", {}).get(key, "")
        lines.append(f"- {key}: `{value}`" if value else f"- {key}: not generated")
    lines.extend(["", "## Normal Coherence Inside Ownership Nodes", ""])
    if report["node_coherence"]:
        lines.append("| node | hit samples | mean angle | max angle | label |")
        lines.append("|---:|---:|---:|---:|---|")
        for row in report["node_coherence"][:40]:
            lines.append(f"| {row['node_id']} | {row['hit_sample_count']} | {row['mean_normal_angle_from_mean_deg']} | {row['max_normal_angle_from_mean_deg']} | {row['coherence_label']} |")
    else:
        lines.append("- No node coherence rows available.")
    lines.extend(["", "## Seam Alignment", ""])
    seam = report["seam_alignment"]
    lines.append(f"- Edge count: {seam['edge_count']}")
    lines.append(f"- Edges with normal deltas: {seam['edges_with_normal_delta']}")
    lines.append(f"- Max normal-angle delta: {seam['max_normal_angle_delta']}")
    lines.append(f"- Assessment: {seam['assessment']}")
    lines.extend(["", "## Unresolved Islands", ""])
    lines.append(f"- Unstable rows: {report['unstable_row_count']}")
    lines.append(f"- Assessment: {report['unstable_assessment']}")
    lines.extend(["", "## Merge/Split Regions", ""])
    lines.append(f"- Merge/split rows: {report['merge_split_row_count']}")
    lines.append(f"- Assessment: {report['merge_split_assessment']}")
    if report.get("notes"):
        lines.extend(["", "## Notes", ""])
        for note in report["notes"]:
            lines.append(f"- {note}")
    (folder / "graph_plus_hit_normals_report.md").write_text("\n".join(lines) + "\n")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("graph_output_folder", type=Path)
    parser.add_argument("--image", type=Path, default=None)
    parser.add_argument("--hit-csv", type=Path, default=None)
    parser.add_argument("--stride", type=int, default=8)
    parser.add_argument("--scale", type=float, default=12.0)
    parser.add_argument("--mode", choices=["fixed", "magnitude"], default="fixed")
    parser.add_argument("--projection", choices=["xy", "xz", "yz"], default="xy")
    parser.add_argument("--flip-y", type=int, default=1)
    parser.add_argument("--min-projected-magnitude", type=float, default=1e-6)
    parser.add_argument("--roi-bbox", type=parse_bbox, default=None)
    args = parser.parse_args()

    folder = args.graph_output_folder
    graph = load_json(folder / "transport_ownership_graph.json")
    metrics = load_json(folder / "transport_ownership_graph_metrics.json")
    nodes = load_csv(folder / "transport_ownership_graph_nodes.csv") or graph.get("nodes", [])
    edges = load_csv(folder / "transport_ownership_graph_edges.csv") or graph.get("edges", [])
    unstable = load_csv(folder / "unstable_subgraphs.csv")
    merge_split = load_csv(folder / "transport_ownership_graph_merges_splits.csv")
    image, hit_csv, notes = find_source_paths(folder, graph, args.image, args.hit_csv)
    outputs: dict[str, str] = {}
    overlay_summaries: dict[str, Any] = {}

    if image and hit_csv:
        overlay_summaries["full_frame"] = run_overlay(image, hit_csv, folder / "full_frame_hit_normals.png", args.stride, args.scale, None, args.mode, args.projection, args.flip_y, args.min_projected_magnitude)
        outputs["full_frame_hit_normals"] = "full_frame_hit_normals.png"
        roi_bbox = args.roi_bbox or parse_bbox(fmt_bbox(tuple(metrics.get("roi_bbox", []))) if metrics.get("roi_bbox") else None)
        if roi_bbox:
            overlay_summaries["roi"] = run_overlay(image, hit_csv, folder / "roi_hit_normals.png", max(1, min(args.stride, 4)), args.scale, roi_bbox, args.mode, args.projection, args.flip_y, args.min_projected_magnitude)
            outputs["roi_hit_normals"] = "roi_hit_normals.png"
        unstable_bbox = bbox_from_rows(unstable)
        if unstable_bbox or (folder / "unstable_subgraphs.csv").exists():
            bbox = clamp_bbox(unstable_bbox or roi_bbox, 2) if (unstable_bbox or roi_bbox) else None
            overlay_summaries["unstable"] = run_overlay(image, hit_csv, folder / "unstable_subgraph_hit_normals.png", max(1, min(args.stride, 4)), args.scale, bbox, args.mode, args.projection, args.flip_y, args.min_projected_magnitude)
            outputs["unstable_subgraph_hit_normals"] = "unstable_subgraph_hit_normals.png"
        if merge_split:
            merge_bbox = bbox_from_rows(nodes) or roi_bbox
            overlay_summaries["merge_split"] = run_overlay(image, hit_csv, folder / "merge_split_hit_normals.png", max(1, min(args.stride, 4)), args.scale, merge_bbox, args.mode, args.projection, args.flip_y, args.min_projected_magnitude)
            outputs["merge_split_hit_normals"] = "merge_split_hit_normals.png"

    hit_rows = load_hit_rows(hit_csv) if hit_csv else {}
    node_coherence = node_normal_coherence(nodes, hit_rows) if hit_rows else []
    seam = seam_alignment(edges)
    abnormal_nodes = [n for n in node_coherence if n["coherence_label"] in {"mixed", "incoherent"}]
    unstable_assessment = (
        "No unstable subgraph rows were present; no abnormal island normal behavior can be assessed."
        if not unstable else
        "Unstable subgraph rows exist; inspect unstable_subgraph_hit_normals.png for local normal behavior."
    )
    merge_split_assessment = (
        "No merge/split rows were present in this graph packet."
        if not merge_split else
        "Merge/split rows exist; inspect merge_split_hit_normals.png to distinguish visible geometric discontinuity from transport-only topology changes."
    )
    if abnormal_nodes and unstable:
        unstable_assessment += " Some ownership nodes show mixed/incoherent visible normals."

    report = {
        "folder": str(folder),
        "image": str(image) if image else "",
        "hit_csv": str(hit_csv) if hit_csv else "",
        "outputs": outputs,
        "overlay_summaries": overlay_summaries,
        "node_coherence": node_coherence,
        "seam_alignment": seam,
        "unstable_row_count": len(unstable),
        "unstable_assessment": unstable_assessment,
        "merge_split_row_count": len(merge_split),
        "merge_split_assessment": merge_split_assessment,
        "notes": notes,
        "post_process_only": True,
    }
    (folder / "graph_plus_hit_normals_summary.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n")
    write_report(folder, report)
    print(f"[graph-plus-hit-normals] folder={folder} outputs={','.join(outputs) or 'none'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
