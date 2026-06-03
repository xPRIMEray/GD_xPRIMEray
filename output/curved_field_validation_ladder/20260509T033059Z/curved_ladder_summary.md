# Curved-Field Validation Ladder Summary

Renderer-validation grounded. This report does not make physical-truth claims.

- Curved validation status: **warning**
- Comparability status: **warning**
- Requested control mode: **matched_pose**
- Control comparison type: **scene_control**
- Control comparison reason: matched_pose requested, but no safe fixture-level curvature bypass or matched-pose flat variant is available; using configured scene-control, not matched-control
- Storyboard: `curved_vs_control_storyboard.png`

## Evidence Tiers

- tier_a_fixture_curvature_engaged: pass
- tier_b_renderer_diagnostics_changed: pass
- tier_c_topology_changed_across_step_ladder: pass
- tier_d_unresolved_island_sealed_or_persisted: sealed

## Curvature Evidence

- curvature metric log present: True
- resolved log present: True
- nonzero curvature params: True
- curved transport enabled: True
- status reason: ok

## Validation Inference

- diagnostics changed vs control: True
- graph_delta_vs_control: `{"edge_count_delta": -103, "high_discontinuity_edge_delta": -101, "merge_split_delta": -112, "node_count_delta": -45, "seam_length_delta": -10599, "unresolved_count_delta": 0}`
- oracle comparisons: 768

## Comparability Warnings

- step=0.02 field=camera_pose_key control=domain_resolver_stress:res://test-domain-resolver-stress.tscn curved=curved_minimal_backdrop:res://test-curved-minimal-backdrop.tscn
- step=0.018 field=camera_pose_key control=domain_resolver_stress:res://test-domain-resolver-stress.tscn curved=curved_minimal_backdrop:res://test-curved-minimal-backdrop.tscn
- step=0.016 field=camera_pose_key control=domain_resolver_stress:res://test-domain-resolver-stress.tscn curved=curved_minimal_backdrop:res://test-curved-minimal-backdrop.tscn
- step=0.015 field=camera_pose_key control=domain_resolver_stress:res://test-domain-resolver-stress.tscn curved=curved_minimal_backdrop:res://test-curved-minimal-backdrop.tscn
- step=0.014 field=camera_pose_key control=domain_resolver_stress:res://test-domain-resolver-stress.tscn curved=curved_minimal_backdrop:res://test-curved-minimal-backdrop.tscn
- step=0.013 field=camera_pose_key control=domain_resolver_stress:res://test-domain-resolver-stress.tscn curved=curved_minimal_backdrop:res://test-curved-minimal-backdrop.tscn
- step=0.0125 field=camera_pose_key control=domain_resolver_stress:res://test-domain-resolver-stress.tscn curved=curved_minimal_backdrop:res://test-curved-minimal-backdrop.tscn
- step=0.011 field=camera_pose_key control=domain_resolver_stress:res://test-domain-resolver-stress.tscn curved=curved_minimal_backdrop:res://test-curved-minimal-backdrop.tscn
- step=0.010 field=camera_pose_key control=domain_resolver_stress:res://test-domain-resolver-stress.tscn curved=curved_minimal_backdrop:res://test-curved-minimal-backdrop.tscn
- step=0.0075 field=camera_pose_key control=domain_resolver_stress:res://test-domain-resolver-stress.tscn curved=curved_minimal_backdrop:res://test-curved-minimal-backdrop.tscn
- step=0.00625 field=camera_pose_key control=domain_resolver_stress:res://test-domain-resolver-stress.tscn curved=curved_minimal_backdrop:res://test-curved-minimal-backdrop.tscn
- step=0.003125 field=camera_pose_key control=domain_resolver_stress:res://test-domain-resolver-stress.tscn curved=curved_minimal_backdrop:res://test-curved-minimal-backdrop.tscn

## Guardrail

Do not describe visible band/support artifacts as caused by curvature unless comparison metrics support that claim; use 'associated with curved transport fixture under tested settings.'

