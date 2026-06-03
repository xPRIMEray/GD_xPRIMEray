# Curved-Field Validation Ladder Summary

Renderer-validation grounded. This report does not make physical-truth claims.

- Curved validation status: **warning**
- Comparability status: **warning**
- Requested control mode: **scene_control**
- Control comparison type: **scene_control**
- Control comparison reason: configured scene-control fixture
- Storyboard: `curved_vs_control_storyboard.png`
- Storyboard selection: `storyboard_selection.json`
- Temporal observability: `topology_evolution_strip.png`, `transport_phase_evolution_strip.png`, `budget_evolution_strip.png`
- Animated observability: `ownership_graph_evolution.gif`, `budget_heatmap_evolution.gif`, `diagnostic_storyboard_evolution.gif`

## Visual Hierarchy

- The storyboard sequence is geometry -> transport -> topology -> quality/budget.
- Cartesian wireframe projection is treated as the foundational coordinate-space anchor.
- Representative frames are selected by visible geometry, hit support, topology signal, overlay availability, and budget-exhaustion penalties.
- Temporal outputs use real captured step artifacts only. No cinematic interpolation or inferred in-between frames are generated.

## Evidence Tiers

- tier_a_fixture_curvature_engaged: pass
- tier_b_renderer_diagnostics_changed: pass
- tier_c_topology_changed_across_step_ladder: not_detected
- tier_d_unresolved_island_sealed_or_persisted: unknown

## Curvature Evidence

- curvature metric log present: True
- resolved log present: True
- nonzero curvature params: True
- curved transport enabled: True
- status reason: ok

## Validation Inference

- diagnostics changed vs control: True
- graph_delta_vs_control: `{"edge_count_delta": -1, "high_discontinuity_edge_delta": -1, "merge_split_delta": 0, "node_count_delta": -1, "seam_length_delta": -762, "unresolved_count_delta": 0}`
- budget diminishing returns: `{"control": {"last_pre_budget_step": "", "likely_point_of_diminishing_returns": "0.015", "reason": "quality improves until step before first sampled step, then budget exhaustion appears at step 0.015"}, "curved": {"last_pre_budget_step": "0.015", "likely_point_of_diminishing_returns": "", "reason": "no traversal budget exhaustion detected across sampled steps"}}`
- transport quality phases: `{"control": {"budget_saturation_start_step": "0.015", "phase_counts": {"budget_saturated": 1}, "plateau_start_step": ""}, "curved": {"budget_saturation_start_step": "", "phase_counts": {"underresolved": 1}, "plateau_start_step": ""}}`
- storyboard representatives: control step `0.015`, curved step `0.015`
- temporal evolution role: `curved`
- oracle comparisons: 8
- budget saturation ladder: `curved_ladder_budget_saturation.csv`
- transport quality phase plot: `transport_quality_phase_plot.png`

## Phase Interpretation

- `underresolved`: transport evidence is still missing or disagrees with oracle/sample diagnostics; reduce step size or focus the oracle/island microscope.
- `converging`: graph, seam, hit, or oracle metrics are still changing; continue the ladder around neighboring step values.
- `plateau`: metrics are locally stable without budget saturation; treat this as a candidate operating window or diminishing-returns region.
- `budget_saturated`: traversal budget exhaustion is present; increase max traversal/step budget or use adaptive budget scaling before trusting smaller-step conclusions.

## Comparability Warnings

- step=0.015 field=camera_pose_key control=domain_resolver_stress:res://test-domain-resolver-stress.tscn curved=curved_minimal_backdrop:res://test-curved-minimal-backdrop.tscn

## Guardrail

Do not describe visible band/support artifacts as caused by curvature unless comparison metrics support that claim; use 'associated with curved transport fixture under tested settings.'

