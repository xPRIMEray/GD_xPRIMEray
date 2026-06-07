# Curvature Benchmark

The Curvature Signature Ladder and related contact sheets form the core visual language for demonstrating where and why curved transport diverges from straight-line assumptions.

## The Curvature Signature Ladder

**What it shows**: For a given scene and observer pose, the minimum step length (or precision floor) required for reference integration to converge on a stable diagnostic classification.

- Every node that reaches the finest available floor (e.g. 0.003125) and does not converge is marked as a "risk node" or "unresolved island."
- Bands and clusters are not random; they trace high-curvature boundaries in the GRIN field.

**Primary Exhibits**

- Atomic Orbital GRIN Ladder (multiple resolutions and pruning variants)
  - `output/atomic_orbital_grin_ladder/`
  - Contact sheets and parameter ladders.
- Curved Field Validation Ladder
  - `assets/curved_field_validation_ladder/`
  - Precision vs. convergence plots and heatmaps.
- Transport Coherence Basin
  - `output/transport_coherence_basin_smoke/` and repeatability runs.
  - `visuals/coherence-basin-hero.png` and radial risk plots.
  - Every probed node in the band hits the same precision floor — a topological signature.

**Contact Sheet Gallery**

Use the assets in `assets/curved_field_validation_ladder/` and `assets/observatory/` for:

- Full contact sheets (raw render + risk overlay + ladder).
- 4-mode traversal comparisons (row vs. tile vs. checkerboard) showing how scheduler choice interacts with curvature cost.
- Normal discontinuity and ownership transition maps around islands.

**Falsification Protocol (for every ladder)**

- Run the identical scene at production step length → record risk node count and locations.
- Increase precision floor by 2× or 4× → the bands must not dissolve if the feature is topological.
- Change the IOR gradient or field profile → the spatial signature of the bands must move or disappear in a predictable way.

**Why This Section Exists**

The ladders turn "the field is curved" into a measurable, mappable, falsifiable property. They are the measurable foundation that makes the other exhibits interpretable — curvature has a signal, and the signal can be mapped.

**Navigation**

- Return to [Canonical Fixtures](./canonical_fixtures.md) to see which fixtures generate these ladders.
- Continue to [Closure Diagnostics](./closure_diagnostics.md) to see what happens when the ladder is ignored (budget exhaustion).
