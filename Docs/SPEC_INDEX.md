# xPRIMEray — Specification Index

This index aligns the spec set to the current MkDocs navigation and makes every Markdown document in the `/Docs` tree reachable from either this page or the top-level [Home](index.md) page.

---

## Current working charter

- [Architecture Charter (Current)](_xPRIMEray_arch_charter_v3-ChatClaudeGrokCoherencePass2.md)

## Active / revised specifications

These are the preferred working specs for the current documentation portal.

| Area | Document |
|---|---|
| Data model | [SceneSnapshot Data Layout — Revised](spec_scene_snapshot_data_layout_1.md) |
| Field evaluation | [Field System (GRIN Evaluation) — Revised](spec_field_system_grin_1.md) |
| FieldSource3D canonical mapping | [FieldSource3D Canonical Params + Legacy Compatibility](spec_fieldsource3d_canonical_params_1.md) |
| Metric / transport tier framing | [Metric Models (GRIN vs Gordon / Transport Tier Roadmap) — Revised](spec_metric_models_grin_vs_gordon_1.md) |
| Godot extraction rules | [Field Extraction Rules — Revised](spec_field_extraction_rules_1.md) |
| Curved transport representation | [Curved Ray Segment Integration — Revised](spec_curved_ray_chunks_1.md) |
| Acceleration | [BVH Acceleration System — Revised](spec_bvh_acceleration_1.md) |
| Scheduling / concurrency | [Scheduler & Task Graph — Revised](spec_scheduler_task_graph_1.md) |
| Rendering backends | [Rendering Backends](spec_rendering_backends_1.md) |
| Telemetry / diagnostics | [Telemetry, Debug, and Diagnostics](spec_telemetry_debug_1.md) |
| Portability interfaces | [Ray Transport & Portability Interfaces](spec_ray_transport_interfaces_1.md) |
| Research controls | [Research Mode](spec_research_mode_1.md) |
| Multi-scene / wormhole system | [Wormhole Multi-Scene System](spec_wormhole_scene_graph_1.md) |

---

## Legacy baseline specs

These older specs remain useful as architectural provenance and comparison points.

| Legacy baseline | Document |
|---|---|
| Data model | [SceneSnapshot Data Layout — Legacy](spec_scene_snapshot_data_layout.md) |
| Field evaluation | [Field System (GRIN Evaluation) — Legacy](spec_field_system_grin.md) |
| Metric framing | [Metric Models (GRIN vs Gordon Metric / Gravity Mode) — Legacy](spec_metric_models_grin_vs_gordon.md) |
| Extraction rules | [Field Extraction Rules — Legacy](spec_field_extraction_rules.md) |
| Curved integration | [Curved Ray Chunk Integration — Legacy](spec_curved_ray_chunks.md) |
| Acceleration | [BVH Acceleration System — Legacy](spec_bvh_acceleration.md) |
| Scheduling | [Scheduler & Task Graph — Legacy](spec_scheduler_task_graph.md) |

---

## Supporting architecture and validation docs

These are not spec files, but they are tightly coupled to the spec set and should remain visible in MkDocs.

- [System Architecture](architecture.md)
- [Architecture Overview](architecture_overview.md)
- [Architecture Review](architecture_review_ray_renderer.md)
- [Code Map (Big 12)](code_map_big12.md)
- [Validation Framework](validation.md)
- [Metric Null Geodesic Parameter Map](metric_null_geodesic_param_map.md)
- [Next-Generation Metric Transport Roadmap](metric_transport_nextgen_roadmap.md)
- [BlackHole Fast Compare (GRIN vs Metric)](blackhole_fast_compare.md)
- [Property Surface](PropertySurface.md)
- [RenderStep Gate Hierarchy Snapshot](RenderStep_GateHierarchy.md)

---

## Calibration roadmap patch logs

- [C1.0 g.1 — Parse/export canonical signature fields](CalibRoadmap/PatchLogs/C1_0_g_1.md)
- [C1.7 g.X — AutoCal weak-signal FieldHeavy delta-aware stopgap](CalibRoadmap/PatchLogs/C1_7_g_X.md)

---

## Archive set

Historical charter and outline documents:

- [_arch_charter_MASTER_v0-Chat52](./_Archive/_arch_charter_MASTER_v0-Chat52.md)
- [_arch_charter_MASTER_v1-Alt-RESEARCHMODE](./_Archive/_arch_charter_MASTER_v1-Alt-RESEARCHMODE.md)
- [_arch_charter_MASTER_v1-Alt-otherUpdated](./_Archive/_arch_charter_MASTER_v1-Alt-otherUpdated.md)
- [_arch_charter_MASTER_v1-Baseline-Gravity](./_Archive/_arch_charter_MASTER_v1-Baseline-Gravity.md)
- [_arch_charter_MASTER_v2-Chat52](./_Archive/_arch_charter_MASTER_v2-Chat52.md)
- [_xPRIMEray_arch_charter_MASTER_v3-ChatClaudeCoherencePass1](./_Archive/_xPRIMEray_arch_charter_MASTER_v3-ChatClaudeCoherencePass1.md)
- [_xPRIMEray_arch_charter_v2-Claude45](./_Archive/_xPRIMEray_arch_charter_v2-Claude45.md)
- [_xPRIMEray_arch_charter_v2-Claude46](./_Archive/_xPRIMEray_arch_charter_v2-Claude46.md)

---

## Alignment notes

- MkDocs should use `docs_dir: Docs` because the repository folder is capitalized.
- The `_1` spec files are treated as the active revised documents.
- The non-`_1` spec files are retained as legacy baselines and intentionally kept linked.
- The top-level [Home](index.md) page and this page together provide link coverage for all `.md` files currently present in `/Docs`.
