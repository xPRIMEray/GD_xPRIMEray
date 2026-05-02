# Scheduler Decorrelation and Local Coherence

## Summary

The current banding evidence points to an architectural pivot: the primary fix should not be global smoothing. The artifact behaves like an interaction between local first-hit or transport ambiguity and the renderer's row/stride scheduler. When the scheduler aligns many ambiguous pixels into the same traversal phase, local uncertainty can be amplified into full-row horizontal bands.

The practical direction is therefore:

- decorrelate scheduler traversal so ambiguity does not become row-global,
- manage remaining instability locally with neighborhood-aware coherence,
- preserve real geometric discontinuities instead of blurring or smoothing them away.

## Current Evidence

Recent DOE runs show that banding is sensitive to step length, resolver mode, and scheduler stride. The strongest new scheduler signal is that banding changes substantially with pixel stride:

- stride `1` can create broad horizontal resonance across large portions of the film,
- stride `2`, `4`, and `8` can decorrelate or suppress much of that row-global artifact,
- residual sparse artifacts remain after decorrelation, suggesting some local geometric or transport ambiguity is real and still needs targeted handling.

This means the observed instability is not explained by a single monotonic geometry-error model. Smaller step lengths may resolve local geometry more completely, yet still expose or widen row-wise discontinuities when the traversal pattern aligns with ambiguous hit regions.

Position-shifted convergence probes also became nonzero after the probe was changed to re-run shifted first-hit acquisition, but those probe rates did not scale directly with the OFF banding increase. That result supports a mixed mechanism: local ambiguity exists and is measurable, but scheduler structure can dominate how visibly it appears.

## Key Interpretation

The renderer instability is not purely a geometry failure.

Local first-hit or transport ambiguity can exist at boundaries, corners, thin features, or near competing collider regions. On its own, that should normally produce sparse or spatially localized uncertainty. The visible problem appears when row scheduling, pixel stride, or traversal cadence aligns these local uncertainties into coherent horizontal bands.

That makes global smoothing the wrong primary fix. A global blur, temporal smear, or post-process repair would reduce visibility, but it would not separate scheduler resonance from true geometry discontinuity. Worse, it could erase the very edges and domain transitions xPRIMEray is trying to measure.

The better architectural split is:

- use scheduler decorrelation to prevent row-global amplification,
- use local coherence only where instability persists after decorrelation,
- use domain memory sparingly, as a diagnostic and stabilization layer for persistent ambiguous regions rather than as a blanket image-space repair.

## Proposed Architecture

### Scheduler Decorrelation

The scheduler should support traversal modes that break deterministic row-phase resonance while preserving reproducibility under a fixed seed.

Candidate modes:

- randomized row order,
- tiled or block traversal,
- interleaved row groups,
- deterministic blue-noise or shuffled scanline order,
- separate row and column stride experiments.

The purpose is not to hide artifacts with randomness. The purpose is to prevent the scheduler from imposing one global phase over large horizontal regions. If band pixels become localized after decorrelation, that is evidence that the row-global bands were scheduler-amplified.

### Randomized, Tiled, And Block Traversal Experiments

Traversal experiments should be controlled DOE factors, not silent renderer changes. Each should preserve fixed-seed determinism and write enough metadata to identify the traversal pattern used.

Suggested first experiments:

- row-randomized traversal with fixed seed,
- tile/block traversal with stable tile order,
- tile/block traversal with shuffled tile order,
- column stride comparison against row stride,
- mixed row/column interleaving.

### Local Coherence Zones

After scheduler decorrelation, any remaining artifacts should be treated as local instability zones. These zones can be detected from repeated local disagreement rather than from global image appearance.

A local coherence zone may be defined by:

- first-hit collider disagreement in a local window,
- hit-distance discontinuity above a threshold,
- normal discontinuity not explained by a known geometry edge,
- repeated resolver changes across nearby pixels,
- persistent band-mask remnants after scheduler decorrelation.

The zone should be small and measurable. It should not become a whole-frame smoothing pass.

### Neighborhood-Aware First-Hit Continuity

The first-hit resolver should use neighborhood context only as a tie-breaker when local evidence says the hit is ambiguous. It should prefer continuity across nearby stable pixels, but only within plausible distance and normal bounds.

Potential signals:

- neighboring collider/domain agreement,
- local hit-distance continuity,
- compatible normal orientation,
- known boundary/domain confidence,
- persistence across fixed-seed repeated captures.

The resolver should preserve actual object boundaries. A sharp true edge should remain sharp even if neighboring pixels disagree.

### Domain Memory Where Instability Persists

Domain memory should be applied only after decorrelation and local coherence checks show persistent ambiguity. It can help stabilize repeated uncertain zones, but it should not be used as a global temporal smoothing layer.

Recommended constraints:

- activate only in detected local instability zones,
- decay or reset when geometry/camera/step settings change,
- record when memory changes a selected hit,
- keep beauty-path effects auditable,
- export maps showing where memory was used.

## Guardrails

- Do not blur the beauty image.
- Do not hide artifacts with post-process smoothing.
- Do not tune for aesthetics as the primary goal.
- Keep claims grounded in DOE outputs: band pixels, horizontal band score, row coverage, row-mod-stride distributions, changed pixels, and probe maps.
- Preserve real geometric discontinuities, including true collider edges, domain boundaries, and normal breaks.
- Keep fixed-seed runs deterministic so scheduler experiments can be reproduced.
- Separate measurement passes from production behavior whenever extra telemetry work changes timing or budget behavior.

## Next Experiments

1. Randomized row order

   Add a fixed-seed row-shuffle scheduler mode and compare against baseline row order at the same step length and stride. Success would mean row-global horizontal band score drops while local residual artifacts remain inspectable.

2. Tile/block traversal

   Run tile/block traversal with stable and shuffled tile order. Compare whether bands fragment into tile-local artifacts or disappear. This distinguishes scanline resonance from broader frame-budget timing effects.

3. Column stride comparison

   Add a column-oriented stride or traversal comparison. If the artifact rotates or changes structure with column scheduling, that strengthens the scheduler-resonance hypothesis.

4. Local window coherence resolver

   Prototype a local resolver that only acts inside detected ambiguity windows. It should use nearby stable first-hit data as a tie-breaker, not as a blur.

5. Residual artifact mapping after decorrelation

   After scheduler decorrelation, map the remaining artifacts by collider id, hit distance delta, normal delta, row group, and y-mod-stride. Residuals should identify the true geometric or transport ambiguity that still needs local treatment.

## Success Criteria

- Horizontal band score drops under scheduler decorrelation.
- Band pixels become localized instead of row-global.
- Row-band coverage decreases even if a small number of local band pixels remain.
- Band pixels by `y mod stride` become less concentrated in scheduler-aligned residues.
- True geometry edges remain sharp.
- OFF render remains deterministic under a fixed seed.
- Any local coherence resolver reports where it acted and does not alter stable pixels.

## Architectural Direction

The pivot is from "smooth the image until the bands are less visible" to "remove scheduler amplification, then solve the remaining ambiguity locally."

That keeps xPRIMEray honest as a renderer and as a diagnostic tool. The scheduler should not manufacture global structure from local uncertainty, and the coherence system should not erase real structure to make the frame look cleaner.
