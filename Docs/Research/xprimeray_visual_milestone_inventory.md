# xPRIMEray Visual Milestone Inventory

*Historical archaeology pass — rendered outputs, diagnostic overlays, and milestone images from the xPRIMEray repository. Organized chronologically. Separates live engine capability from engineering harness and post-process analysis.*

---

## Era 0 — First Light: Interactive GRIN Rendering (Feb 2026)

The earliest screenshots show xPRIMEray operating as an interactive Godot scene: GRIN field bending visible in real-time, multiple field sources active, camera movement captured.

| Artifact | Path | Date | Type | Classification |
|---|---|---|---|---|
| GenericScene 3-field baseline | `Docs/screenshots/2026-02-26_GenericScene-3fields-5obj.png` | 2026-02-26 | In-game screenshot | **A** — In-game runtime |
| GenericScene 4-field baseline | `Docs/screenshots/2026-02-26_GenericScene-4fields-5obj.png` | 2026-02-26 | In-game screenshot | **A** — In-game runtime |
| GenericScene camera move | `Docs/screenshots/2026-02-26_GenericScene-4fields-5obj_camera-move.png` | 2026-02-26 | In-game screenshot | **A** — In-game runtime |
| GenericScene high-angle move | `Docs/screenshots/2026-02-26_GenericScene-4fields-5obj_camera-highmove.png` | 2026-02-26 | In-game screenshot | **A** — In-game runtime |
| GenericScene negative curvature | `Docs/screenshots/2026-02-26_GenericScene-4fields-5obj_negCurvature.png` | 2026-02-26 | In-game screenshot | **A** — In-game runtime |

**Historical significance:** First evidence of working curved-ray integration in interactive Godot. The negative curvature capture confirms bidirectional field response was working this early.

---

## Era 1 — CurvedMinimal Fixture & Visual Parameter Sweeps (Mar 2026)

The introduction of the `CurvedMinimalFixture` and the first systematic parameter DOEs. The `grin_basic_visual_sweep.py` and `metric_basic_visual_sweep.py` tools produced grid comparisons of amplitude, gamma, step, gain, and depth.

### Fixture baseline

| Artifact | Path | Date | Type | Classification |
|---|---|---|---|---|
| CurvedMinimal first fixture | `Docs/screenshots/2026-03-01_CurvedMinimalFixture.png` | 2026-03-01 | Fixture harness | **B** — Test harness |
| Metric basic visual smoke | `Docs/screenshots/metric_basic_visual_smoke.png` | 2026-03-14 | Smoke test | **B** — Test harness |

### GRIN parameter sweep (2026-03-14)

| Artifact | Path | Visual feature |
|---|---|---|
| Straight reference | `Docs/screenshots/grin_basic_visual_sweep/2026-03-14/straight_reference.png` | Null-transport baseline |
| Minimal baseline | `Docs/screenshots/grin_basic_visual_sweep/2026-03-14/minimal_baseline.png` | GRIN minimal field |
| Stronger baseline | `Docs/screenshots/grin_basic_visual_sweep/2026-03-14/stronger_baseline.png` | GRIN stronger field |
| Manual settle contacts | `Docs/screenshots/grin_basic_visual_sweep/2026-03-14/manual_*.png` | Hand-tuned presets |
| DOE contact sheet | `Docs/screenshots/grin_basic_visual_sweep/2026-03-14/contact_sheet.png` | Full 20-cell DOE grid |

**Classification: B** (test harness DOE), **historical significance:** establishes the amplitude/gamma parameter response baseline.

### Metric parameter sweep (2026-03-14)

| Artifact | Path | Visual feature |
|---|---|---|
| Metric comparison sheet | `Docs/screenshots/metric_basic_visual_sweep/2026-03-14/comparison_sheet.png` | GRIN vs Metric comparison |
| Film crop compare | `Docs/screenshots/metric_basic_visual_sweep/2026-03-14/film_crop_compare.png` | Film-plane crop comparison |
| Full sweep contact sheet | `Docs/screenshots/metric_basic_visual_sweep/2026-03-14/contact_sheet.png` | Metric DOE grid |

### Metric step/gain/depth sweep (2026-03-15)

These are the first images showing step-length sensitivity and gain scaling as controlled DOE:

| Artifact | Path | Visual feature |
|---|---|---|
| Step ×0.25 vs baseline | `Docs/screenshots/metric_basic_visual_sweep/2026-03-15/comparisons/minimal_step_0p25x_vs_minimal_baseline.png` | Step sensitivity |
| Gain ×10 vs baseline | `Docs/screenshots/metric_basic_visual_sweep/2026-03-15/comparisons/minimal_gain_10x_vs_minimal_baseline.png` | Gain response |
| Depth ×4 vs baseline | `Docs/screenshots/metric_basic_visual_sweep/2026-03-15/comparisons/minimal_depth_4x_vs_minimal_baseline.png` | Integration depth |
| Patch detail | `Docs/screenshots/metric_basic_visual_sweep/2026-03-15_patch/*.png` | Sub-pixel detail crops |

**Classification: B** (test harness DOE).

### Off-axis / observe sweeps (2026-03-15)

First captures of off-axis camera angles and the `observe` camera mode, with straight-reference side-by-side comparisons.

| Artifact | Path | Visual feature |
|---|---|---|
| On/off-axis ladder sheet | `Docs/screenshots/basic_visual_offaxis/2026-03-15/offaxis_ladder_sheet.png` | Off-axis bending ladder |
| On vs off-axis comparison | `Docs/screenshots/basic_visual_offaxis/2026-03-15/onaxis_vs_offaxis_sheet.png` | Curved vs reference |
| Straight off-axis reference | `Docs/screenshots/basic_visual_offaxis/2026-03-15/straight_offaxis_reference.png` | Null baseline |
| Observe contact sheet | `Docs/screenshots/basic_visual_offaxis_observe/2026-03-15/observe_contact_sheet.png` | Observer-mode contact |
| Old vs observe comparison | `Docs/screenshots/basic_visual_offaxis_observe/2026-03-15/old_vs_observe_comparison_sheet.png` | Mode migration evidence |

**Classification: B/A** — off-axis shots are in-game captures; observe mode compares two rendering modes.

---

## Era 2 — Telemetry Heatmaps & Performance Diagnostics (Mar–Apr 2026)

The `telemetry_heatmap_*` runs introduced per-pixel performance heatmaps: candidates, pass-1 steps, query cost, resolve cost, work cost. Also the first curvature derivative heatmaps (`heat_curvature_max`, `heat_d2k_max`, `heat_dk_max`).

| Artifact | Path | Visual feature |
|---|---|---|
| Heat candidates | `output/telemetry_heatmap_quick/*.heat_candidates.png` | BVH candidate count map |
| Heat pass-1 steps | `output/telemetry_heatmap_quick/*.heat_pass1_steps.png` | Integration step density map |
| Heat query | `output/telemetry_heatmap_quick/*.heat_query.png` | Query cost per pixel |
| Heat resolve | `output/telemetry_heatmap_quick/*.heat_resolve.png` | Resolver cost per pixel |
| Heat work | `output/telemetry_heatmap_quick/*.heat_work.png` | Total work per pixel |
| Curvature max | `output/telemetry_heatmap_v2_sample/*.heat_curvature_max.png` | Kmax curvature field |
| Curvature dK/dt | `output/telemetry_heatmap_v2_sample/*.heat_dk_max.png` | First curvature derivative |
| Curvature d²K/dt² | `output/telemetry_heatmap_v2_sample/*.heat_d2k_max.png` | Second derivative — oscillation |
| Query minus curvature | `output/telemetry_heatmap_v2_sample/*.heat_query_minus_curvature.png` | Cost not explained by curvature |

**Classification: B** — test harness only. Produced by `scripts/run_*.sh` + Python analysis. No in-game equivalent.

---

## Era 3 — Regression Pack & Renderer Compact Tests (Mar 2026)

| Artifact | Path | Visual feature |
|---|---|---|
| Flat baseline (no field) | `Docs/screenshots/renderer_compact_regression_pack/2026-03-18/flat_baseline_no_field.png` | Straight reference with no field |
| GRIN curved minimal | `Docs/screenshots/renderer_compact_regression_pack/2026-03-18/grin_curved_minimal.png` | GRIN curved transport |
| Metric off-axis observe | `Docs/screenshots/renderer_compact_regression_pack/2026-03-18/metric_minimal_offaxis_observe.png` | Metric off-axis observe mode |

**Classification: B** — regression pack for CI/validation. Produced by `renderer_compact_regression_pack.py`.

---

## Era 4 — Fixture Series 001–007: Systematic Transport Validation (Mar–Apr 2026)

The numbered fixture system establishes hermetic reproducibility. Each fixture run produces a `capture.png` at a fixed camera position with fixed transport parameters.

| Fixture | Output path | Scene | Historical significance |
|---|---|---|---|
| Fixture 001 | `output/fixture_001/2026-03-19*/capture.png` | Radial GRIN baseline | First hermetic fixture — coverage contract established |
| Fixture 002 | `output/fixture_002/` | Linear transport baseline | Horizontal-field transport |
| Fixture 003 | `output/fixture_003/` | Offset field baseline | Off-center field transport |
| Fixture 004 | `output/fixture_004/` | Dual attractor baseline | Two-field interference |
| Fixture 005 | `output/fixture_005/` | (extended) | — |
| Fixture 006 | `output/fixture_006_topology/` | Topology test | Boundary topology cases |
| Fixture 007 | `output/fixture_007_field/` | Field stress | Field evaluation stress |

The fixture_001 directory has 60+ run timestamps showing iterative parameter tuning. Step sweep subcategories: `step_0p045`, `step_0p050`, `step_0p055`, `step_followup`, `step_expanded`.

**Classification: B** — test harness only. Scripts: `scripts/run_fixture_001.sh` etc.

---

## Era 5 — Derivative Step Experiments (Apr 2026)

The `exp1_derivative_step_v0` through `v4` series tested derivative-aware adaptive stepping as an alternative to fixed RK4 step control. Parallel renders of `curved_minimal` and `curved_minimal_backdrop` scenes.

| Artifact | Path | Visual feature |
|---|---|---|
| v0 baseline vs firstpass | `output/exp1_derivative_step_v0/` | Derivative step v0 |
| v1 curved/backdrop comparison | `output/exp1_derivative_step_v1/` | v1 hold-state comparisons |
| v2 baseline/firstpass | `output/exp1_derivative_step_v2/` | v2 hold-state comparisons |
| v3 baseline/firstpass | `output/exp1_derivative_step_v3/` | v3 hold-state comparisons |
| v4 hold1/hold2/hold3/hold4 | `output/exp1_derivative_step_v4/` | v4 four hold states |

**Classification: B** — engineering experiment. Evidence of derivative step research before the current fixed-RK4 production path.

---

## Era 6 — Wormhole Test & Dual Reality First Renders (Apr 2026)

The first wormhole renders with layered dual-reality overlays. The `wormhole_test` and `wormhole_DR_analysis` runs are the earliest evidence of the Dual-Reality Overlay system working end-to-end.

### Wormhole test (early April)

| Artifact | Path | Visual feature |
|---|---|---|
| Main render | `output/wormhole_test/figures/figure_A_main_render.png` | Wormhole beauty render |
| Composed overlay | `output/wormhole_test/figures/figure_B_composed_overlay.png` | First dual-reality compose |
| Ring density | `output/wormhole_test/figures/figure_C_ring_density.png` | Portal ring density map |
| Metrics table | `output/wormhole_test/figures/figure_D_metrics_table.png` | Rendered metrics table |
| Phase space | `output/wormhole_test/figures/figure_E_phase_space.png` | Phase space diagram |
| Portal ring density | `output/wormhole_test/wormhole_portal_ring_density.png` | Portal ring |
| Portal sector heatmap | `output/wormhole_test/wormhole_portal_sector_heatmap.png` | Sector heat |
| Domain confidence | `output/wormhole_test/wormhole_validation_capture.domain_confidence.png` | First domain confidence map |
| Boundary confidence | `output/wormhole_test/wormhole_validation_capture.boundary_confidence.png` | First boundary confidence map |
| Wormhole capture (Docs) | `Docs/wormhole_test/wormhole_validation_capture.png` | Curated baseline render |
| Composed overlay (Docs) | `Docs/wormhole_test/wormhole_validation_composed.png` | Curated composite |

**Classification: C** (post-process analysis from harness). `domain_confidence` and `boundary_confidence` are the first visual evidence of the `EnableDomainTelemetry` pipeline.

### Dual Reality analysis (2026-04-10)

Two timestamps: `2026-04-10T08-00-00` (5-layer DR stack) and `2026-04-10T16-10-00` (curvature added).

| Artifact | Path | Visual feature |
|---|---|---|
| Clean curved render | `output/wormhole_DR_analysis/*/images/wormhole_clean_curved.png` | Beauty curved-transport render |
| Reference only | `output/wormhole_DR_analysis/*/images/wormhole_reference_only.png` | Straight reference inset |
| Reference + collision | `output/wormhole_DR_analysis/*/images/wormhole_reference_plus_collision.png` | Collision radar overlay |
| Reference + semantic | `output/wormhole_DR_analysis/*/images/wormhole_reference_plus_semantic.png` | Wireframe glyph overlay |
| Reference + curvature | `output/wormhole_DR_analysis/*/images/wormhole_reference_plus_curvature.png` | Curvature heatmap overlay |
| Full stack | `output/wormhole_DR_analysis/*/images/wormhole_full_stack*.png` | All layers composited |
| Straight transport reference comparisons | `output/wormhole_DR_analysis/*/support/*straight_transport_reference.png` | Side-by-side curved vs straight |
| Resized analysis | `output/wormhole_DR_analysis/*/analysis/*.png` | Normalized size comparison |

**Dual Reality Storytelling contact sheet:** `output/wormhole_DR_Story/latest/wormhole_dual_reality_storytelling_contact_sheet.png`

**Classification: C** (produced by `tools/wormhole_dual_reality_analysis.py` and `wormhole_dual_reality_storytelling.py`). The *in-game runtime* for these layers is separately confirmed in `WormholePrototypeRig.cs` (see Feature Maturity Matrix).

---

## Era 7 — Wormhole Inset & Camera Distance Sweep (Apr 2026)

| Artifact | Path | Visual feature |
|---|---|---|
| Wormhole inset baseline | `output/dual_reality/wormhole_inset_baseline.png` | Straight reference inset in curved render |
| Curvature full stack | `output/dual_reality/wormhole_curvature_full_stack.png` | Curvature overlay composited |
| Curvature reference | `output/dual_reality/wormhole_curvature_reference.png` | Curvature reference only |
| Collision radar | `output/dual_reality/wormhole_collision_radar.png` | Collision radar overlay |
| Camera dist sweep (backoff) | `output/camera_dist_sweep/*.png` | Camera backoff sweep |
| Fine-grained backoff | `output/camera_dist_sweep/fine_grained/*.png` | Sub-unit backoff steps |

**Classification: C/B** (post-process analysis and test harness).

---

## Era 8 — Fixture 008–013: Wormhole Observer Ladder (Apr 2026)

The wormhole witness fixtures are the first hermetic captures of the full observer ladder through the wormhole (mouth → throat → exit) at multiple camera positions.

| Fixture | Output path | Checkpoint | Visual feature |
|---|---|---|---|
| Fixture 008 | `output/fixture_runs/fixture_008_wormhole_witness/` | Witness (outer) | Wormhole mouth from approach |
| Fixture 008 mouth | `output/fixture_runs/fixture_008_wormhole_witness_mouth/` | Mouth close | Mouth interior view |
| Fixture 009 | `output/fixture_runs/fixture_009_wormhole_witness_throat/` | Throat | Throat crossing |
| Fixture 010 | `output/fixture_runs/fixture_010_wormhole_witness_exit/` | Exit | Far-side exit view |
| Fixture 011 | `output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/` | Sequence (mouth+throat+exit) | Three-checkpoint sequence |
| Fixture 012 | `output/fixture_runs/fixture_012_wormhole_mouth_throat_interpolation/` | Interpolated path | Smooth interpolation |
| Fixture 013 | `output/fixture_runs/fixture_013_wormhole_throat_exit_interpolation/` | Interpolated exit | Throat-exit interpolation |

Each fixture run produces: `capture.png` (beauty), `debug_capture.png`, `coverage_annotated.png`, `throat_depth_map.png`, `throat_depth_annotated.png`.

Fixture 011 is the **canonical observer ladder fixture** used in all Papers 001–004. It produces `00_mouth_capture.png`, `01_throat_capture.png`, `02_exit_lookback_capture.png` per run.

**Classification: B** — test harness only.

---

## Era 8b — Paper 001 Analysis Figures (Apr 2026)

The morphology analysis series from `Docs/papers/paper_001_causal_observer_ladders/analysis/morphology/` is the first evidence of structured transport science in this repo.

| Artifact | Path | Visual feature |
|---|---|---|
| Annotated mouth | `analysis/morphology/annotated/mouth_annotated.png` | Annotated transport at mouth |
| Annotated throat | `analysis/morphology/annotated/throat_annotated.png` | Annotated throat crossing |
| Annotated exit lookback | `analysis/morphology/annotated/exit_lookback_annotated.png` | Far-side lookback |
| Post-throat backstep | `analysis/morphology/annotated/post_throat_backstep_01_annotated.png` | Bridge anomaly |
| Radial profiles | `analysis/morphology/profiles/*.png` | Per-checkpoint radial profiles |
| Anomaly scores | `analysis/anomaly_detection/figures/checkpoint_anomaly_scores.png` | Bridge anomaly z-score |
| Regime clustering | `analysis/figures/regime_clustering.png` | k=3 PCA cluster result |
| Segments per crossing | `figures/segments_per_crossing_vs_checkpoint.png` | Transport anomaly signal |
| Throat event density | `figures/throat_event_density_vs_checkpoint.png` | Throat event rate |
| OPL mean vs checkpoint | `figures/opl_mean_vs_checkpoint.png` | Optical path length ladder |

**Classification: C** — post-process analysis only.

---

## Era 9 — Domain Telemetry Maps (Apr 2026)

The `domain_audit_visual` series is the first evidence of all five domain telemetry channels exported as per-pixel heatmaps. These are gated by `EnableDomainTelemetry`.

| Artifact | Path | Visual feature |
|---|---|---|
| Domain ID map | `output/domain_audit_visual/*/resolver_on/*.domain_id.png` | Per-pixel domain assignment |
| Domain confidence | `output/domain_audit_visual/*/resolver_on/*.domain_confidence.png` | Domain confidence score |
| Boundary confidence | `output/domain_audit_visual/*/resolver_on/*.boundary_confidence.png` | Boundary proximity |
| Selection flip | `output/domain_audit_visual/*/resolver_on/*.selection_flip.png` | Domain resolver flip events |
| Normal discontinuity | `output/domain_audit_visual/*/resolver_on/*.normal_discontinuity.png` | Surface normal discontinuity |
| Resolver diff | `output/domain_audit_visual/*/resolver_diff.png` | Domain resolver on vs off diff |
| Telemetry vs resolver contact | `output/domain_audit_visual/*/contact_sheet.png` | Side-by-side three-mode contact |
| Off vs tel diff heatmap | `output/domain_audit_visual/*/off_vs_tel_diff_heatmap.png` | Telemetry delta heatmap |

**Classification: B** — produced via test harness (`run_domain_audit_visual.sh`) and Python analysis. In-game runtime for the raw domain IDs is confirmed via `GrinFilmCamera.EnableDomainTelemetry` → CSV/JSON export. In-game visualization of the telemetry as heatmaps is **not yet implemented** (see Feature Maturity Matrix).

---

## Era 10 — Overspace First Milestone (Apr 2026)

The first evidence of the OverspaceRig — a wormhole rabbit-hole nesting scene with path traversal frames.

| Artifact | Path | Visual feature |
|---|---|---|
| Overspace contact sheet | `output/overspace_first_milestone/latest/overspace_first_milestone_contact_sheet.png` | 6-frame path sequence |
| Frame 01: path start | `output/overspace_first_milestone/latest/images/01_path_start.png` | Entry |
| Frames 02–06 | `output/overspace_first_milestone/latest/images/0{2-6}_path_*.png` | 20/40/60/80/100% progress |
| Runtime check | `output/overspace_first_milestone/runtime_check.png` | In-game runtime screenshot |

**Classification: A/B** — `runtime_check.png` is an in-game runtime capture. The contact sheet is post-process. **Historical significance:** First visual evidence of the overspace nesting architecture working.

---

## Era 11 — Cathedral Probe: Scheduler DOE & Traversal Analysis (May 2026)

The definitive transport instability investigation. Full documentation in `cathedral_probe_architecture.md`.

| Artifact | Path (output) | Visual feature |
|---|---|---|
| Scheduler stride plot | `output/doe_scheduler_resonance/20260502T155725Z/scheduler_stride_plot.png` | Stride 1→4→8 band% collapse |
| Band score vs step | `output/doe_scheduler_resonance/20260502T155725Z/horizontal_band_score_plot.png` | Step-length invariant banding |
| Band-by-row heatmap | `output/doe_scheduler_resonance/20260502T155725Z/band_by_row_mod_stride_heatmap.png` | Row resonance heatmap |
| Step sensitivity band plot | `output/doe_overnight/20260502T060652Z/DOE_overnight_band_plot.png` | Band% vs step non-monotonic |
| 4-mode traversal sheet | `output/tile_commit_traversal_comparison/20260503T231337Z/traversal_contact_sheet.png` | Row/col/tile/checkerboard |
| Row vs tile diff | `output/tile_commit_traversal_comparison/20260503T231337Z/row_vs_tile_diff.png` | Tile decorrelation |
| Band support by mode | `output/tile_commit_traversal_comparison/20260503T231337Z/band_support_by_mode.png` | Mode band reduction |
| Corner flip map | `output/corner_transport_probe/20260503T132655Z/corner_collider_flip_map.png` | Ownership flip at seam |
| Corner precision map | `output/corner_transport_probe/20260503T132655Z/corner_required_precision_map.png` | Required precision |
| Corner convergence | `output/corner_transport_probe/20260503T132655Z/corner_convergence_profile.png` | Decision risk vs step |
| Six-layer overlay | `output/tile_commit_traversal_comparison/20260503T231337Z/beauty/step_0.015/row/combined_diagnostic_overlay.png` | Cathedral Probe composite |
| Overlay contact sheet | `output/tile_commit_traversal_comparison/20260503T231337Z/beauty/step_0.015/row/diagnostic_overlay_contact_sheet.png` | Six-layer individual panels |
| Continuity vectors | `output/tile_commit_traversal_comparison/20260503T231337Z/beauty/step_0.015/row/layer5_transport_continuity_vectors.png` | 6,619 discontinuity vectors |
| Transport shape regions | `output/tile_commit_traversal_comparison/20260503T231337Z/beauty/step_0.015/row/transport_shape_regions_overlay.png` | Ownership contour overlay |

**Classification: C** — post-process analysis only. Produced by `tools/diagnostic_wireframe_overlay.py`, `tile_commit_traversal_analysis.py`, `doe_scheduler_resonance_analysis.py`, `doe_overnight_analysis.py`, `corner_transport_probe_analyzer.py`.

---

## Era 12 — Transport Ownership Graph & Oracle (May 2026)

| Artifact | Path (output) | Visual feature |
|---|---|---|
| Ownership graph precision sweep | `output/transport_ownership_graph_precision_sweep/20260504T043955Z/` | Graph construction at multiple step lengths |
| ROI sweep convergence ladder | `output/reference_transport_oracle_roi_sweep/20260505T034858Z/cells/row_stride_1/convergence_ladder_contact_sheet.png` | Full-scene stability map |
| ROI sweep epsilon stability | `output/reference_transport_oracle_roi_sweep/20260505T034858Z/cells/row_stride_1/epsilon_stability_map.png` | 266 stable, 54 unresolved |
| ROI oracle path overlay | `output/reference_transport_oracle_roi_sweep/20260505T034858Z/cells/row_stride_1/oracle_path_overlay.png` | Oracle trajectories |
| Island diagnostic sheet | `output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/diagnostic_overlay_contact_sheet.png` | Dense island 6-layer |
| Island epsilon stability | `output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/epsilon_stability_map.png` | 289/289 Stable |
| Island first stable step | `output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/first_stable_step_map.png` | Spatial precision gradient |
| Island convergence ladder | `output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/island_convergence_ladder.png` | Patch convergence ladder |

**Classification: C** — post-process analysis. Scripts: `run_reference_transport_oracle_roi_sweep.sh`, `run_reference_transport_oracle_unresolved_island.sh`, `tools/reference_transport_oracle_analysis.py`, `tools/reference_transport_oracle_island_analysis.py`.

---

## Special: Logo and Brand Assets (Apr 2026)

See [xprimeray_logo_usage_recommendation.md](xprimeray_logo_usage_recommendation.md).

| Asset | Path | Notes |
|---|---|---|
| Primary logo (PNG) | `Docs/assets/xPRIMEray-LOGO.png` | 904 KB — full-color logo |
| Dark variant | `Docs/assets/xprimeray-logo-dark.png` | 3.1 MB — dark background |
| SVG logo | `Docs/assets/xprimeray-logo.svg` | 4.2 KB — vector scalable |
| Icon SVG | `Docs/assets/xprimeray-icon.svg` | 3.4 KB — square icon |
| Blueprint render | `Docs/assets/xprimeray-blueprint.png` | 4.7 MB — technical blueprint style |
| AI concept image | `Docs/assets/ChatGPT Image Apr 20, 2026, 12_26_28 AM.png` | 591 KB — ChatGPT-generated concept |

---

## Special: Architecture Diagram (Docs)

| Asset | Path | Notes |
|---|---|---|
| Architecture diagram | `Docs/assets/fig_01_architecture.png` | 6.1 MB — full pipeline diagram |
| Wormhole inset (Docs) | `Docs/assets/wormhole_inset_baseline.png` | 314 KB — curated dual-reality inset |

---

## Output Directory Summary

| Directory | Artifact count | Era | Classification |
|---|---|---|---|
| `output/camera_dist_sweep/` | 14 PNGs | 7 | B/C |
| `output/corner_transport_probe/` | ~10 PNGs | 11 | C |
| `output/doe_overnight/` | 1 plot | 11 | C |
| `output/doe_scheduler_resonance/` | 3 plots | 11 | C |
| `output/domain_audit_visual/` | ~50 PNGs | 9 | B/C |
| `output/dual_reality/` | 4 PNGs | 7 | C |
| `output/exp1_derivative_step_v0-v4/` | 22 PNGs | 5 | B |
| `output/fixture_001-007/` | 100+ PNGs | 4 | B |
| `output/fixture_runs/fixture_008-013/` | ~100 PNGs | 8 | B |
| `output/overspace_first_milestone/` | 8 PNGs | 10 | A/B |
| `output/reference_transport_oracle_*/` | ~60 PNGs | 12 | C |
| `output/telemetry_heatmap_*/` | ~70 PNGs | 2 | B/C |
| `output/tile_commit_traversal_comparison/` | ~50 PNGs | 11 | C |
| `output/transport_ownership_graph_*/` | few | 12 | C |
| `output/wormhole_DR_analysis/` | ~25 PNGs | 6 | C |
| `output/wormhole_DR_Story/` | 8 PNGs | 6 | C |
| `output/wormhole_test/` | 10 PNGs | 6 | C |
| `Docs/screenshots/` | ~100 PNGs | 0–3 | A/B |
| `Docs/papers/paper_001*/` | ~25 PNGs | 8b | C |
| `Docs/assets/cathedral_probe/` | 22 PNGs | 11 | C |
| `Docs/assets/transport_islands/` | 19 PNGs | 12 | C |

**Classification key:** A = in-game runtime · B = test harness/engineering only · C = post-process analysis only · D = obsolete/prototype
