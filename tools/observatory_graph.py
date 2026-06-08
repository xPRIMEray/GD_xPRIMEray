#!/usr/bin/env python3
"""Build the Observatory Knowledge Graph from catalog and docs artifacts."""

from __future__ import annotations

import argparse
import json
import re
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


CATALOG_PATH = Path("reports/observatory_catalog.json")
OBSERVER_STORYBOARD_ROOT = Path("docs/observer_storyboard")
GALLERY_ROOT = Path("Docs/Observatory_Gallery")
OUTPUT_PATH = Path("reports/observatory_graph.json")

NODE_TYPES = {"Fixture", "Artifact", "Storyboard", "Vocabulary Term", "Citation Type"}
EDGE_TYPES = {"GENERATES", "USES", "REFERENCES", "DERIVED_FROM", "EXPLAINS"}

FIXTURE_LABELS = {
    "hermetic_curved_room": "Hermetic Curved Room",
    "curved_minimal": "Curved Minimal",
    "object_island": "Object Island",
    "corner_probe_reference": "Corner Probe Reference",
    "cathedral_probe": "Corner Probe Reference",
    "oracle_closure": "Oracle Closure",
    "renderer": "Renderer",
    "observer_storyboard_framework": "Observer Storyboard Framework",
    "observatory_reference": "Observatory Reference",
    "unknown": "Unknown Fixture",
}

CANONICAL_FIXTURE_IDS = [
    "hermetic_curved_room",
    "curved_minimal",
    "object_island",
    "corner_probe_reference",
    "oracle_closure",
]

ARTIFACT_LABELS = {
    "curvature_signature_ladder": "Curvature Signature Ladder",
    "hermetic_storyboard_v2": "Hermetic Storyboard v2",
    "observatory_story_reference": "Observatory Story Reference",
    "observer_storyboard": "Observer Storyboard",
    "renderer_storyboard_v1": "Renderer Storyboard v1",
}

STORYBOARDS = {
    "observatory_story": "Observatory Story",
    "observer_storyboard_v1": "Observer Storyboard v1",
    "hermetic_storyboard_v2": "Hermetic Storyboard v2",
    "renderer_storyboard_v1": "Renderer Storyboard v1",
    "observatory_story_reference": "Observatory Story Reference",
}

TERMS = {
    "observation": "Observation",
    "assumptions": "Assumptions",
    "perspectives": "Perspectives",
    "disagreements": "Disagreements",
    "closure_basin": "Closure Basin",
    "lineage": "Lineage",
    "coverage": "Coverage",
    "sensitivity_signature": "Sensitivity Signature",
    "verdict": "Verdict",
    "curvature_signature": "Curvature Signature",
    "hermetic_closure": "Hermetic Closure",
    "scene_contract": "Scene Contract",
    "transport_completion": "Transport Completion",
    "physical_accuracy": "Physical Accuracy",
    "reference_integration": "Reference Integration",
    "oracle": "Oracle",
    "budget_stress": "Budget Stress",
    "observer_disagreement": "Observer Disagreement",
    "coherence_basin": "Coherence Basin",
    "closure_diagnostics": "Closure Diagnostics",
    "curvature_benchmark": "Curvature Benchmark",
    "beauty_capture": "Beauty Capture",
    "full_frame_coverage": "Full-Frame Coverage",
    "traversal_cost": "Traversal Cost",
    "renderer_cost": "Renderer Cost",
    "artifact_catalog": "Artifact Catalog",
}

CITATION_TYPES = {
    "artifact_catalog": "Artifact Catalog",
    "docs_page": "Docs Page",
    "storyboard_spec": "Storyboard Specification",
    "schema": "Schema",
    "benchmark_report": "Benchmark Report",
    "source_path": "Source Path",
    "reference_integration": "Reference Integration",
    "scene_contract": "Scene Contract",
}

TERM_ALIASES = {
    "closure diagnostics": "closure_diagnostics",
    "curvature benchmark": "curvature_benchmark",
    "curvature signature": "curvature_signature",
    "hermetic closure": "hermetic_closure",
    "scene contract": "scene_contract",
    "scene-contract": "scene_contract",
    "transport completion": "transport_completion",
    "physical accuracy": "physical_accuracy",
    "reference integration": "reference_integration",
    "observer disagreement": "observer_disagreement",
    "coherence basin": "coherence_basin",
    "budget stress": "budget_stress",
    "beauty capture": "beauty_capture",
    "full-frame coverage": "full_frame_coverage",
    "full frame coverage": "full_frame_coverage",
    "coverage": "coverage",
    "oracle": "oracle",
    "verdict": "verdict",
}

DOC_CITATIONS = {
    "Docs/Observatory_Gallery/index.md": "docs_page:observatory_gallery_index",
    "Docs/Observatory_Gallery/what_the_observatory_measures.md": "docs_page:what_the_observatory_measures",
    "Docs/Observatory_Gallery/canonical_fixtures.md": "docs_page:canonical_fixtures",
    "Docs/Observatory_Gallery/closure_diagnostics.md": "docs_page:closure_diagnostics",
    "Docs/Observatory_Gallery/curvature_benchmark.md": "docs_page:curvature_benchmark",
    "docs/observer_storyboard/observer_storyboard_v1.md": "storyboard_spec:observer_storyboard_v1",
    "docs/observer_storyboard/observer_storyboard_v1.schema.json": "schema:observer_storyboard_v1",
}


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def slug(value: str) -> str:
    text = value.strip().lower()
    text = text.replace("&", " and ")
    text = re.sub(r"[^a-z0-9]+", "_", text)
    return text.strip("_") or "unknown"


def load_catalog(path: Path) -> list[dict[str, Any]]:
    if not path.exists():
        return []
    data = json.loads(path.read_text(encoding="utf-8"))
    if isinstance(data, list):
        return [r for r in data if isinstance(r, dict)]
    if isinstance(data, dict):
        for key in ("artifacts", "records", "items"):
            value = data.get(key)
            if isinstance(value, list):
                return [r for r in value if isinstance(r, dict)]
    return []


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8")
    except Exception:
        return ""


class Graph:
    def __init__(self) -> None:
        self.nodes: dict[str, dict[str, Any]] = {}
        self.edges: dict[tuple[str, str, str], dict[str, Any]] = {}

    def add_node(self, node_id: str, node_type: str, label: str, **metadata: Any) -> None:
        if node_type not in NODE_TYPES:
            raise ValueError(f"Unsupported node type: {node_type}")
        existing = self.nodes.get(node_id)
        if existing:
            existing.setdefault("metadata", {}).update({k: v for k, v in metadata.items() if v not in (None, "", [], {})})
            return
        node = {
            "id": node_id,
            "type": node_type,
            "label": label,
        }
        clean = {k: v for k, v in metadata.items() if v not in (None, "", [], {})}
        if clean:
            node["metadata"] = clean
        self.nodes[node_id] = node

    def add_edge(self, source: str, edge_type: str, target: str, **metadata: Any) -> None:
        if edge_type not in EDGE_TYPES:
            raise ValueError(f"Unsupported edge type: {edge_type}")
        key = (source, edge_type, target)
        clean = {k: v for k, v in metadata.items() if v not in (None, "", [], {})}
        if key in self.edges:
            self.edges[key].setdefault("metadata", {}).update(clean)
            return
        edge = {
            "source": source,
            "type": edge_type,
            "target": target,
        }
        if clean:
            edge["metadata"] = clean
        self.edges[key] = edge

    def sorted_nodes(self) -> list[dict[str, Any]]:
        return [self.nodes[k] for k in sorted(self.nodes)]

    def sorted_edges(self) -> list[dict[str, Any]]:
        return [self.edges[k] for k in sorted(self.edges)]


def add_base_nodes(graph: Graph) -> None:
    for fixture_id in CANONICAL_FIXTURE_IDS:
        graph.add_node(f"fixture:{fixture_id}", "Fixture", FIXTURE_LABELS[fixture_id], category="Canonical")
    for key, label in STORYBOARDS.items():
        graph.add_node(f"storyboard:{key}", "Storyboard", label)
    for key, label in TERMS.items():
        graph.add_node(f"term:{key}", "Vocabulary Term", label)
    for key, label in CITATION_TYPES.items():
        graph.add_node(f"citation:{key}", "Citation Type", label)


def add_doc_citation_nodes(graph: Graph) -> None:
    for path_text, node_id in DOC_CITATIONS.items():
        path = Path(path_text)
        if not path.exists():
            continue
        graph.add_node(
            node_id,
            "Citation Type",
            path.stem.replace("_", " ").title(),
            citation_kind=node_id.split(":", 1)[0],
            source_path=path.as_posix(),
        )


def add_catalog(graph: Graph, records: list[dict[str, Any]]) -> None:
    artifact_sources: dict[str, list[str]] = defaultdict(list)
    artifact_runs: dict[str, list[str]] = defaultdict(list)
    artifact_status: dict[str, dict[str, set[str]]] = defaultdict(lambda: defaultdict(set))

    for record in records:
        fixture_key = slug(str(record.get("fixture") or "unknown"))
        if fixture_key == "cathedral_probe":
            fixture_key = "corner_probe_reference"
        fixture_id = f"fixture:{fixture_key}"
        graph.add_node(
            fixture_id,
            "Fixture",
            FIXTURE_LABELS.get(fixture_key, str(record.get("fixture") or "Unknown Fixture").replace("_", " ").title()),
            category=record.get("category"),
        )

        artifact_key = slug(str(record.get("artifact_type") or "unknown_artifact"))
        artifact_id = f"artifact:{artifact_key}"
        source_path = str(record.get("source_path") or "")
        run_id = str(record.get("run_id") or "unknown")
        if source_path:
            artifact_sources[artifact_id].append(source_path)
        if run_id:
            artifact_runs[artifact_id].append(run_id)
        for field in ("coverage", "closure", "verdict"):
            value = str(record.get(field) or "")
            if value:
                artifact_status[artifact_id][field].add(value)

        graph.add_node(
            artifact_id,
            "Artifact",
            ARTIFACT_LABELS.get(artifact_key, artifact_key.replace("_", " ").title()),
        )
        graph.add_edge(
            fixture_id,
            "GENERATES",
            artifact_id,
            run_id=run_id,
            source_path=source_path,
            coverage=record.get("coverage"),
            closure=record.get("closure"),
            verdict=record.get("verdict"),
        )
        graph.add_edge(artifact_id, "DERIVED_FROM", "citation:artifact_catalog", source_path="reports/observatory_catalog.json")
        if source_path:
            graph.add_edge(artifact_id, "REFERENCES", "citation:source_path", source_path=source_path)

    for artifact_id, sources in artifact_sources.items():
        graph.add_node(
            artifact_id,
            "Artifact",
            graph.nodes[artifact_id]["label"],
            source_paths=sorted(set(sources)),
            run_ids=sorted(set(artifact_runs.get(artifact_id, []))),
            status_summary={k: sorted(v) for k, v in artifact_status[artifact_id].items()},
        )


def add_storyboard_relationships(graph: Graph) -> None:
    observer_terms = [
        "observation",
        "assumptions",
        "perspectives",
        "disagreements",
        "closure_basin",
        "lineage",
        "coverage",
        "sensitivity_signature",
        "verdict",
    ]
    for term in observer_terms:
        graph.add_edge("storyboard:observer_storyboard_v1", "USES", f"term:{term}")
    graph.add_edge("storyboard:observer_storyboard_v1", "DERIVED_FROM", "storyboard_spec:observer_storyboard_v1")
    graph.add_edge("storyboard:observer_storyboard_v1", "REFERENCES", "schema:observer_storyboard_v1")

    for term in (
        "beauty_capture",
        "hermetic_closure",
        "coverage",
        "curvature_signature",
        "budget_stress",
        "verdict",
    ):
        graph.add_edge("storyboard:hermetic_storyboard_v2", "USES", f"term:{term}")
    graph.add_edge("storyboard:hermetic_storyboard_v2", "USES", "storyboard:observatory_story")

    for term in (
        "renderer_cost",
        "traversal_cost",
        "coverage",
        "beauty_capture",
        "hermetic_closure",
        "curvature_signature",
        "verdict",
    ):
        graph.add_edge("storyboard:renderer_storyboard_v1", "USES", f"term:{term}")

    graph.add_edge("artifact:hermetic_storyboard_v2", "USES", "storyboard:hermetic_storyboard_v2")
    graph.add_edge("artifact:observer_storyboard", "USES", "storyboard:observer_storyboard_v1")
    graph.add_edge("artifact:renderer_storyboard_v1", "USES", "storyboard:renderer_storyboard_v1")
    graph.add_edge("artifact:observatory_story_reference", "USES", "storyboard:observatory_story")
    graph.add_edge("artifact:curvature_signature_ladder", "USES", "term:curvature_signature")


def add_vocabulary_relationships(graph: Graph) -> None:
    relationships = [
        ("fixture:hermetic_curved_room", "GENERATES", "term:curvature_signature"),
        ("term:curvature_signature", "USES", "term:sensitivity_signature"),
        ("term:sensitivity_signature", "EXPLAINS", "term:observer_disagreement"),
        ("term:hermetic_closure", "EXPLAINS", "term:scene_contract"),
        ("term:transport_completion", "USES", "term:scene_contract"),
        ("term:transport_completion", "EXPLAINS", "term:coverage"),
        ("term:coverage", "EXPLAINS", "term:full_frame_coverage"),
        ("term:oracle", "USES", "term:reference_integration"),
        ("term:reference_integration", "REFERENCES", "citation:reference_integration"),
        ("term:budget_stress", "EXPLAINS", "term:traversal_cost"),
        ("term:closure_basin", "USES", "term:hermetic_closure"),
        ("term:coherence_basin", "EXPLAINS", "term:observer_disagreement"),
        ("term:curvature_benchmark", "USES", "term:curvature_signature"),
        ("term:curvature_benchmark", "USES", "term:coverage"),
        ("term:curvature_benchmark", "USES", "term:budget_stress"),
        ("term:physical_accuracy", "REFERENCES", "citation:scene_contract"),
        ("term:beauty_capture", "EXPLAINS", "term:observation"),
    ]
    for source, edge_type, target in relationships:
        graph.add_edge(source, edge_type, target)


def add_doc_references(graph: Graph, roots: list[Path]) -> None:
    for root in roots:
        if not root.exists():
            continue
        for path in sorted(root.rglob("*")):
            if not path.is_file() or path.suffix.lower() not in {".md", ".json"}:
                continue
            text = read_text(path).lower()
            citation_id = DOC_CITATIONS.get(path.as_posix())
            if citation_id is None:
                citation_id = f"docs_page:{slug(path.stem)}" if path.suffix == ".md" else f"schema:{slug(path.stem)}"
                graph.add_node(
                    citation_id,
                    "Citation Type",
                    path.stem.replace("_", " ").title(),
                    citation_kind="docs_page" if path.suffix == ".md" else "schema",
                    source_path=path.as_posix(),
                )
            graph.add_edge(citation_id, "USES", "citation:docs_page" if path.suffix == ".md" else "citation:schema")

            for alias, term_key in TERM_ALIASES.items():
                if alias in text:
                    graph.add_edge(f"term:{term_key}", "DERIVED_FROM", citation_id, source_path=path.as_posix())


def build_graph(catalog_path: Path, storyboard_root: Path, gallery_root: Path) -> dict[str, Any]:
    records = load_catalog(catalog_path)
    graph = Graph()
    add_base_nodes(graph)
    add_doc_citation_nodes(graph)
    add_catalog(graph, records)
    add_storyboard_relationships(graph)
    add_vocabulary_relationships(graph)
    add_doc_references(graph, [storyboard_root, gallery_root])

    return {
        "schema": "xprimeray.observatory_graph.v1",
        "generated_at": utc_now(),
        "generated_from": {
            "catalog": catalog_path.as_posix(),
            "observer_storyboard": storyboard_root.as_posix(),
            "gallery": gallery_root.as_posix(),
        },
        "node_types": sorted(NODE_TYPES),
        "edge_types": sorted(EDGE_TYPES),
        "nodes": graph.sorted_nodes(),
        "edges": graph.sorted_edges(),
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", type=Path, default=CATALOG_PATH)
    parser.add_argument("--observer-storyboard-root", type=Path, default=OBSERVER_STORYBOARD_ROOT)
    parser.add_argument("--gallery-root", type=Path, default=GALLERY_ROOT)
    parser.add_argument("--output", type=Path, default=OUTPUT_PATH)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    graph = build_graph(args.catalog, args.observer_storyboard_root, args.gallery_root)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(graph, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"wrote {args.output} ({len(graph['nodes'])} nodes, {len(graph['edges'])} edges)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
