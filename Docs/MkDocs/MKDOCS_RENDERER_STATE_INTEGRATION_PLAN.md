# MkDocs Renderer State Integration Plan

> Authored: 2026-05-14
> Scope: Thread Quad Audit artifacts into MkDocs / GitHub Pages structure

---

## What This Plan Does

Integrates the five Quad Audit documents into the xPRIMEray MkDocs site so that the current renderer state is clear, visually coherent, and connected to the transport observatory framing. The changes are documentation-only: no source code edits, no file deletions.

---

## 1. Proposed `mkdocs.yml` Nav Changes

Key changes from the current nav:
- Add **Overview** tab (new: Transport Observatory overview page + Feature Index)
- Add **Observatory** tab (consolidates existing Diagnostics + new overlay docs)
- Add **Release** tab (Feature Readiness Audit)
- Add **MisterY Labs** tab (Inspiration Cards)
- Add `OPTICAL_TRANSPORT_FEATURE_MAP.md` to existing Research section
- Remove standalone **Diagnostics** tab (content moved to Observatory)
- Remove duplicate Cathedral Probe entry from Research

### Proposed Full Nav (replace existing `nav:` block in `mkdocs.yml`)

```yaml
nav:
  - Home: index.md

  - Overview:
      - Transport Observatory: Overview/TRANSPORT_OBSERVATORY_OVERVIEW.md
      - Feature Index: FEATURE_INDEX.md

  - Observatory:
      - Overlay Master List: Observatory/OVERLAY_MASTER_LIST.md
      - Atomic Visual Observatory: Research/atomic_orbital_visual_observatory_fixture.md
      - Wormhole Structure Observatory: Research/wormhole_dual_reality_transport_workflow.md
      - Domain Audit Visuals: diagnostics/domain_ownership.md
      - Diagnostics:
          - Overview: diagnostics/README.md
          - Curvature Heatmaps: diagnostics/heatmaps.md
          - Domain Ownership: diagnostics/domain_ownership.md
          - Phase Coherence: diagnostics/phase_coherence.md
          - Tile Coherence: diagnostics/tile_coherence.md

  - Research:
      - Optical Transport Map: Research/OPTICAL_TRANSPORT_FEATURE_MAP.md
      - Cathedral Probe Architecture: Research/cathedral_probe_architecture.md
      - Dual Reality Framework: Research/DualRealityFramework.md
      - Overspace Architecture Layer: Research/overspace_architecture_layer.md
      - Wormhole Pipeline Validation: Research/wormhole_render_pipeline_validation.md
      - Wormhole Dual-Reality Workflow: Research/wormhole_dual_reality_transport_workflow.md
      - GRIN Fixture Auto-Calibration: Research/grin_fixture_auto_calibration_phase_plan.md
      - Atomic Orbital Studies:
          - GRIN Room Fixture: Research/atomic_orbital_grin_room_fixture.md
          - Visual Observatory Fixture: Research/atomic_orbital_visual_observatory_fixture.md
      - Fixture Notes:
          - Note Pattern & Template: Research/fixture_note_pattern.md
          - Fixture 001 — Radial GRIN Baseline: Research/fixture_001_radial_grin_baseline.md
          - Fixture 002 — Linear Transport Baseline: Research/fixture_002_linear_transport_baseline.md
          - Fixture 003 — Offset Field Baseline: Research/fixture_003_offset_field_baseline.md
          - Fixture 004 — Dual Attractor Baseline: Research/fixture_004_dual_attractor_baseline.md
      - Advanced Studies:
          - Wormhole Curvature Heatmap Literature: Research/wormhole_curvature_heatmap_literature_crosswalk.md
          - Wormhole Throat Phased Architecture: Research/wormhole_throat_phased_architecture_note.md
          - TriClock DoE Sandbox Plan: Research/triclock_doe_sandbox_plan.md
          - Subtile Granularity: Research/curved_minimal_subtile_granularity.md
          - Experimental Subtile Scheduler: Research/experimental_subtile_scheduler_mode.md
      - Domain & Geometry Analysis:
          - Domain Ownership Analysis: Research/curvature_domain_ownership.md
          - Geometric Sampling Texture: Research/geometric_sampling_texture.md
          - Phase Coherence Field: Research/phase_coherence_field.md
      - Transport Island Microscopy: Research/transport_island_microscopy.md
      - Scheduler Decorrelation: Research/scheduler_decorrelation_and_local_coherence.md
      - Traversal Council Review: Research/architecture_design_council_traversal_review.md
      - Object-Seeded Null Geodesic Scheduler: Research/object_seeded_null_geodesic_tiling_scheduler.md
      - Reference Precision Null Geodesic Probe: Research/reference_precision_null_geodesic_probe.md
      - Visual Milestone Inventory: Research/xprimeray_visual_milestone_inventory.md
      - Feature Maturity Matrix: Research/xprimeray_feature_maturity_matrix.md

  - Release:
      - Feature Readiness Audit: Release/FEATURE_READINESS_AUDIT.md

  - MisterY Labs:
      - Inspiration Cards: MisterYLabs/INSPIRATION_CARD_FEATURE_LINKS.md

  - Core Docs:
      - System Architecture: architecture.md
      - Architecture Overview: architecture_overview.md
      - Architecture Subsystems: architecture/overview.md
      - Architecture Review: architecture_review_ray_renderer.md
      - Code Map (Big 12): code_map_big12.md
      - Glossary: glossary.md
      - Validation Framework: validation.md
      - Boundary Layer Fixtures: BoundaryLayerFixtures.md
      - Specification Index: SPEC_INDEX.md
      - Architecture Charter: _xPRIMEray_arch_charter_v3-ChatClaudeGrokCoherencePass2.md

  - Physics & Transport:
      - Metric Parameter Map: metric_null_geodesic_param_map.md
      - Metric Transport Roadmap: metric_transport_nextgen_roadmap.md
      - BlackHole Fast Compare (GRIN vs Metric): blackhole_fast_compare.md
      - Black Hole Optical Texture Reference: black_hole_optical_texture_reference.md
      - Property Surface: PropertySurface.md
      - RenderStep Gate Hierarchy: RenderStep_GateHierarchy.md
      - Overspaces: overspaces.md
      - Curved Ray Transport Model Review: Research/curved_ray_transport_model_review.md

  - Specifications — Current:
      - SceneSnapshot Data Layout: spec_scene_snapshot_data_layout_1.md
      - Field System (GRIN Evaluation): spec_field_system_grin_1.md
      - FieldSource3D Canonical Params: spec_fieldsource3d_canonical_params_1.md
      - Metric Models (GRIN vs Gordon): spec_metric_models_grin_vs_gordon_1.md
      - Field Extraction Rules: spec_field_extraction_rules_1.md
      - Curved Ray Segment Integration: spec_curved_ray_chunks_1.md
      - BVH Acceleration: spec_bvh_acceleration_1.md
      - Scheduler & Task Graph: spec_scheduler_task_graph_1.md
      - Rendering Backends: spec_rendering_backends_1.md
      - Telemetry / Debug / Diagnostics: spec_telemetry_debug_1.md
      - Ray Transport Interfaces: spec_ray_transport_interfaces_1.md
      - Research Mode: spec_research_mode_1.md
      - Wormhole Multi-Scene System: spec_wormhole_scene_graph_1.md

  - Specifications — Legacy:
      - SceneSnapshot Data Layout (Legacy): spec_scene_snapshot_data_layout.md
      - Field System GRIN (Legacy): spec_field_system_grin.md
      - Metric Models (Legacy): spec_metric_models_grin_vs_gordon.md
      - Field Extraction Rules (Legacy): spec_field_extraction_rules.md
      - Curved Ray Chunk Integration (Legacy): spec_curved_ray_chunks.md
      - BVH Acceleration (Legacy): spec_bvh_acceleration.md
      - Scheduler and Task Graph (Legacy): spec_scheduler_task_graph.md

  - Papers:
      - Paper Index: papers/index.md
      - Paper 000 — Unified Summary (Trilogy): papers/paper_000_unified_summary/paper.md
      - Paper 001 — Proto-Caustic Invariant: papers/paper_001_proto_caustic_invariant/paper.md
      - Paper 001 — Causal Observer Ladders: papers/paper_001_causal_observer_ladders/paper.md
      - Paper 002 — Low-Value Sector Budget: papers/paper_002_low_value_sector_budget/paper.md
      - Paper 003 — Coupled Invariants & Phase Space: papers/paper_003_coupled_invariants_phase_space/paper.md
      - Paper 004 — Hermetic Throat Validation: papers/paper_004_hermetic_throat_validation/paper.md
      - Paper 004 — Preprint (Markdown): papers/overspace_throat_validation_preprint_v1.md
      - Shared Related Work & Bibliography: papers/shared_related_work.md
      - Figure Captions: papers/figure_captions.md

  - arXiv:
      - Preprint Index: Arxiv/index.md

  - Validation:
      - Hermetic Fixture Rule: validation/hermetic_fixture_rule.md
      - Wormhole Observer Ladder: validation/wormhole_observer_ladder.md

  - Calibration Roadmap:
      - C1.0 g.1 — Canonical Signature Fields: CalibRoadmap/PatchLogs/C1_0_g_1.md
      - C1.7 g.X — AutoCal Weak-Signal Stopgap: CalibRoadmap/PatchLogs/C1_7_g_X.md

  - Archive:
      - Master Charter v0: _Archive/_arch_charter_MASTER_v0-Chat52.md
      - Master Charter v1 — RESEARCHMODE: _Archive/_arch_charter_MASTER_v1-Alt-RESEARCHMODE.md
      - Master Charter v1 — Alt Updated: _Archive/_arch_charter_MASTER_v1-Alt-otherUpdated.md
      - Master Charter v1 — Gravity Baseline: _Archive/_arch_charter_MASTER_v1-Baseline-Gravity.md
      - Master Charter v2: _Archive/_arch_charter_MASTER_v2-Chat52.md
      - Charter v3 — Coherence Pass 1: _Archive/_xPRIMEray_arch_charter_MASTER_v3-ChatClaudeCoherencePass1.md
      - Charter v2 — Claude 4.5: _Archive/_xPRIMEray_arch_charter_v2-Claude45.md
      - Charter v2 — Claude 4.6: _Archive/_xPRIMEray_arch_charter_v2-Claude46.md
```

---

## 2. Proposed Index Page Sections

The index page becomes a concise executive overview. Key changes from current:

| Change | Old | New |
|--------|-----|-----|
| Identity statement | "experimental curved-ray transport engine" | "curved-ray optical transport observatory" |
| Hero framing | Research-milestone-first | Three-part framing (Renderer / Observatory / Research Map) |
| Maturity summary | Not present | Table of Ready / In-Progress / Research-Only / Proposed |
| Quad Audit links | Not present | New "Navigator" section with links to 5 audit docs |
| Visual gallery | Not present | Gallery strip using committed assets |

Sections to add (without removing existing content):
1. Updated one-sentence identity at top
2. Three-part framing callout after "What This Is"
3. Current Renderer State maturity table after "Core Capabilities"
4. "Observatory & Audit Navigator" section after "Read Next"
5. Gallery strip above or below the Current Milestone section

---

## 3. Proposed Expanded Overview Page

**Path:** `Docs/Overview/TRANSPORT_OBSERVATORY_OVERVIEW.md`

Sections:
1. What xPRIMEray Is — three definitions (renderer, observatory, research map)
2. Why It Is a Transport Observatory — the "reveal structure, don't assert physics" philosophy
3. System Architecture Summary — how GRIN fields, curved rays, boundary layers, domains, and overlays fit together
4. Current Renderer State — table derived from `FEATURE_INDEX.md`
5. Research-Only Systems — diagnostic guardrail systems
6. Proposed Next — top 5 actions from `FEATURE_READINESS_AUDIT.md`
7. Inspiration Cards — how thinkers connect to engine features (links to MisterY Labs doc)
8. Visual Gallery — committed assets from `Docs/assets/`

---

## 4. Visual Gallery Image Candidates

### Currently Committed (safe to use immediately)

| Source Path | Size | Feature | Suitable? |
|-------------|------|---------|-----------|
| `Docs/assets/xPRIMEray-LOGO.png` | 904KB | Brand identity | Yes (header) |
| `Docs/assets/curved_field_validation_ladder/curved_vs_control_storyboard.png` | 66KB | Curved ray / GRIN field validation | **Yes — primary** |
| `Docs/assets/cathedral_probe/cathedral_probe_contact_sheet_row_0015.png` | 110KB | Six-layer Cathedral Probe diagnostic composite | **Yes — primary** |
| `Docs/assets/cathedral_probe/scheduler_resonance_stride_heatmap.png` | 110KB | Scheduler resonance DOE heatmap | Yes |
| `Docs/assets/cathedral_probe/traversal_contact_sheet_4mode_0015.png` | ~80KB | Traversal mode comparison | Yes |
| `Docs/assets/transport_islands/island_parent_trajectory_contact_sheet.png` | 42KB | Transport island oracle contact sheet | Yes |
| `Docs/assets/wormhole_inset_baseline.png` | 314KB | Wormhole baseline (slightly large) | Yes |
| `Docs/wormhole_test/wormhole_validation_composed_polished.png` | 118KB | Wormhole validation composed overlay | **Yes — primary** |
| `Docs/wormhole_test/figures/figure_B_composed_overlay.png` | 119KB | Composed overlay figure | Yes |
| `Docs/screenshots/2026-03-01_CurvedMinimalFixture.png` | 145KB | Curved minimal fixture render | **Yes — primary** |
| `Docs/screenshots/metric_basic_visual_smoke.png` | 81KB | Metric basic visual smoke test | Yes |

### From `output/` — Recommend Copying to `Docs/assets/overview/`

These are recent observatory outputs. They should **not** be committed from `output/` (4.1GB total). Instead, copy single representative images to `Docs/assets/overview/` before committing.

| Source | Size | Proposed Dest | Feature |
|--------|------|---------------|---------|
| `output/atomic_orbital_visual_observatory/20260513T012903Z/atomic_visual_contact_sheet.png` | 235KB | `Docs/assets/overview/atomic_orbital_contact_sheet.png` | Atomic Visual Observatory — V0-V4 comparison |
| `output/wormhole_structure_observatory/20260514T045629Z/wormhole_structure_contact_sheet.png` | 147KB | `Docs/assets/overview/wormhole_structure_contact_sheet.png` | Wormhole Structure Observatory contact sheet |
| `output/wormhole_structure_observatory/20260514T045629Z/testbench_preview.png` | 147KB | `Docs/assets/overview/wormhole_structure_preview.png` | Wormhole structure preview |
| `output/curved_field_validation_ladder/20260509T033059Z/curved_vs_control_storyboard.png` | 66KB | Already in `Docs/assets/curved_field_validation_ladder/` | Duplicate — use existing committed version |

### Oversized Assets — Flag for Optimization

| Asset | Size | Issue |
|-------|------|-------|
| `Docs/assets/fig_01_architecture.png` | 6.1MB | Too large for GitHub Pages; optimize to < 400KB |
| `Docs/assets/xprimeray-blueprint.png` | 4.7MB | Too large; consider SVG or compressed PNG |
| `Docs/assets/xprimeray-logo-dark.png` | 3.1MB | Too large for nav logo; the SVG `xprimeray-icon.svg` in `mkdocs.yml` is correct |

---

## 5. Asset Movement Recommendations

```bash
# Copy (not move) representative images from output/ to docs assets:
cp output/atomic_orbital_visual_observatory/20260513T012903Z/atomic_visual_contact_sheet.png \
   Docs/assets/overview/atomic_orbital_contact_sheet.png

cp output/wormhole_structure_observatory/20260514T045629Z/wormhole_structure_contact_sheet.png \
   Docs/assets/overview/wormhole_structure_contact_sheet.png
```

Do not commit the `output/` directory. Only the copied representative images go into `Docs/assets/overview/`.

The `Docs/assets/overview/` directory is now created and ready.

---

## 6. Risks & Cleanup Notes

| Risk | Notes |
|------|-------|
| Duplicate nav entries | Current nav has Cathedral Probe section AND Cathedral Probe entries inside Research — resolved in new nav |
| Standalone Diagnostics tab | Merged into Observatory — same pages, better discovery |
| Large committed images | `fig_01_architecture.png` (6.1MB) slows GitHub Pages build; optimize separately |
| `output/` commit risk | 4.1GB in output/ must stay gitignored; only copy singles to `Docs/assets/overview/` |
| `_Marketing/` tone | Marketing dialogue script is creative/entertainment; keep separate from main docs nav |
| `index_obs.md` / `README_obs.md` duplication | Two observatory-variant index files exist; they are not in the nav; they could be added under Overview or left as dev references |
| `Research/OPTICAL_TRANSPORT_FEATURE_MAP.md` in Research section | MkDocs sorts Research section alphabetically if no explicit order; placing it first in the Research nav entry shows it prominently |

---

## 7. Implementation Checklist

- [x] Create `Docs/MkDocs/` directory
- [x] Write this plan file
- [x] Create `Docs/Overview/` directory
- [x] Write `Docs/Overview/TRANSPORT_OBSERVATORY_OVERVIEW.md`
- [x] Update `Docs/index.md` (add framing, maturity table, audit links)
- [x] Update `mkdocs.yml` (add Overview, Observatory, Release, MisterY Labs sections)
- [ ] Copy observatory contact sheets to `Docs/assets/overview/` (requires manual step from output/)
- [ ] Optimize oversized assets (`fig_01_architecture.png`, `xprimeray-blueprint.png`)

---

## 8. Next Implementation Prompt

```
Now implement the following:

1. Copy two images into Docs/assets/overview/ (see asset movement section above)
2. Run: cd /home/bb/code/godot_xPRIMEray && .venv/bin/mkdocs build --strict
   to verify the new nav compiles without broken links
3. If any broken links appear, resolve by checking file paths in the nav section
4. Push site/ to GitHub Pages: mkdocs gh-deploy
```

---

*This plan reflects the state as of 2026-05-14. All file paths are relative to `/home/bb/code/godot_xPRIMEray/`.*
