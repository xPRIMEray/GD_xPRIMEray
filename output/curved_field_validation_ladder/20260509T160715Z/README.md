# How to read this validation ladder

This packet is renderer-validation instrumentation, not a physics proof.

## Visual story

- `curved_vs_control_storyboard.png`: representative six-panel story using the sequence geometry -> transport -> topology -> quality/budget.
- `topology_evolution_strip.png`, `transport_phase_evolution_strip.png`, and `budget_evolution_strip.png`: step-ladder evolution strips built from real rendered diagnostic artifacts.
- `ownership_graph_evolution.gif`, `budget_heatmap_evolution.gif`, and `diagnostic_storyboard_evolution.gif`: animated observability outputs using real step frames only; no fake interpolation.
- Per-cell `diagnostic_storyboard.png`: rendered frame, Cartesian wireframe, hit normals/vectors, ownership seams, budget/islands, and lineage/phase.
- Per-cell `diagnostic_quad_panel.png`: rendered frame, hit-normal overlay, camera cross-section minimap, and available transport/field overlay.

## Visual hierarchy principles

1. Establish Cartesian geometry projection first. This gives the observer a stable coordinate-space anchor.
2. Add hit normals and transport vectors second, so transport behavior is read against the geometry anchor.
3. Reveal ownership topology and seams third.
4. Diagnose budget saturation and unresolved islands fourth.
5. Track graph lineage and phase evolution last.

## Representative frame selection

- Prefer cells with visible geometry, visible hits, coherent transport structure, informative topology, and non-empty overlays.
- Avoid low-hit cells, visually empty cells, mostly budget-exhausted cells, and unresolved samples with no observable geometry.
- Selection details are written to `storyboard_selection.json`.
- Temporal role selection is written to `temporal_observability_summary.json` and scores best temporal coherence, strongest visible evolution, and explanatory value.

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
- Control comparison reason: configured scene-control fixture
- Curved validation status: warning
