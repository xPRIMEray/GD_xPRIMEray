# xPRIMEray — Specification Index

**Master charter:** `xPRIMEray_architecture_charter_v2-Claude46.md`
**Updated:** 2026-02-13

---

## Charter Section → Spec Mapping

| Charter § | Topic | Spec Document | Status |
|-----------|-------|--------------|--------|
| §5 | Data Model | `spec_scene_snapshot_data_layout.md` | Updated — code-grounded |
| §7.1 | Field Entity & Evaluation | `spec_field_system_grin.md` | **New** — was empty, now populated |
| §7.2–7.3 | Metric Models & Tier Roadmap | `spec_metric_models_grin_vs_gordon.md` | Updated — added tier roadmap |
| §4, §7.1 | Field Extraction (Godot → Snapshot) | `spec_field_extraction_rules.md` | Updated — code-grounded |
| §8 | Curved Ray Representation | `spec_curved_ray_chunks.md` | Updated — aligned to RaySeg reality |
| §9 | Acceleration Structures | `spec_bvh_acceleration.md` | Updated — TLAS grounded, BLAS planned |
| §10 | Scheduling & Concurrency | `spec_scheduler_task_graph.md` | Updated — current model + planned migration |
| §11 | Rendering Backends | `spec_rendering_backends.md` | **New** |
| §12 | Telemetry & Debug | `spec_telemetry_debug.md` | **New** |
| §13 | Portability Interfaces | `spec_ray_transport_interfaces.md` | **New** |
| §14 | Research Mode | `spec_research_mode.md` | **New** |
| §15 | Wormhole System | `spec_wormhole_scene_graph.md` | **New** |

---

## Status Legend

- **Updated — code-grounded:** Existing spec revised with actual code references
- **New:** Spec created for charter section that had no prior spec
- **Implemented:** Describes code that exists and runs
- **Planned:** Describes architecture for code that does not yet exist

---

## Coverage Summary

| Category | Count |
|----------|-------|
| Total specs | 12 |
| Updated from existing | 7 |
| New | 5 |
| Charter sections covered | §4, §5, §7–§15 |
| Charter sections not needing spec | §0 (conventions), §1 (summary), §2 (overview), §3 (principles), §6 (coordinates), §16 (roadmap), §17 (glossary), §18–19 (refs) |
