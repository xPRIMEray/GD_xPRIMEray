# ReferenceTransportOracle ROI Sweep Summary - 2026-05-05

This note summarizes the latest `ReferenceTransportOracle` ROI sweep:

`output/reference_transport_oracle_roi_sweep/20260505T034858Z/cells/row_stride_1/`

The oracle is treated only as a best-known renderer-reference transport path. It is not physical truth, not GR validation, and it does not feed rendering, hit selection, shading, resolver scoring, traversal, scheduling, or adaptive precision.

## Artifact Packet

Copied strongest artifacts:

- `Docs/assets/cathedral_probe/oracle/reference_transport_oracle_report_20260505T034858Z.md`
- `Docs/assets/cathedral_probe/oracle/epsilon_stability_map_20260505T034858Z.png`
- `Docs/assets/cathedral_probe/oracle/convergence_ladder_contact_sheet_20260505T034858Z.png`
- `Docs/assets/cathedral_probe/oracle/precision_cost_curves_20260505T034858Z.png`
- `Docs/assets/cathedral_probe/oracle/production_vs_oracle_diff_20260505T034858Z.png`

## Key Results

- Samples: 64
- Oracle runs: 128
- Production-vs-oracle comparisons: 320
- Trajectory-family rows: 576
- Oracle replay failures: 0
- Stable comparisons: 266
- Unresolved comparisons: 54
- Threshold-snap comparisons: 0
- Multi-solution comparisons: 0
- Mean decision risk: 0.000570
- Max decision risk: 0.002268

The important result is that every sampled pixel had at least one stable production step. That supports the existence of epsilon-stable renderer transport regions under the current oracle semantics.

## Step-Length Stabilization

Coarsest stable production step by sample:

| First stable step | Sample count |
|---:|---:|
| 0.02 | 35 |
| 0.015 | 23 |
| 0.0125 | 5 |
| 0.00625 | 1 |
| Never stable | 0 |

Per-step comparison outcome:

| Production step | Stable | Unresolved |
|---:|---:|---:|
| 0.02 | 35 | 29 |
| 0.015 | 46 | 18 |
| 0.0125 | 57 | 7 |
| 0.00625 | 64 | 0 |
| 0.003125 | 64 | 0 |

This is the clearest convergence ladder so far: coarse steps already work in much of the ROI, the unresolved set shrinks monotonically as step length decreases, and the sampled region is fully stable by `0.00625`.

## Unresolved Cluster

The 54 unresolved comparisons collapse to 41 unique pixels. They are tightly localized:

- Bounding box: `x=36..44`, `y=31..37`
- Centroid: approximately `(39.98, 34.06)`
- Location: upper-left manual corner ROI neighborhood

Unresolved by step:

| Production step | Unresolved count | Pixel bbox |
|---:|---:|---|
| 0.02 | 29 | `36,31 -> 44,37` |
| 0.015 | 18 | `36,31 -> 44,37` |
| 0.0125 | 7 | `37,32 -> 44,37` |
| 0.00625 | 0 | none |
| 0.003125 | 0 | none |

The microscope target is no longer the whole frame. It is this small unresolved upper-left corner patch.

## Continuity And Ownership Alignment

Using the latest available tile-commit traversal artifacts from `output/tile_commit_traversal_comparison/20260504T010110Z`, the unresolved oracle pixels did not overlap existing exported continuity-vector or transport-shape-region support:

- Full-frame row `0.015` continuity vectors near unresolved bbox: 0
- Corner-probe row `0.015` continuity vectors near unresolved bbox: 0
- Transport shape regions touching unresolved bbox: 0

This should be interpreted cautiously. It does not prove the unresolved patch is not an ownership boundary. It means the currently exported continuity/shape maps are not aimed at this small oracle-discovered region. The oracle has identified a new microscope target that prior overlays did not localize.

## Interpretation

The sweep supports epsilon-stable transport regions in the renderer-validation sense:

- Oracle replay was deterministic for this packet.
- All 64 sampled pixels eventually stabilized against the oracle reference.
- Stability improves monotonically with smaller production step length.
- The remaining instability concentrates into a compact corner patch instead of spreading across the ROI.

This changes the workflow:

1. Archive stable regions.
2. Stop spending DOE time on already stable samples.
3. Promote unresolved samples to the next microscope pass.

The next target is only the unresolved 54 comparisons / 41 unique pixels.

## Recommended Next ROI Refinement

Run a focused oracle refinement on the unresolved bbox:

- Center: `(40,34)`
- ROI bbox: `36,31 -> 44,37`
- Suggested manual ROI seed: `40,34`
- Patch: `17x17` first, then `33x33` only if the unstable edge extends outside the patch
- Production steps: `0.02,0.018,0.016,0.015,0.014,0.013,0.0125,0.011,0.010,0.0075,0.00625,0.003125`
- Oracle step: keep `0.0015625`
- Add matching local continuity-vector and ownership-shape export for the same patch

Primary metrics:

- first stable step per pixel
- oracle replay match
- local decision-risk gradient
- ownership/collider transition map
- path-length delta map
- normal-angle delta map
- local continuity vector density inside `x=32..48`, `y=27..43`

Success criterion:

The unresolved island should either seal by `0.00625`, as this sweep suggests, or reveal a smaller persistent subregion that needs an even finer oracle/reference pass.

## What Not To Claim

- Do not claim physical truth.
- Do not claim real null-geodesic validation.
- Do not claim `0.0015625` is absolute truth.
- Do not claim the absence of continuity-vector overlap means no ownership boundary exists.
- Do not feed oracle results back into rendering yet.

The disciplined claim is narrower and useful: under the current best-known renderer-reference oracle, this ROI contains mostly epsilon-stable transport behavior, with a small unresolved corner patch that becomes the next microscope target.
