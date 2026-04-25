# Phase Sampling Domain Update

Exploratory sampling-domain analysis only. No renderer changes, hit-selection changes, or simulation reruns were performed. Raw validation truth remains separate from coherence previews.

## Executive Summary

The 2026-04-25 phase/coherence probes support a consistent sampling-domain picture: the visible bands align with real field-geometry structure, but no single global polar center explains them.

Phase-guided hit-selection proxies slightly improve local phase scores but do not strongly reduce band-boundary alignment. Incoherence-centered polar tiling slightly improves bridge/backstep direction fidelity relative to aperture-centered polar tiling. Curvature-center polar tiling improves direction similarity over aperture-centered polar on both tested checkpoints, but loses distance alignment and remains well below adaptive-square directional fidelity. Adaptive-square tiles remain the best local direction-preserving texture.

## Inputs

- `phase_guided_first_hit_selection_2026-04-25/band_reduction_metrics.json`
- `incoherence_centered_polar_2026-04-25/incoherence_polar_summary.json`
- `curvature_center_polar_2026-04-25/curvature_center_summary.json`
- `polar_edge_comparison_summary.json`
- `edge_alignment_summary.json`

All inputs are existing artifacts under:

`output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-24T01-44-23_first_hit_baseline_current/`

## Result Summary

The phase-guided first-hit proxy used existing accepted-hit diagnostics rather than true renderer candidate lists. It reduced mean local selection score by `0.0401` on average and reduced visible-band/incoherence correlation by `-0.0325`, but the visible-band phase-boundary fraction changed only `-0.0070`. This is useful as a tie-breaker signal, not a standalone band-reduction fix.

Incoherence-centered polar tiling used a centroid estimated from visible-band mask, neighbor-normal-delta, and phase-incoherence signals. It retained the high recall behavior of aperture-centered polar tiling and slightly improved bridge/backstep direction similarity (`0.656` vs `0.642`), but did not approach adaptive-square direction fidelity.

Curvature-center polar tiling selected centers from Hough circle/arc and contour-fit candidates. It improved direction similarity relative to aperture-centered polar on both checkpoints (`mouth`: `0.656` vs `0.628`; bridge: `0.660` vs `0.642`) but reduced symmetric edge-distance score relative to aperture/incoherence polar and adaptive-square baselines.

## Direction Fidelity Comparison

| method | mouth direction | post-throat direction | key verdict |
|---|---:|---:|---|
| aperture polar | 0.628 | 0.642 | High recall baseline, weak direction fidelity |
| incoherence polar | 0.624 | 0.656 | Slight bridge direction gain, not global |
| curvature-center polar | 0.656 | 0.660 | Best polar direction result, weaker distance alignment |
| adaptive square | 0.836 | 0.875 | Best local direction-preserving texture |

## Current Architecture Doctrine

- Raw row pass = scout truth.
- Adaptive-square tiles = best local direction-preserving texture.
- Polar, incoherence-centered polar, and curvature-center polar = high-recall geometric probes.
- Future smart mode should support hybrid and multi-center sampling textures.
- Validation truth must remain separate from coherence previews.

## Verdict

The field geometry is real in the sampling domain, but the band structure is not explained by a single global polar center. Polar recentering can improve geometric probe alignment slightly, especially for bridge/backstep direction fidelity, but adaptive-square tiles remain the strongest directional match. The next useful sampling texture is likely hybrid: adaptive-square for local direction, plus multi-center annular/curvature probes for high-recall geometry diagnostics.

## Artifact References

- Phase-guided first-hit proxy: `phase_guided_first_hit_selection_2026-04-25/band_reduction_metrics.json`
- Incoherence-centered polar: `incoherence_centered_polar_2026-04-25/incoherence_polar_summary.json`
- Curvature-center polar: `curvature_center_polar_2026-04-25/curvature_center_summary.json`
- Existing aperture/adaptive comparison: `polar_edge_comparison_summary.json`
