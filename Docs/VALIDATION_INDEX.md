# Validation Index

This page is the compact entry point for renderer validation workflows. Use it to move from baseline sanity checks to image-level transport comparisons without hunting across roadmap, reference, and diagnostics pages.

## What The Validation Stack Covers

- **Straight vs curved ladders**: compare straight-line baselines against progressively stronger curved transport so steering, hit recovery, and image deformation remain easy to see.
- **GRIN vs Metric comparisons**: run the same fixture under both transport models to separate "curved, but working" behavior from metric-specific regressions or missing transport fidelity.
- **Screenshot sweeps**: capture repeatable image sets across presets, strengths, or fixture variants so transport changes can be reviewed as a sequence instead of isolated frames.
- **Contact sheets**: condense sweep outputs into one visual grid for quick regression spotting and side-by-side review.
- **Comparison overlays**: inspect aligned output pairs to see where silhouettes, photon-ring structure, shadow edge placement, or other transport features diverge.
- **Diagnostics and capture gating**: correlate image outcomes with runtime counters, hit recovery, sweep/subdivide behavior, and pass-2 gating so a bad frame can be traced back to a concrete failure mode.

## Recommended Workflow

1. Start with a straight or low-curvature ladder to confirm the fixture and baseline image behavior.
2. Run the curved ladder or sweep to check progression across transport strength or fixture variants.
3. Compare GRIN and Metric outputs on the same fixture before interpreting metric-only failures as visual bugs.
4. Use contact sheets and overlays to review image deltas quickly.
5. If captures look wrong, check gating and diagnostics before changing transport assumptions.

## Key Pages

- [Validation](validation.md) - top-level validation modes and verification context.
- [Boundary Layer Fixtures](BoundaryLayerFixtures.md) - deterministic crossing-event fixture family for BoundaryLayerVolume validation.
- [BlackHole Fast Compare](blackhole_fast_compare.md) - quickest GRIN vs Metric comparison path for the black-hole fixture.
- [Black Hole Optical Texture Reference](black_hole_optical_texture_reference.md) - expected black-hole image signatures used as visual validation targets.
- [Metric Transport Next-Gen Roadmap](metric_transport_nextgen_roadmap.md) - current ladder status, sweep observations, and staged metric-validation plan.
- [Metric Null Geodesic Parameter Map](metric_null_geodesic_param_map.md) - current authored-parameter mapping for the metric transport scaffold.
- [Architecture Overview](architecture_overview.md) - renderer structure and subsystem boundaries behind the validation stack.
- [Render Step Gate Hierarchy](RenderStep_GateHierarchy.md) - pass-1/pass-2 gates, soft-gate budgets, sweep behavior, and failure signatures that affect captures.

## Practical Notes

- Treat the **GRIN ladder** as the control path when metric behavior is still incomplete.
- Keep fixture, profile, and capture settings stable across comparison runs.
- Prefer narrow compare runs first; only move to larger sweeps once the baseline pair is readable.
