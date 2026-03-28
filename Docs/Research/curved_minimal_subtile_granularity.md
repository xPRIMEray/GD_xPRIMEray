# Curved Minimal Subtile Granularity

## Scope

Phase 1.5 characterizes the existing tile-metrics scaffold on the `curved_minimal` validation path without changing scheduler behavior.

Runs used:

- `--tile-metrics=1`
- subtile widths: `64`, `32`, `16`, `8`, `4`
- same curved-minimal render-test path already used for validation

Reference artifacts:

- batch summary: `/tmp/curved_minimal_granularity_8cmn/summary.json`
- logs:
  - `/tmp/curved_minimal_granularity_8cmn/w64.log`
  - `/tmp/curved_minimal_granularity_8cmn/w32.log`
  - `/tmp/curved_minimal_granularity_8cmn/w16.log`
  - `/tmp/curved_minimal_granularity_8cmn/w8.log`
  - `/tmp/curved_minimal_granularity_8cmn/w4.log`

The scaled film width on this path is effectively `80px`, so widths below `4` would likely over-fragment without adding meaningful spatial signal.

## Summary Table

| Width | Unique Subtiles | Active | Empty | Hit Concentration | Mean Spread | Max Spread | Active-Band Top Stability |
| --- | ---: | ---: | ---: | --- | ---: | ---: | --- |
| 64 | 2 | 1 | 1 | one subtile owns `100%` of hits | `0.0392` | `0.1880` | `100%` same subtile |
| 32 | 3 | 1 | 2 | one subtile owns `100%` of hits | `0.0783` | `0.3750` | `100%` same subtile |
| 16 | 5 | 1 | 4 | one subtile owns `100%` of hits | `0.1566` | `0.7500` | `100%` same subtile |
| 8 | 10 | 2 | 8 | hits split `53.3% / 46.7%` across two neighbors | `0.1671` | `0.7500` | `66.6% / 33.4%` between two neighbors |
| 4 | 20 | 4 | 16 | hits split `40.0% / 36.7% / 13.3% / 10.1%` across four neighbors | `0.2504` | `1.0000` | `66.6% / 33.4%` between two neighbors |

## Interpretation

### Active vs Empty Subtiles

- Widths `64`, `32`, and `16` are too coarse for scheduler experiments on this scene. They collapse the active region into a single subtile, so prioritization would have little to choose between.
- Width `8` is the first setting that exposes more than one active subtile while still keeping the active region compact.
- Width `4` reveals more structure, but most subtiles remain empty and the active region becomes noticeably more fragmented.

### Hit Concentration

- Coarse widths hide internal structure by assigning all hits to one container.
- At width `8`, the scene resolves into two adjacent active subtiles around `x=32` and `x=40`, with a near-even split in hit share.
- At width `4`, that same region breaks into four active subtiles, but the outer two carry much less signal than the inner pair.

### Mean / Max Yield Spread

- Yield spread rises as width narrows, which is expected and desirable up to a point.
- Width `8` increases spatial contrast relative to `16` while still preserving a compact active set.
- Width `4` pushes max spread to `1.0`, which is strong discrimination but also a sign of sparse, highly localized occupancy.

### Top-Yield Stability

Important note:

- counting the top subtile across all bands is misleading here because many bands are zero-hit and therefore tie at `0`
- the useful signal is top-subtile stability across active bands only

Using active bands only:

- widths `64`, `32`, and `16` are trivially stable because only one subtile ever carries hits
- width `8` remains stable enough for prioritization: the best subtile stays within the same two-neighbor region, with a `66.6% / 33.4%` split
- width `4` keeps the same core region, but becomes more spike-prone and fragmented

## Recommendation

Recommended initial subtile width for first scheduler experiments: `8`

Why:

- it is the first width that exposes non-trivial horizontal structure
- it keeps the active region narrow and interpretable
- it avoids the over-fragmentation and one-pixel-like spikes seen at width `4`
- it should give a scheduler enough spatial choice to test prioritization without making bookkeeping dominate the experiment

## Safest First Prioritization Experiment

Recommended first experiment: `reorder-only`

Why:

- it isolates the effect of tile ordering from the effect of reduced work
- it preserves the current budget envelope, which is safer for output-stability comparisons
- it makes validation easier to interpret because any change in results comes from traversal order, not fewer rays or fewer candidate checks

`reorder plus budget reduction` should be deferred until reorder-only shows:

- stable top-subtile preference over time
- no visible instability or regression in the curved-minimal validation path
- a clear work-to-hit advantage worth converting into an actual budget cut
