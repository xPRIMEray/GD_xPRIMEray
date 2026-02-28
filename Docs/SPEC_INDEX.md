# xPRIMEray - Specification Index

**Master charter:** `xPRIMEray_architecture_charter_v2-Claude46.md`  
**Updated:** 2026-02-28

---

## Charter Section -> Spec Mapping

| Charter Section | Topic | Spec Document | Status |
|---|---|---|---|
| Sec5 | Data Model | `spec_scene_snapshot_data_layout.md` | Updated - code-grounded |
| Sec7.1 | Field Entity and Evaluation | `spec_field_system_grin.md` | New |
| Sec7.1 | FieldSource3D Canonical/Legacy Resolution | `spec_fieldsource3d_canonical_params_1.md` | New |
| Sec7.2-Sec7.3 | Metric Models and Tier Roadmap | `spec_metric_models_grin_vs_gordon.md` | Updated |
| Sec4, Sec7.1 | Field Extraction (Godot -> Snapshot) | `spec_field_extraction_rules.md` | Updated |
| Sec8 | Curved Ray Representation | `spec_curved_ray_chunks.md` | Updated |
| Sec9 | Acceleration Structures | `spec_bvh_acceleration.md` | Updated |
| Sec10 | Scheduling and Concurrency | `spec_scheduler_task_graph.md` | Updated |
| Sec11 | Rendering Backends | `spec_rendering_backends.md` | New |
| Sec12 | Telemetry and Debug | `spec_telemetry_debug.md` | New |
| Sec13 | Portability Interfaces | `spec_ray_transport_interfaces.md` | New |
| Sec14 | Research Mode | `spec_research_mode.md` | New |
| Sec15 | Wormhole System | `spec_wormhole_scene_graph.md` | New |

---

## Status Legend

- **Updated - code-grounded:** Existing spec revised with concrete code references
- **New:** Spec created for section that previously had no concrete spec
- **Implemented:** Describes code that exists and runs
- **Planned:** Describes architecture not yet implemented

---

## Coverage Summary

| Category | Count |
|---|---|
| Total specs | 13 |
| Updated from existing | 7 |
| New | 6 |
| Charter sections covered | Sec4, Sec5, Sec7-Sec15 |
