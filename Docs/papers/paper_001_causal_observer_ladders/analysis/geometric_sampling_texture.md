# Geometric Sampling Texture and Orientation Persistence

Exploratory geometric morphology analysis only; no physical interpretation is asserted. This note synthesizes the existing adaptive tiling, polar tiling, edge-alignment, geometric structure, and log-polar orientation analyses for the approved observer ladder artifacts.

## Executive Summary

The latest artifact-only analyses support a hybrid view of sampling texture. Adaptive square tile boundaries are the better directional match to visible-band edges, while polar/radial boundaries improve near-edge recall but lose edge-direction fidelity. The bridge checkpoint, `post_throat_backstep_01`, remains morphologically distinct by Hough line count, lower contour eccentricity, and visible-band connected-component count. However, log-polar orientation histograms do not show a global bridge-only breakdown: near-side, throat, and bridge remain radial-dominant, while the far-side checkpoints shift toward tangential dominance.

This argues for a hybrid sampling texture architecture: raw row pass remains scout truth, adaptive square tiles carry local coherence and direction, polar/radial tiles serve high-recall aperture diagnostics, and future diagonal, triangular, and annular textures can be layered as separate diagnostic views. Coherence previews should remain separate from raw validation truth.

## Inputs

- Adaptive tile summaries and overlays from `output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-24T01-44-23_first_hit_baseline_current/`.
- Edge-alignment summary from `edge_alignment_summary.json`.
- Polar tiling and polar edge comparison summaries from `polar_edge_comparison_summary.json` and `*_polar_tile_summary.json`.
- Geometric morphology summary from `geometry_structure_search_2026-04-25/geometry_structure_summary.json`.
- Log-polar orientation summary from `log_polar_orientation_2026-04-25/log_polar_orientation_summary.json`.

## Sampling Texture Findings

Adaptive square tiles are the strongest directional boundary match among the available diagnostic overlays. For `mouth`, the adaptive boundary overlay reached visible-edge recall `0.712` and gradient-direction similarity `0.836`; for `post_throat_backstep_01`, it reached recall `0.731` and direction similarity `0.875`. Polar boundary overlays reached near-perfect visible-edge recall (`1.000` and `0.9997`) but lower direction similarity (`0.628` and `0.642`). This means polar/radial tiles are useful for high-recall aperture diagnostics, while adaptive square tiles better preserve local edge orientation.

The bridge/backstep checkpoint is morphologically distinct in the geometric structure search. It has `214` Hough line detections, versus `71-106` for most other checkpoints and `86` at the mouth. Its mean contour eccentricity drops to `0.663`, versus roughly `0.909-0.962` for most non-bridge checkpoints. Its visible-band mask splits into `190` connected components, compared with `91` for the mouth. These are morphology signals only, but they agree with the independent transport analyses that isolate the bridge as a distinct regime.

The log-polar orientation analysis does not show a global loss of radial/tangential orientation persistence at the bridge. The bridge-to-near-side mean histogram cosine is `0.988`, and bridge-to-rest cosine is `0.972`, compared with a non-bridge pairwise mean of `0.961`. The transition appears instead as a regime shift after the bridge: near-side, throat, and bridge are radial-dominant, while the far-side checkpoints become tangential-dominant.

## Comparison Table

Edge metrics are shown where they exist in current artifacts. `visible edge px` is the raw visible-band edge reference used by the edge-alignment comparisons. Adaptive and polar entries are `visible-edge recall / gradient-direction similarity` within a 6 px matching radius. `na` means that the current artifact set does not include that checkpoint for that diagnostic pass.

| checkpoint | visible edge px | adaptive edge recall/dir | polar edge recall/dir | radial frac | tangential frac | radial/tangential | Hough lines | visible-band components |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| `mouth` | 53578 | 0.712 / 0.836 | 1.000 / 0.628 | 0.312 | 0.202 | 1.543 | 86 | 91 |
| `mouth_to_throat_approach` | na | na | na | 0.301 | 0.195 | 1.541 | 85 | na |
| `throat` | na | na | na | 0.335 | 0.187 | 1.796 | 71 | na |
| `post_throat_backstep_01` | 48946 | 0.731 / 0.875 | 1.000 / 0.642 | 0.336 | 0.166 | 2.029 | 214 | 190 |
| `post_throat_exit_approach` | na | na | na | 0.194 | 0.257 | 0.754 | 90 | na |
| `exit_lookback` | na | na | na | 0.177 | 0.305 | 0.581 | 106 | na |

## Interpretation

The sampling evidence separates into two useful signals:

- Boundary-finding signal: polar/radial tiling gives high recall near aperture edges and is therefore useful for diagnostics that should not miss circular or annular boundary structure.
- Direction-preserving signal: adaptive square tiling gives stronger local gradient-direction agreement and is therefore better for coherence previews that need to preserve local edge direction.

The bridge is not a simple orientation collapse. It is radial-dominant like the near-side and throat checkpoints, but its edge field is more line-rich and more fragmented in the visible-band mask. The far side then shifts toward tangential dominance, with radial/tangential ratios below `1.0` for `post_throat_exit_approach` and `exit_lookback`.

## Hybrid Sampling Texture Architecture

Proposed architecture:

1. Keep the raw row pass as scout truth. It remains the validation reference and should not be replaced by coherence previews.
2. Use adaptive square tiles for local coherence and directional boundary matching. These are the best current diagnostic texture for direction-preserving local structure.
3. Use polar/radial tiles for high-recall aperture diagnostics. They are appropriate for circular, radial, and annular features, especially when missing an aperture edge is worse than over-covering it.
4. Add future triangle, diagonal, and annular textures as diagnostic layers. Triangle/diagonal tiling can probe oblique edge families; annular tiling can probe ring persistence and radial band spacing.
5. Keep coherence previews separate from raw validation truth. Coherence maps should explain and guide sampling, but raw classifications and fresh-instance coverage remain the authoritative validation layer.

## Figure References

All figures below are in `output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/` (not versioned in the docs site).

- `00_mouth_adaptive_tile_overlay.png`, `01_post_throat_backstep_01_adaptive_tile_overlay.png` — adaptive tile overlay per checkpoint
- `00_mouth_adaptive_tile_heatmap.png`, `01_post_throat_backstep_01_adaptive_tile_heatmap.png` — adaptive tile heatmap per checkpoint
- `edge_alignment_contact_sheet.png` — edge alignment contact sheet
- `00_mouth_polar_boundary_overlay.png`, `01_post_throat_backstep_01_polar_boundary_overlay.png` — polar edge comparison overlays
- `orientation_histograms.png` — log-polar orientation histogram
- `log_polar_edge_contact_sheet.png` — log-polar edge contact sheet
- `annotated_shape_search_contact_sheet.png` — geometry structure contact sheet

## Short Verdict

The bridge disrupts morphology more than orientation persistence. It is line-rich and component-fragmented, but it remains radial-dominant and strongly persistent with the near-side/throat orientation family. The clearer orientation transition occurs after the bridge, where far-side checkpoints become tangential-dominant.
