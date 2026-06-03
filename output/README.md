# xPRIMEray Output Artifacts

This directory contains all experiment, test, and visual outputs from the xPRIMEray optical transport renderer. Each subdirectory is a self-describing artifact folder: copy any folder to the MisterY Labs site and the README.md inside it provides full context.

## Organization

Outputs are grouped by experiment type. Each folder contains a `README.md` with:
- What the output demonstrates
- Which script/scene/workflow generated it
- What the key files are
- A suggested card summary for the website
- Status and next steps

## Artifact Index

### Visual Benchmarks & Observatories

| Folder | Description | Status |
|--------|-------------|--------|
| [atomic_orbital_visual_observatory](atomic_orbital_visual_observatory/) | GRIN atomic orbital parameter ladder — contact sheet | Visual reference |
| [atomic_orbital_grin_ladder](atomic_orbital_grin_ladder/) | GRIN efficiency sweep across pruning variants | Test output |
| [atomic_orbital_grin_smoke](atomic_orbital_grin_smoke/) | A2 hydrogen quick smoke renders | Test output |
| [causal_observatory_testbench](causal_observatory_testbench/) | Causal tile scheduler validation | Validation candidate |
| [wormhole_structure_observatory](wormhole_structure_observatory/) | Wormhole mouth/throat/exit contact sheet | Visual reference |
| [wormhole_DR_Story](wormhole_DR_Story/) | Six-step wormhole DualReality storytelling sequence | Visual reference |
| [wormhole_DR_analysis](wormhole_DR_analysis/) | Wormhole transport layer analysis | Test output |
| [overspace_first_milestone](overspace_first_milestone/) | First overspace render — path approach ladder | Visual reference |
| [recursive_mirror_ghost_portal](recursive_mirror_ghost_portal/) | Phase 1 mirror/refraction testbench | Draft |
| [dual_reality](dual_reality/) | Wormhole dual reality multi-overlay | Archived |
| [observer_disagreement](observer_disagreement/) | Curved vs. straight ray classification delta | Validation candidate |
| [v0.0-pre](v0.0-pre/) | Pre-release v0 hermetic baseline renders | Archived |

### Design of Experiments

| Folder | Description | Status |
|--------|-------------|--------|
| [doe_overnight](doe_overnight/) | Step length vs. telemetry mode — 18 cells | Test output |
| [doe_scheduler_resonance](doe_scheduler_resonance/) | Stride × mode resonance sweep | Test output |
| [doe_sensitivity](doe_sensitivity/) | Pruning × stride full-factorial | Test output |
| [doe_sensitivity_smoke](doe_sensitivity_smoke/) | Quick sensitivity DOE smoke | Archived |
| [exp1_derivative_step_v0](exp1_derivative_step_v0/) | Derivative step experiment — v0 backdrop baseline | Archived |
| [exp1_derivative_step_v1](exp1_derivative_step_v1/) | Derivative step — v1 two-scene | Archived |
| [exp1_derivative_step_v2](exp1_derivative_step_v2/) | Derivative step — v2 stability | Archived |
| [exp1_derivative_step_v3](exp1_derivative_step_v3/) | Derivative step — v3 pre-hold | Archived |
| [exp1_derivative_step_v4](exp1_derivative_step_v4/) | Derivative step — v4 hold variants | Test output |

### Transport & Domain Analysis

| Folder | Description | Status |
|--------|-------------|--------|
| [transport_coherence_basin_smoke](transport_coherence_basin_smoke/) | Transport coherence risk basin smoke | Validation candidate |
| [transport_coherence_basin_repeatability](transport_coherence_basin_repeatability/) | Transport coherence repeatability | Validation candidate |
| [transport_ownership_graph_precision_sweep](transport_ownership_graph_precision_sweep/) | Ownership graph precision sweep | Test output |
| [corner_transport_probe](corner_transport_probe/) | Corner edge stability probe | Validation candidate |
| [reference_geodesic_probe_smoke](reference_geodesic_probe_smoke/) | Geodesic probe convergence smoke | Test output |
| [reference_transport_oracle_roi_sweep](reference_transport_oracle_roi_sweep/) | Transport oracle ROI stride sweep | Test output |
| [reference_transport_oracle_unresolved_island](reference_transport_oracle_unresolved_island/) | Unresolved ownership island investigation | Validation candidate |
| [domain_audit](domain_audit/) | Domain audit off-mode baseline | Archived |
| [domain_audit_quick](domain_audit_quick/) | Domain audit telemetry smoke | Test output |
| [domain_audit_visual](domain_audit_visual/) | Domain audit visual comparison suite | Visual reference |
| [domain_aware_first_hit_validation](domain_aware_first_hit_validation/) | Domain-aware first-hit before/after | Validation candidate |
| [domain_resolver_stress](domain_resolver_stress/) | Domain resolver stress harness | Test output |
| [domain_telemetry_validation](domain_telemetry_validation/) | Domain telemetry cross-scene validation | Test output |

### Traversal & Scheduler

| Folder | Description | Status |
|--------|-------------|--------|
| [first_pass_traversal_comparison](first_pass_traversal_comparison/) | Row vs. column vs. tile traversal | Test output |
| [tile_commit_traversal_comparison](tile_commit_traversal_comparison/) | Tile-commit traversal 9-run comparison | Test output |
| [tile_commit_traversal_repeatability](tile_commit_traversal_repeatability/) | Tile-commit determinism check | Validation candidate |
| [render_scheduler](render_scheduler/) | Baseline vs. reorder scheduler | Test output |
| [render_test_visual_compare](render_test_visual_compare/) | Scheduler/pruning visual comparison | Archived |
| [threaded_band_eval](threaded_band_eval/) | 1/2/4-thread band eval comparison | Test output |

### Telemetry — Adaptive Sweep

| Folder | Description | Status |
|--------|-------------|--------|
| [telemetry_adaptive_compare_baseline](telemetry_adaptive_compare_baseline/) | Adaptive telemetry reference baseline | Test output |
| [telemetry_adaptive_compare_on](telemetry_adaptive_compare_on/) | Adaptive telemetry enabled | Test output |
| [telemetry_adaptive_four_state_warm_p80](telemetry_adaptive_four_state_warm_p80/) | Four-state warm, P80 threshold | Test output |
| [telemetry_adaptive_prior_prevpass](telemetry_adaptive_prior_prevpass/) | Previous-pass prior adaptation | Test output |
| [telemetry_adaptive_regime_neutral085](telemetry_adaptive_regime_neutral085/) | Neutral regime α=0.85 | Test output |
| [telemetry_adaptive_regime_neutral090](telemetry_adaptive_regime_neutral090/) | Neutral regime α=0.90 | Test output |
| [telemetry_adaptive_regime_norelax](telemetry_adaptive_regime_norelax/) | No-relaxation regime | Test output |
| [telemetry_adaptive_regime_ref](telemetry_adaptive_regime_ref/) | Reference regime | Test output |
| [telemetry_adaptive_stat_max](telemetry_adaptive_stat_max/) | Max curvature statistic | Test output |
| [telemetry_adaptive_stat_mean](telemetry_adaptive_stat_mean/) | Mean curvature statistic | Test output |
| [telemetry_adaptive_stat_p90](telemetry_adaptive_stat_p90/) | P90 curvature statistic | Test output |
| [telemetry_adaptive_threshold_fixed08](telemetry_adaptive_threshold_fixed08/) | Fixed threshold 0.8 | Test output |
| [telemetry_adaptive_threshold_v2](telemetry_adaptive_threshold_v2/) | Threshold design v2 | Test output |

### Telemetry — Efficiency Sweep

| Folder | Description | Status |
|--------|-------------|--------|
| [telemetry_eff_baseline](telemetry_eff_baseline/) | Efficiency baseline | Test output |
| [telemetry_eff_env08](telemetry_eff_env08/) | Environment factor 0.8 | Test output |
| [telemetry_eff_env08_fix](telemetry_eff_env08_fix/) | Env 0.8 corrected (canonical) | Test output |
| [telemetry_eff_env08r](telemetry_eff_env08r/) | Env 0.8 repeatability | Test output |
| [telemetry_eff_env09](telemetry_eff_env09/) | Environment factor 0.9 | Test output |
| [telemetry_eff_env09_fix](telemetry_eff_env09_fix/) | Env 0.9 corrected (canonical) | Test output |
| [telemetry_eff_env09r](telemetry_eff_env09r/) | Env 0.9 repeatability | Test output |
| [telemetry_heatmap_quick](telemetry_heatmap_quick/) | Quick heatmap pipeline v1 | Archived |
| [telemetry_heatmap_quick_v2](telemetry_heatmap_quick_v2/) | Quick heatmap pipeline v2 | Archived |
| [telemetry_heatmap_test](telemetry_heatmap_test/) | Stride=2 heatmap test | Test output |
| [telemetry_heatmap_v2_sample](telemetry_heatmap_v2_sample/) | v2 heatmap baseline sample | Archived |

### Fixtures

| Folder | Description | Status |
|--------|-------------|--------|
| [fixture_001](fixture_001/) | 104-cell parameter sweep | Test output |
| [fixture_002](fixture_002/) | Radial field profile | Test output |
| [fixture_003](fixture_003/) | Nearest attractor profile | Test output |
| [fixture_004](fixture_004/) | Sector field profile (extended) | Test output |
| [fixture_005](fixture_005/) | Throat depth map | Test output |
| [fixture_006_topology](fixture_006_topology/) | Wormhole topology characterization | Test output |
| [fixture_007_field](fixture_007_field/) | Field-focused characterization | Test output |
| [fixture_runs](fixture_runs/) | Wormhole checkpoint debug captures | Test output |

### Miscellaneous

| Folder | Description | Status |
|--------|-------------|--------|
| [characterization_ledger](characterization_ledger/) | Fixture test suite completeness ledger | Test output |
| [camera_dist_sweep](camera_dist_sweep/) | Camera backoff distance sweep | Archived |
| [curved_field_validation_ladder](curved_field_validation_ladder/) | GRIN field validation ladder | Validation candidate |
| [hermetic_hit_closure](hermetic_hit_closure/) | Integration escape / closure audit | Validation candidate |
| [shutdown_probe](shutdown_probe/) | Graceful shutdown under load | Test output |
| [testbench](testbench/) | Smoke cache and run manifests | Test output |
| [wormhole_test](wormhole_test/) | Early wormhole validation | Archived |
| [overspace](overspace/) | Overspace staging (empty) | Archived |

## Copying an Artifact

To promote any folder to the MisterY Labs site:
1. Copy the entire folder (including the `README.md` and all timestamped subfolders).
2. The `README.md` is the artifact card content.
3. Point the site card at the PNG(s) listed under **Key Files**.
4. Update **Status** to `Visual reference` once the image is published.
