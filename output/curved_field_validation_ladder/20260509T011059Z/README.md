# How to read this validation ladder

This packet is renderer-validation instrumentation, not a physics proof.

## Visual story

- `curved_vs_control_storyboard.png`: control render, curved render, curved hit normals, ownership seams, unresolved island overlay, and graph lineage.
- Per-cell `diagnostic_quad_panel.png`: rendered frame, hit-normal overlay, camera cross-section minimap, and available transport/field overlay.
- Per-cell `diagnostic_storyboard.png`: local six-panel cockpit for that capture cell.

## Evidence tiers

- Tier A: fixture curvature engaged. The curved fixture log must include nonzero curvature evidence and enabled curved transport.
- Tier B: renderer diagnostics changed. This can include image hash, graph, seam, normal, or visible band/support metric changes.
- Tier C: topology changed across the step ladder. This comes from graph persistence, merge/split, appear/disappear, or seam changes.
- Tier D: unresolved island sealed or persisted. This comes from ReferenceTransportOracle/island outputs.

## Guardrail

Do not describe visible band/support artifacts as caused by curvature unless comparison metrics support that claim; use 'associated with curved transport fixture under tested settings.'

Use 'associated with curved transport fixture under tested settings' unless the comparison metrics justify stronger causal language.

## Current status

- Comparability status: warning
- Control comparison type: scene_control
- Control comparison reason: matched_pose requested, but no safe fixture-level curvature bypass or matched-pose flat variant is available; using configured scene-control, not matched-control
- Curved validation status: warning
