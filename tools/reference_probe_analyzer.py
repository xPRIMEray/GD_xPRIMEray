#!/usr/bin/env python3
"""
Analyze reference-precision null geodesic probe CSV artifacts.

Input:
    *.reference_geodesic_probe.csv files, or directories containing them.

Outputs:
    reference_probe_summary.csv
    risk_vs_step_by_anchor.png
    nonconvergent_anchor_report.md
    decision_risk_heatmap.png
    required_precision_heatmap.png
    convergence_class_heatmap.png
    risk_node_map.png
    transport_risk_nodes.csv
    risk_node_report.md
    transport_risk_regions.csv
    risk_region_report.md
    radial_risk_profile_by_node.png
    risk_region_overlay.png
    radial_dist_vs_required_precision.png
"""

from __future__ import annotations

import argparse
import csv
import math
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable

try:
    from PIL import Image, ImageDraw, ImageFont
except Exception as exc:  # pragma: no cover - import guard for clearer CLI error
    raise SystemExit(f"Pillow is required for PNG output: {exc}") from exc


SUMMARY_COLS = [
    "anchor_id",
    "object_id",
    "projected_tile",
    "projected_x",
    "projected_y",
    "object_centroid_projected_x",
    "object_centroid_projected_y",
    "radial_dist_from_object_centroid",
    "radial_angle_from_object_centroid",
    "nearest_anchor_kind",
    "nearest_corner_dist",
    "nearest_edge_dist",
    "radial_offset_x",
    "radial_offset_y",
    "step_length",
    "reference_step_length",
    "sample_count",
    "mean_decision_risk",
    "max_decision_risk",
    "match_rate",
    "matched_count",
    "monotonic_decay",
    "minimum_step_satisfying_epsilon",
    "persistent_mismatch_at_0.00625",
]


@dataclass(frozen=True)
class ProbeRow:
    source: Path
    anchor_id: str
    object_id: str
    step_length: float
    reference_step_length: float
    projected_x: int
    projected_y: int
    projected_tile: str
    object_centroid_x: int
    object_centroid_y: int
    radial_dist_from_object_centroid: float
    radial_angle_from_object_centroid: float
    nearest_anchor_kind: str
    nearest_corner_dist: float
    nearest_edge_dist: float
    offset_x: int
    offset_y: int
    decision_risk: float
    matched_reference: bool
    required_precision_label: str

    @property
    def offset_key(self) -> tuple[str, str, str, int, int, int, int]:
        return (self.anchor_id, self.object_id, self.projected_tile, self.projected_x, self.projected_y, self.offset_x, self.offset_y)

    @property
    def step_key(self) -> tuple[str, str, str, int, int, int, int, float]:
        return (*self.offset_key, self.step_length)


def parse_float(value: str, default: float = math.nan) -> float:
    try:
        return float(str(value).strip())
    except Exception:
        return default


def parse_int(value: str, default: int = 0) -> int:
    try:
        return int(float(str(value).strip()))
    except Exception:
        return default


def parse_bool(value: str) -> bool:
    return str(value).strip().lower() in {"1", "true", "yes", "y", "on"}


def discover_inputs(paths: Iterable[Path]) -> list[Path]:
    files: list[Path] = []
    for path in paths:
        if path.is_dir():
            files.extend(sorted(path.rglob("*.reference_geodesic_probe.csv")))
        elif path.is_file():
            files.append(path)
    seen: set[Path] = set()
    out: list[Path] = []
    for path in files:
        resolved = path.resolve()
        if resolved in seen:
            continue
        seen.add(resolved)
        out.append(path)
    return out


def load_rows(paths: list[Path]) -> list[ProbeRow]:
    rows: list[ProbeRow] = []
    for path in paths:
        with path.open(newline="") as f:
            reader = csv.DictReader(f)
            for raw in reader:
                step = parse_float(raw.get("step_length", ""))
                ref = parse_float(raw.get("reference_step_length", ""))
                risk = parse_float(raw.get("decision_risk", ""))
                if math.isnan(step) or math.isnan(ref) or math.isnan(risk):
                    continue
                rows.append(
                    ProbeRow(
                        source=path,
                        anchor_id=raw.get("anchor_id", ""),
                        object_id=raw.get("object_id", ""),
                        step_length=step,
                        reference_step_length=ref,
                        projected_x=parse_int(raw.get("projected_x", "")),
                        projected_y=parse_int(raw.get("projected_y", "")),
                        projected_tile=raw.get("projected_tile", ""),
                        object_centroid_x=parse_int(raw.get("object_centroid_projected_x", "-1"), -1),
                        object_centroid_y=parse_int(raw.get("object_centroid_projected_y", "-1"), -1),
                        radial_dist_from_object_centroid=parse_float(raw.get("radial_dist_from_object_centroid", "-1"), -1.0),
                        radial_angle_from_object_centroid=parse_float(raw.get("radial_angle_from_object_centroid", "0"), 0.0),
                        nearest_anchor_kind=raw.get("nearest_anchor_kind", ""),
                        nearest_corner_dist=parse_float(raw.get("nearest_corner_dist", "-1"), -1.0),
                        nearest_edge_dist=parse_float(raw.get("nearest_edge_dist", "-1"), -1.0),
                        offset_x=parse_int(raw.get("radial_offset_x", "")),
                        offset_y=parse_int(raw.get("radial_offset_y", "")),
                        decision_risk=risk,
                        matched_reference=parse_bool(raw.get("matched_reference_decision", "")),
                        required_precision_label=raw.get("required_precision_label", ""),
                    )
                )
    return rows


def fmt_float(value: float, digits: int = 6) -> str:
    if value is None or not math.isfinite(value):
        return ""
    return f"{value:.{digits}f}".rstrip("0").rstrip(".")


def offset_groups(rows: list[ProbeRow]) -> dict[tuple[str, str, str, int, int, int, int], list[ProbeRow]]:
    groups: dict[tuple[str, str, str, int, int, int, int], list[ProbeRow]] = defaultdict(list)
    for row in rows:
        groups[row.offset_key].append(row)
    return groups


def risks_by_step(group: list[ProbeRow]) -> dict[float, list[float]]:
    by_step: dict[float, list[float]] = defaultdict(list)
    for row in group:
        by_step[row.step_length].append(row.decision_risk)
    return by_step


def match_rate_by_step(group: list[ProbeRow]) -> dict[float, tuple[int, int]]:
    by_step: dict[float, list[bool]] = defaultdict(list)
    for row in group:
        by_step[row.step_length].append(row.matched_reference)
    return {step: (sum(1 for v in vals if v), len(vals)) for step, vals in by_step.items()}


def mean(values: list[float]) -> float:
    return sum(values) / len(values) if values else math.nan


def monotonic_decay_pass(group: list[ProbeRow], tolerance: float) -> bool:
    by_step = risks_by_step(group)
    ordered = sorted((step, mean(vals)) for step, vals in by_step.items())
    # As step length decreases, risk should not increase beyond tolerance.
    for (_, prev_risk), (_, next_risk) in zip(reversed(ordered), list(reversed(ordered))[1:]):
        if next_risk > prev_risk + tolerance:
            return False
    return True


def min_step_satisfying(group: list[ProbeRow], epsilon: float) -> str:
    by_step = risks_by_step(group)
    passing = [step for step, vals in by_step.items() if mean(vals) <= epsilon]
    if not passing:
        return "none"
    return fmt_float(max(passing))


def persistent_mismatch_at(group: list[ProbeRow], step: float, epsilon: float) -> bool:
    vals = [row for row in group if abs(row.step_length - step) <= 1e-8]
    if not vals:
        return False
    return any((not row.matched_reference) or row.decision_risk > epsilon for row in vals)


def build_summary(rows: list[ProbeRow], epsilon: float, monotonic_tolerance: float) -> list[dict[str, Any]]:
    groups = offset_groups(rows)
    summary: list[dict[str, Any]] = []
    for key, group in sorted(groups.items()):
        anchor_id, object_id, tile, _px, _py, ox, oy = key
        mono = monotonic_decay_pass(group, monotonic_tolerance)
        min_step = min_step_satisfying(group, epsilon)
        persistent = persistent_mismatch_at(group, 0.00625, epsilon)
        by_step = risks_by_step(group)
        matches = match_rate_by_step(group)
        ref_step = group[0].reference_step_length if group else math.nan
        for step in sorted(by_step):
            risks = by_step[step]
            matched, total = matches.get(step, (0, 0))
            summary.append(
                {
                    "anchor_id": anchor_id,
                    "object_id": object_id,
                    "projected_tile": tile,
                    "projected_x": group[0].projected_x,
                    "projected_y": group[0].projected_y,
                    "object_centroid_projected_x": group[0].object_centroid_x,
                    "object_centroid_projected_y": group[0].object_centroid_y,
                    "radial_dist_from_object_centroid": fmt_float(group[0].radial_dist_from_object_centroid),
                    "radial_angle_from_object_centroid": fmt_float(group[0].radial_angle_from_object_centroid),
                    "nearest_anchor_kind": group[0].nearest_anchor_kind,
                    "nearest_corner_dist": fmt_float(group[0].nearest_corner_dist),
                    "nearest_edge_dist": fmt_float(group[0].nearest_edge_dist),
                    "radial_offset_x": ox,
                    "radial_offset_y": oy,
                    "step_length": fmt_float(step),
                    "reference_step_length": fmt_float(ref_step),
                    "sample_count": len(risks),
                    "mean_decision_risk": fmt_float(mean(risks)),
                    "max_decision_risk": fmt_float(max(risks)),
                    "match_rate": fmt_float(matched / total if total else math.nan),
                    "matched_count": matched,
                    "monotonic_decay": "pass" if mono else "fail",
                    "minimum_step_satisfying_epsilon": min_step,
                    "persistent_mismatch_at_0.00625": "yes" if persistent else "no",
                }
            )
    return summary


def write_summary_csv(path: Path, summary: list[dict[str, Any]]) -> None:
    with path.open("w", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=SUMMARY_COLS)
        writer.writeheader()
        for row in summary:
            writer.writerow({key: row.get(key, "") for key in SUMMARY_COLS})


def aggregate_anchor_step_risk(rows: list[ProbeRow]) -> dict[str, dict[float, float]]:
    tmp: dict[str, dict[float, list[float]]] = defaultdict(lambda: defaultdict(list))
    for row in rows:
        tmp[row.anchor_id][row.step_length].append(row.decision_risk)
    return {
        anchor: {step: mean(vals) for step, vals in by_step.items()}
        for anchor, by_step in tmp.items()
    }


def palette(i: int) -> tuple[int, int, int]:
    colors = [
        (42, 111, 151),
        (220, 100, 48),
        (79, 149, 74),
        (132, 82, 161),
        (190, 70, 90),
        (148, 118, 45),
        (70, 145, 160),
        (80, 80, 80),
    ]
    return colors[i % len(colors)]


def draw_plot(path: Path, rows: list[ProbeRow], epsilon: float) -> None:
    anchor_risk = aggregate_anchor_step_risk(rows)
    if not anchor_risk:
        img = Image.new("RGB", (800, 400), "white")
        draw = ImageDraw.Draw(img)
        draw.text((24, 24), "No reference probe rows found", fill=(0, 0, 0))
        img.save(path)
        return

    width, height = 1100, 680
    margin_l, margin_r, margin_t, margin_b = 90, 260, 60, 90
    plot_w = width - margin_l - margin_r
    plot_h = height - margin_t - margin_b
    img = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(img)

    all_steps = sorted({step for by_step in anchor_risk.values() for step in by_step})
    all_risks = [risk for by_step in anchor_risk.values() for risk in by_step.values() if math.isfinite(risk)]
    max_risk = max([epsilon * 1.25, *all_risks, 1e-6])
    min_step = min(all_steps)
    max_step = max(all_steps)

    def x_for(step: float) -> int:
        if max_step == min_step:
            return margin_l + plot_w // 2
        # Coarser steps on the left, finer/reference on the right.
        return int(margin_l + (max_step - step) / (max_step - min_step) * plot_w)

    def y_for(risk: float) -> int:
        return int(margin_t + (1.0 - min(max(risk, 0.0), max_risk) / max_risk) * plot_h)

    # axes and grid
    draw.rectangle((margin_l, margin_t, margin_l + plot_w, margin_t + plot_h), outline=(30, 30, 30))
    for step in all_steps:
        x = x_for(step)
        draw.line((x, margin_t, x, margin_t + plot_h), fill=(225, 225, 225))
        draw.text((x - 24, margin_t + plot_h + 12), fmt_float(step), fill=(40, 40, 40))
    for frac in [0.0, 0.25, 0.5, 0.75, 1.0]:
        risk = frac * max_risk
        y = y_for(risk)
        draw.line((margin_l, y, margin_l + plot_w, y), fill=(235, 235, 235))
        draw.text((18, y - 8), fmt_float(risk, 3), fill=(40, 40, 40))

    y_eps = y_for(epsilon)
    draw.line((margin_l, y_eps, margin_l + plot_w, y_eps), fill=(180, 40, 40), width=2)
    draw.text((margin_l + plot_w + 8, y_eps - 8), f"epsilon={fmt_float(epsilon)}", fill=(180, 40, 40))

    draw.text((margin_l, 22), "Reference Probe: risk vs step by anchor", fill=(0, 0, 0))
    draw.text((margin_l + plot_w // 2 - 120, height - 34), "step length (finer to the right)", fill=(0, 0, 0))
    draw.text((12, margin_t + plot_h // 2 - 20), "decision risk", fill=(0, 0, 0))

    legend_y = margin_t
    for idx, (anchor, by_step) in enumerate(sorted(anchor_risk.items())[:18]):
        color = palette(idx)
        points = [(x_for(step), y_for(risk)) for step, risk in sorted(by_step.items(), reverse=True)]
        if len(points) >= 2:
            draw.line(points, fill=color, width=3)
        for p in points:
            draw.ellipse((p[0] - 4, p[1] - 4, p[0] + 4, p[1] + 4), fill=color)
        short = anchor if len(anchor) <= 34 else anchor[:31] + "..."
        draw.rectangle((margin_l + plot_w + 20, legend_y + 3, margin_l + plot_w + 34, legend_y + 17), fill=color)
        draw.text((margin_l + plot_w + 42, legend_y), short, fill=(20, 20, 20))
        legend_y += 26
    if len(anchor_risk) > 18:
        draw.text((margin_l + plot_w + 20, legend_y), f"+ {len(anchor_risk) - 18} more anchors", fill=(80, 80, 80))

    img.save(path)


def infer_canvas(rows: list[ProbeRow]) -> tuple[int, int]:
    max_x = max((r.projected_x for r in rows), default=319)
    max_y = max((r.projected_y for r in rows), default=179)
    return max(320, max_x + 16), max(180, max_y + 16)


def group_metrics(group: list[ProbeRow], epsilon: float) -> dict[str, Any]:
    by_step = risks_by_step(group)
    step_mean = {step: mean(vals) for step, vals in by_step.items()}
    max_risk = max((r.decision_risk for r in group), default=0.0)
    risk_00625 = step_mean.get(0.00625, math.nan)
    required = min_step_satisfying(group, epsilon)
    mono = monotonic_decay_pass(group, 1e-9)
    persistent = persistent_mismatch_at(group, 0.00625, epsilon)
    reference_step = min(by_step) if by_step else math.nan
    ref_risk = step_mean.get(reference_step, math.nan)
    if not math.isfinite(ref_risk) or ref_risk > epsilon:
        cls = "persistent"
    elif required == "0.015":
        cls = "stable_coarse"
    elif required in {"0.0125", "0.00625"} and mono:
        cls = "gradual_decay"
    elif required in {"0.003125", "reference"} and persistent:
        cls = "threshold_snap"
    elif not mono:
        cls = "nonmonotonic"
    else:
        cls = "mixed"
    first = group[0]
    return {
        "anchor_id": first.anchor_id,
        "object_id": first.object_id,
        "projected_tile": first.projected_tile,
        "projected_x": first.projected_x,
        "projected_y": first.projected_y,
        "object_centroid_projected_x": first.object_centroid_x,
        "object_centroid_projected_y": first.object_centroid_y,
        "radial_dist_from_object_centroid": first.radial_dist_from_object_centroid,
        "radial_angle_from_object_centroid": first.radial_angle_from_object_centroid,
        "nearest_anchor_kind": first.nearest_anchor_kind,
        "nearest_corner_dist": first.nearest_corner_dist,
        "nearest_edge_dist": first.nearest_edge_dist,
        "offset_x": first.offset_x,
        "offset_y": first.offset_y,
        "max_risk": max_risk,
        "risk_00625": risk_00625,
        "required_precision": required,
        "monotonic": mono,
        "persistent_00625": persistent,
        "convergence_class": cls,
    }


def group_metric_rows(rows: list[ProbeRow], epsilon: float) -> list[dict[str, Any]]:
    return [group_metrics(group, epsilon) for group in offset_groups(rows).values()]


def splat(draw: ImageDraw.ImageDraw, x: int, y: int, color: tuple[int, int, int], radius: int = 2) -> None:
    draw.rectangle((x - radius, y - radius, x + radius, y + radius), fill=color)


def risk_color(value: float, max_value: float) -> tuple[int, int, int]:
    t = 0.0 if max_value <= 0 else max(0.0, min(1.0, value / max_value))
    if t < 0.5:
        u = t / 0.5
        return (int(255 * u), int(190 * u), 30)
    u = (t - 0.5) / 0.5
    return (255, int(190 * (1 - u)), int(30 * (1 - u)))


def precision_color(label: str) -> tuple[int, int, int]:
    return {
        "0.015": (40, 170, 80),
        "0.0125": (35, 160, 190),
        "0.00625": (235, 190, 45),
        "0.003125": (220, 70, 55),
        "reference": (220, 70, 55),
        "none": (180, 55, 180),
    }.get(label, (120, 120, 120))


def class_color(label: str) -> tuple[int, int, int]:
    return {
        "stable_coarse": (50, 160, 75),
        "gradual_decay": (55, 130, 210),
        "threshold_snap": (230, 150, 40),
        "nonmonotonic": (190, 70, 170),
        "persistent": (210, 45, 45),
        "mixed": (130, 130, 130),
    }.get(label, (80, 80, 80))


def write_heatmaps(out_dir: Path, metric_rows: list[dict[str, Any]], canvas: tuple[int, int], epsilon: float) -> None:
    w, h = canvas
    max_risk = max((float(r["max_risk"]) for r in metric_rows), default=epsilon)
    max_risk = max(max_risk, epsilon, 1e-6)

    risk_img = Image.new("RGB", (w, h), (0, 0, 0))
    precision_img = Image.new("RGB", (w, h), (0, 0, 0))
    class_img = Image.new("RGB", (w, h), (0, 0, 0))
    for img, mode in [(risk_img, "risk"), (precision_img, "precision"), (class_img, "class")]:
        draw = ImageDraw.Draw(img)
        for row in metric_rows:
            x, y = int(row["projected_x"]), int(row["projected_y"])
            if mode == "risk":
                color = risk_color(float(row["max_risk"]), max_risk)
            elif mode == "precision":
                color = precision_color(str(row["required_precision"]))
            else:
                color = class_color(str(row["convergence_class"]))
            splat(draw, x, y, color, radius=2)

    risk_img.save(out_dir / "decision_risk_heatmap.png")
    precision_img.save(out_dir / "required_precision_heatmap.png")
    class_img.save(out_dir / "convergence_class_heatmap.png")


def detect_transport_risk_nodes(metric_rows: list[dict[str, Any]], epsilon: float) -> list[dict[str, Any]]:
    nodes: list[dict[str, Any]] = []
    for i, row in enumerate(metric_rows):
        x, y = int(row["projected_x"]), int(row["projected_y"])
        risk = float(row["max_risk"])
        neighbor_risks = []
        for j, other in enumerate(metric_rows):
            if i == j:
                continue
            dx = int(other["projected_x"]) - x
            dy = int(other["projected_y"]) - y
            if dx * dx + dy * dy <= 8 * 8:
                neighbor_risks.append(float(other["max_risk"]))
        local_max = not neighbor_risks or risk >= max(neighbor_risks)
        persistent = bool(row["persistent_00625"])
        snap = str(row["convergence_class"]) == "threshold_snap"
        if not (local_max or persistent or snap):
            continue
        nodes.append(
            {
                "node_id": len(nodes) + 1,
                "anchor_id": row["anchor_id"],
                "object_id": row["object_id"],
                "projected_tile": row["projected_tile"],
                "projected_x": x,
                "projected_y": y,
                "object_centroid_projected_x": row.get("object_centroid_projected_x", -1),
                "object_centroid_projected_y": row.get("object_centroid_projected_y", -1),
                "radial_dist_from_object_centroid": row.get("radial_dist_from_object_centroid", -1.0),
                "radial_angle_from_object_centroid": row.get("radial_angle_from_object_centroid", 0.0),
                "nearest_anchor_kind": row.get("nearest_anchor_kind", ""),
                "nearest_corner_dist": row.get("nearest_corner_dist", -1.0),
                "nearest_edge_dist": row.get("nearest_edge_dist", -1.0),
                "radial_offset_x": row["offset_x"],
                "radial_offset_y": row["offset_y"],
                "max_decision_risk": risk,
                "risk_at_0.00625": row["risk_00625"],
                "required_precision_estimate": row["required_precision"],
                "convergence_class": row["convergence_class"],
                "local_maximum": local_max,
                "persistent_mismatch_at_0.00625": persistent,
                "threshold_snap_zone": snap,
            }
        )
    nodes.sort(key=lambda n: (float(n["max_decision_risk"]), bool(n["persistent_mismatch_at_0.00625"])), reverse=True)
    for idx, node in enumerate(nodes, start=1):
        node["node_id"] = idx
    return nodes


NODE_COLS = [
    "node_id",
    "anchor_id",
    "object_id",
    "projected_tile",
    "projected_x",
    "projected_y",
    "object_centroid_projected_x",
    "object_centroid_projected_y",
    "radial_dist_from_object_centroid",
    "radial_angle_from_object_centroid",
    "nearest_anchor_kind",
    "nearest_corner_dist",
    "nearest_edge_dist",
    "radial_offset_x",
    "radial_offset_y",
    "max_decision_risk",
    "risk_at_0.00625",
    "required_precision_estimate",
    "convergence_class",
    "local_maximum",
    "persistent_mismatch_at_0.00625",
    "threshold_snap_zone",
]


def write_nodes_csv(path: Path, nodes: list[dict[str, Any]]) -> None:
    with path.open("w", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=NODE_COLS)
        writer.writeheader()
        for node in nodes:
            out = dict(node)
            out["max_decision_risk"] = fmt_float(float(out["max_decision_risk"]))
            out["risk_at_0.00625"] = fmt_float(float(out["risk_at_0.00625"])) if math.isfinite(float(out["risk_at_0.00625"])) else ""
            out["local_maximum"] = "yes" if out["local_maximum"] else "no"
            out["persistent_mismatch_at_0.00625"] = "yes" if out["persistent_mismatch_at_0.00625"] else "no"
            out["threshold_snap_zone"] = "yes" if out["threshold_snap_zone"] else "no"
            writer.writerow({key: out.get(key, "") for key in NODE_COLS})


def write_node_map(path: Path, nodes: list[dict[str, Any]], canvas: tuple[int, int]) -> None:
    img = Image.new("RGB", canvas, (0, 0, 0))
    draw = ImageDraw.Draw(img)
    for node in nodes:
        x, y = int(node["projected_x"]), int(node["projected_y"])
        color = class_color(str(node["convergence_class"]))
        r = 5 if node["persistent_mismatch_at_0.00625"] else 3
        draw.ellipse((x - r, y - r, x + r, y + r), outline=(255, 255, 255), fill=color)
    img.save(path)


REGION_COLS = [
    "region_id",
    "node_id",
    "object_id",
    "node_center_x",
    "node_center_y",
    "object_centroid_projected_x",
    "object_centroid_projected_y",
    "max_decision_risk",
    "inner_high_risk_radius",
    "outer_stable_radius",
    "required_precision_label_inside_region",
    "convergence_class_distribution",
    "region_class",
    "sample_count",
    "high_risk_sample_count",
    "stable_sample_count",
    "sealed",
]


def pixel_dist(a: dict[str, Any], b: dict[str, Any]) -> float:
    dx = float(a["projected_x"]) - float(b["projected_x"])
    dy = float(a["projected_y"]) - float(b["projected_y"])
    return math.hypot(dx, dy)


def angle_delta(a: float, b: float) -> float:
    d = abs(a - b) % (math.pi * 2.0)
    return min(d, math.pi * 2.0 - d)


def required_precision_rank(label: str) -> int:
    return {
        "0.015": 0,
        "0.0125": 1,
        "0.00625": 2,
        "0.003125": 3,
        "reference": 3,
        "none": 4,
    }.get(label, 5)


def classify_region(node: dict[str, Any], samples: list[dict[str, Any]], high: list[dict[str, Any]], stable: list[dict[str, Any]], outer_stable: float | None, epsilon: float) -> str:
    if not high:
        return "CORE_STABLE"
    if outer_stable is None:
        return "UNSEALED_NONCONVERGENT"
    nearest_kind = str(node.get("nearest_anchor_kind", ""))
    nearest_corner = float(node.get("nearest_corner_dist", -1.0))
    nearest_edge = float(node.get("nearest_edge_dist", -1.0))
    snap_count = sum(1 for s in high if str(s["convergence_class"]) == "threshold_snap")
    if "Corner" in nearest_kind or (nearest_corner >= 0.0 and (nearest_edge < 0.0 or nearest_corner <= nearest_edge)) or snap_count >= max(1, len(high) // 2):
        return "CORNER_CURVATURE_SNAP"
    if "Edge" in nearest_kind or (nearest_edge >= 0.0 and (nearest_corner < 0.0 or nearest_edge < nearest_corner)):
        return "EDGE_TRANSITION"
    if stable and max(float(s["max_risk"]) for s in stable) <= epsilon:
        return "OUTER_STABLE_BOUND"
    return "EDGE_TRANSITION"


def build_transport_risk_regions(nodes: list[dict[str, Any]], metric_rows: list[dict[str, Any]], epsilon: float) -> list[dict[str, Any]]:
    regions: list[dict[str, Any]] = []
    for node in nodes:
        node_angle = float(node.get("radial_angle_from_object_centroid", 0.0))
        object_id = str(node["object_id"])
        samples: list[dict[str, Any]] = []
        for row in metric_rows:
            if str(row["object_id"]) != object_id:
                continue
            dist = pixel_dist(node, row)
            row_angle = float(row.get("radial_angle_from_object_centroid", 0.0))
            same_radial_corridor = angle_delta(node_angle, row_angle) <= math.radians(35.0)
            if dist <= 28.0 or same_radial_corridor:
                enriched = dict(row)
                enriched["dist_from_node"] = dist
                samples.append(enriched)
        if not samples:
            continue

        high = [s for s in samples if float(s["max_risk"]) > epsilon or bool(s["persistent_00625"])]
        stable = [s for s in samples if float(s["max_risk"]) <= epsilon]
        inner_high = max((float(s["dist_from_node"]) for s in high), default=0.0)
        outer_candidates = [float(s["dist_from_node"]) for s in stable if float(s["dist_from_node"]) >= inner_high]
        outer_stable = min(outer_candidates) if outer_candidates else None
        precision_counts = Counter(str(s["required_precision"]) for s in high or samples)
        if precision_counts:
            required_label = sorted(precision_counts, key=lambda label: (-precision_counts[label], -required_precision_rank(label), label))[0]
        else:
            required_label = "none"
        class_counts = Counter(str(s["convergence_class"]) for s in samples)
        class_dist = ";".join(f"{cls}:{count}" for cls, count in class_counts.most_common())
        region_class = classify_region(node, samples, high, stable, outer_stable, epsilon)

        regions.append(
            {
                "region_id": len(regions) + 1,
                "node_id": node["node_id"],
                "object_id": object_id,
                "node_center_x": node["projected_x"],
                "node_center_y": node["projected_y"],
                "object_centroid_projected_x": node.get("object_centroid_projected_x", -1),
                "object_centroid_projected_y": node.get("object_centroid_projected_y", -1),
                "max_decision_risk": max(float(s["max_risk"]) for s in samples),
                "inner_high_risk_radius": inner_high,
                "outer_stable_radius": outer_stable,
                "required_precision_label_inside_region": required_label,
                "convergence_class_distribution": class_dist,
                "region_class": region_class,
                "sample_count": len(samples),
                "high_risk_sample_count": len(high),
                "stable_sample_count": len(stable),
                "sealed": outer_stable is not None,
                "_samples": samples,
            }
        )
    return regions


def write_regions_csv(path: Path, regions: list[dict[str, Any]]) -> None:
    with path.open("w", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=REGION_COLS)
        writer.writeheader()
        for region in regions:
            out = dict(region)
            out.pop("_samples", None)
            out["max_decision_risk"] = fmt_float(float(out["max_decision_risk"]))
            out["inner_high_risk_radius"] = fmt_float(float(out["inner_high_risk_radius"]))
            outer = out["outer_stable_radius"]
            out["outer_stable_radius"] = fmt_float(float(outer)) if outer is not None else ""
            out["sealed"] = "yes" if out["sealed"] else "no"
            writer.writerow({key: out.get(key, "") for key in REGION_COLS})


def draw_radial_profile(path: Path, regions: list[dict[str, Any]], epsilon: float) -> None:
    width, height = 1100, 640
    ml, mr, mt, mb = 82, 250, 52, 82
    pw, ph = width - ml - mr, height - mt - mb
    img = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(img)
    samples = [(r, s) for r in regions[:14] for s in r.get("_samples", [])]
    if not samples:
        draw.text((24, 24), "No transport risk region samples", fill=(0, 0, 0))
        img.save(path)
        return
    max_x = max(float(s["dist_from_node"]) for _, s in samples)
    max_y = max([epsilon * 1.25, *(float(s["max_risk"]) for _, s in samples), 1e-6])
    draw.rectangle((ml, mt, ml + pw, mt + ph), outline=(30, 30, 30))
    y_eps = int(mt + (1.0 - min(epsilon / max_y, 1.0)) * ph)
    draw.line((ml, y_eps, ml + pw, y_eps), fill=(180, 40, 40), width=2)
    draw.text((ml + pw + 8, y_eps - 8), f"epsilon={fmt_float(epsilon)}", fill=(180, 40, 40))
    draw.text((ml, 20), "Transport Risk Regions: radial risk profile by node", fill=(0, 0, 0))
    draw.text((ml + pw // 2 - 80, height - 32), "distance from node center (px)", fill=(0, 0, 0))

    for idx, region in enumerate(regions[:14]):
        color = palette(idx)
        pts = []
        by_dist: dict[int, list[float]] = defaultdict(list)
        for sample in region.get("_samples", []):
            by_dist[int(round(float(sample["dist_from_node"])))].append(float(sample["max_risk"]))
        for dist, vals in sorted(by_dist.items()):
            x = int(ml + (dist / max(max_x, 1e-6)) * pw)
            y = int(mt + (1.0 - min(max(vals) / max_y, 1.0)) * ph)
            pts.append((x, y))
        if len(pts) >= 2:
            draw.line(pts, fill=color, width=2)
        for p in pts:
            draw.ellipse((p[0] - 3, p[1] - 3, p[0] + 3, p[1] + 3), fill=color)
        draw.rectangle((ml + pw + 22, mt + idx * 24 + 4, ml + pw + 34, mt + idx * 24 + 16), fill=color)
        draw.text((ml + pw + 42, mt + idx * 24), f"region {region['region_id']} node {region['node_id']}", fill=(20, 20, 20))
    img.save(path)


def draw_region_overlay(path: Path, regions: list[dict[str, Any]], metric_rows: list[dict[str, Any]], canvas: tuple[int, int], epsilon: float) -> None:
    img = Image.new("RGB", canvas, (0, 0, 0))
    draw = ImageDraw.Draw(img)
    max_risk = max((float(r["max_risk"]) for r in metric_rows), default=epsilon)
    max_risk = max(max_risk, epsilon, 1e-6)
    for row in metric_rows:
        splat(draw, int(row["projected_x"]), int(row["projected_y"]), risk_color(float(row["max_risk"]), max_risk), radius=1)
    for region in regions:
        x, y = int(region["node_center_x"]), int(region["node_center_y"])
        inner = int(round(float(region["inner_high_risk_radius"])))
        outer = region["outer_stable_radius"]
        draw.ellipse((x - max(3, inner), y - max(3, inner), x + max(3, inner), y + max(3, inner)), outline=(255, 210, 60), width=1)
        if outer is not None:
            ro = int(round(float(outer)))
            draw.ellipse((x - ro, y - ro, x + ro, y + ro), outline=(80, 220, 110), width=1)
        else:
            draw.rectangle((x - 5, y - 5, x + 5, y + 5), outline=(230, 60, 220), width=1)
    img.save(path)


def draw_radial_precision(path: Path, metric_rows: list[dict[str, Any]], epsilon: float) -> None:
    width, height = 1000, 520
    ml, mr, mt, mb = 80, 40, 42, 80
    pw, ph = width - ml - mr, height - mt - mb
    img = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(img)
    rows = [r for r in metric_rows if float(r.get("radial_dist_from_object_centroid", -1.0)) >= 0.0]
    if not rows:
        draw.text((24, 24), "No centroid-relative radial distances found", fill=(0, 0, 0))
        img.save(path)
        return
    max_dist = max(float(r["radial_dist_from_object_centroid"]) for r in rows)
    labels = ["0.015", "0.0125", "0.00625", "0.003125", "reference", "none"]
    y_for_label = {label: mt + int((idx / max(1, len(labels) - 1)) * ph) for idx, label in enumerate(labels)}
    draw.rectangle((ml, mt, ml + pw, mt + ph), outline=(30, 30, 30))
    for label, y in y_for_label.items():
        draw.line((ml, y, ml + pw, y), fill=(230, 230, 230))
        draw.text((18, y - 8), label, fill=(30, 30, 30))
    draw.text((ml, 18), "Radial distance from object centroid vs required precision", fill=(0, 0, 0))
    draw.text((ml + pw // 2 - 100, height - 32), "radial distance from object centroid (px)", fill=(0, 0, 0))
    for row in rows:
        dist = float(row["radial_dist_from_object_centroid"])
        label = str(row["required_precision"])
        y = y_for_label.get(label, y_for_label.get("none", mt + ph))
        x = int(ml + (dist / max(max_dist, 1e-6)) * pw)
        color = precision_color(label)
        r = 3 if float(row["max_risk"]) > epsilon else 2
        draw.ellipse((x - r, y - r, x + r, y + r), fill=color)
    img.save(path)


def build_region_report(regions: list[dict[str, Any]], epsilon: float) -> str:
    class_counter = Counter(str(r["region_class"]) for r in regions)
    precision_counter = Counter(str(r["required_precision_label_inside_region"]) for r in regions)
    lines = [
        "# Transport Risk Region Report",
        "",
        f"- TransportRiskRegions: {len(regions)}",
        f"- Epsilon: {epsilon:g}",
        f"- Sealed regions: {sum(1 for r in regions if r['sealed'])}",
        f"- Unsealed regions: {sum(1 for r in regions if not r['sealed'])}",
        "",
        "## Region Classes",
        "",
        "| class | count |",
        "|---|---:|",
    ]
    for cls, count in class_counter.most_common():
        lines.append(f"| `{cls}` | {count} |")
    lines.extend(["", "## Required Precision Inside Regions", "", "| precision | count |", "|---|---:|"])
    for precision, count in precision_counter.most_common():
        lines.append(f"| `{precision}` | {count} |")
    lines.extend(["", "## Top Regions", "", "| region | node | class | center | high_radius | stable_radius | precision | max_risk | sealed |", "|---:|---:|---|---:|---:|---:|---|---:|---|"])
    for region in regions[:40]:
        outer = region["outer_stable_radius"]
        lines.append(
            f"| {region['region_id']} | {region['node_id']} | `{region['region_class']}` | "
            f"({region['node_center_x']},{region['node_center_y']}) | {fmt_float(float(region['inner_high_risk_radius']))} | "
            f"{fmt_float(float(outer)) if outer is not None else ''} | `{region['required_precision_label_inside_region']}` | "
            f"{fmt_float(float(region['max_decision_risk']))} | {'yes' if region['sealed'] else 'no'} |"
        )
    lines.extend(["", "## Guardrail", "", "This is analysis only. It does not alter renderer behavior, scheduler order, hit selection, or shading."])
    return "\n".join(lines) + "\n"


def build_node_report(nodes: list[dict[str, Any]], metric_rows: list[dict[str, Any]], epsilon: float) -> str:
    object_counter = Counter(str(n["object_id"]) for n in nodes)
    tile_counter = Counter(str(n["projected_tile"]) for n in nodes)
    class_counter = Counter(str(n["convergence_class"]) for n in nodes)
    lines = [
        "# Transport Risk Node Report",
        "",
        f"- Sample points: {len(metric_rows)}",
        f"- TransportRiskNodes: {len(nodes)}",
        f"- Epsilon: {epsilon:g}",
        "",
        "## Convergence Classes",
        "",
        "| class | node_count |",
        "|---|---:|",
    ]
    for cls, count in class_counter.most_common():
        lines.append(f"| `{cls}` | {count} |")

    lines.extend(["", "## Objects Most Associated With Risk Nodes", "", "| object_id | node_count |", "|---|---:|"])
    for obj, count in object_counter.most_common(20):
        lines.append(f"| `{obj}` | {count} |")

    lines.extend(["", "## Tiles Most Associated With Risk Nodes", "", "| projected_tile | node_count |", "|---:|---:|"])
    for tile, count in tile_counter.most_common(20):
        lines.append(f"| {tile} | {count} |")

    lines.extend(["", "## Top Risk Nodes", "", "| node | anchor_id | tile | pixel | class | required_precision | max_risk | risk_0.00625 | flags |", "|---:|---|---:|---:|---|---|---:|---:|---|"])
    for node in nodes[:40]:
        flags = []
        if node["local_maximum"]:
            flags.append("local_max")
        if node["persistent_mismatch_at_0.00625"]:
            flags.append("persistent_0.00625")
        if node["threshold_snap_zone"]:
            flags.append("snap")
        risk625 = node["risk_at_0.00625"]
        lines.append(
            f"| {node['node_id']} | `{node['anchor_id']}` | {node['projected_tile']} | "
            f"({node['projected_x']},{node['projected_y']}) | `{node['convergence_class']}` | "
            f"`{node['required_precision_estimate']}` | {fmt_float(float(node['max_decision_risk']))} | "
            f"{fmt_float(float(risk625)) if math.isfinite(float(risk625)) else ''} | {', '.join(flags)} |"
        )

    lines.extend(["", "## Guardrail", "", "This is analysis only. It does not alter renderer behavior, scheduler order, hit selection, or shading."])
    return "\n".join(lines) + "\n"


def build_report(rows: list[ProbeRow], summary: list[dict[str, Any]], epsilon: float) -> str:
    groups = offset_groups(rows)
    nonmono = [key for key, group in groups.items() if not monotonic_decay_pass(group, 1e-9)]
    persistent = [key for key, group in groups.items() if persistent_mismatch_at(group, 0.00625, epsilon)]

    object_counter: Counter[str] = Counter()
    tile_counter: Counter[str] = Counter()
    anchor_counter: Counter[str] = Counter()
    for key in persistent:
        anchor, obj, tile, *_rest = key
        anchor_counter[anchor] += 1
        object_counter[obj] += 1
        tile_counter[tile] += 1

    total_offsets = len(groups)
    total_rows = len(rows)
    total_matches = sum(1 for r in rows if r.matched_reference)
    match_rate = total_matches / total_rows if total_rows else 0.0

    lines = [
        "# Nonconvergent Anchor Report",
        "",
        f"- Input rows: {total_rows}",
        f"- Anchor/radial-offset groups: {total_offsets}",
        f"- Overall match rate versus reference: {match_rate:.3f}",
        f"- Epsilon: {epsilon:g}",
        f"- Monotonic decay failures: {len(nonmono)}",
        f"- Persistent mismatches at `0.00625`: {len(persistent)}",
        "",
        "## Objects Most Associated With Nonconvergence",
        "",
    ]
    if object_counter:
        lines.append("| object_id | persistent_offset_count |")
        lines.append("|---|---:|")
        for obj, count in object_counter.most_common(20):
            lines.append(f"| `{obj}` | {count} |")
    else:
        lines.append("No persistent mismatches at `0.00625` were found.")

    lines.extend(["", "## Tiles Most Associated With Nonconvergence", ""])
    if tile_counter:
        lines.append("| projected_tile | persistent_offset_count |")
        lines.append("|---:|---:|")
        for tile, count in tile_counter.most_common(20):
            lines.append(f"| {tile} | {count} |")
    else:
        lines.append("No projected tiles crossed the persistent-mismatch threshold.")

    lines.extend(["", "## Anchors With Persistent Mismatch At 0.00625", ""])
    if anchor_counter:
        lines.append("| anchor_id | persistent_offset_count |")
        lines.append("|---|---:|")
        for anchor, count in anchor_counter.most_common(40):
            lines.append(f"| `{anchor}` | {count} |")
    else:
        lines.append("None.")

    lines.extend(["", "## Monotonic Decay Failures", ""])
    if nonmono:
        lines.append("| anchor_id | object_id | tile | offset |")
        lines.append("|---|---|---:|---:|")
        for anchor, obj, tile, _px, _py, ox, oy in nonmono[:80]:
            lines.append(f"| `{anchor}` | `{obj}` | {tile} | ({ox},{oy}) |")
    else:
        lines.append("All anchor/offset groups passed monotonic decay.")

    lines.extend(["", "## Notes", ""])
    lines.append("This is analysis only. It does not alter renderer behavior, scheduler order, hit selection, or shading.")
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("inputs", nargs="+", type=Path, help="CSV files or directories containing *.reference_geodesic_probe.csv")
    parser.add_argument("--out", type=Path, default=None, help="Output directory. Defaults to first input directory.")
    parser.add_argument("--epsilon", type=float, default=0.05, help="Decision-risk threshold for convergence.")
    parser.add_argument("--monotonic-tolerance", type=float, default=1e-9, help="Allowed risk increase when checking monotonic decay.")
    args = parser.parse_args()

    input_files = discover_inputs(args.inputs)
    if not input_files:
        raise SystemExit("No *.reference_geodesic_probe.csv files found.")

    out_dir = args.out
    if out_dir is None:
        first = args.inputs[0]
        out_dir = first if first.is_dir() else first.parent
    out_dir.mkdir(parents=True, exist_ok=True)

    rows = load_rows(input_files)
    if not rows:
        raise SystemExit("No valid probe rows loaded.")

    summary = build_summary(rows, args.epsilon, args.monotonic_tolerance)
    summary_path = out_dir / "reference_probe_summary.csv"
    plot_path = out_dir / "risk_vs_step_by_anchor.png"
    report_path = out_dir / "nonconvergent_anchor_report.md"
    nodes_path = out_dir / "transport_risk_nodes.csv"
    node_report_path = out_dir / "risk_node_report.md"
    regions_path = out_dir / "transport_risk_regions.csv"
    region_report_path = out_dir / "risk_region_report.md"

    write_summary_csv(summary_path, summary)
    draw_plot(plot_path, rows, args.epsilon)
    report_path.write_text(build_report(rows, summary, args.epsilon))
    metric_rows = group_metric_rows(rows, args.epsilon)
    canvas = infer_canvas(rows)
    write_heatmaps(out_dir, metric_rows, canvas, args.epsilon)
    nodes = detect_transport_risk_nodes(metric_rows, args.epsilon)
    write_nodes_csv(nodes_path, nodes)
    write_node_map(out_dir / "risk_node_map.png", nodes, canvas)
    node_report_path.write_text(build_node_report(nodes, metric_rows, args.epsilon))
    regions = build_transport_risk_regions(nodes, metric_rows, args.epsilon)
    write_regions_csv(regions_path, regions)
    draw_radial_profile(out_dir / "radial_risk_profile_by_node.png", regions, args.epsilon)
    draw_region_overlay(out_dir / "risk_region_overlay.png", regions, metric_rows, canvas, args.epsilon)
    draw_radial_precision(out_dir / "radial_dist_vs_required_precision.png", metric_rows, args.epsilon)
    region_report_path.write_text(build_region_report(regions, args.epsilon))

    print(f"[reference-probe-analyzer] inputs={len(input_files)} rows={len(rows)}")
    print(f"[reference-probe-analyzer] wrote {summary_path}")
    print(f"[reference-probe-analyzer] wrote {plot_path}")
    print(f"[reference-probe-analyzer] wrote {report_path}")
    print(f"[reference-probe-analyzer] wrote {out_dir / 'decision_risk_heatmap.png'}")
    print(f"[reference-probe-analyzer] wrote {out_dir / 'required_precision_heatmap.png'}")
    print(f"[reference-probe-analyzer] wrote {out_dir / 'convergence_class_heatmap.png'}")
    print(f"[reference-probe-analyzer] wrote {out_dir / 'risk_node_map.png'}")
    print(f"[reference-probe-analyzer] wrote {nodes_path}")
    print(f"[reference-probe-analyzer] wrote {node_report_path}")
    print(f"[reference-probe-analyzer] wrote {regions_path}")
    print(f"[reference-probe-analyzer] wrote {region_report_path}")
    print(f"[reference-probe-analyzer] wrote {out_dir / 'radial_risk_profile_by_node.png'}")
    print(f"[reference-probe-analyzer] wrote {out_dir / 'risk_region_overlay.png'}")
    print(f"[reference-probe-analyzer] wrote {out_dir / 'radial_dist_vs_required_precision.png'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
