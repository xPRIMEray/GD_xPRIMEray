# Shared Related-Work and Bibliography Note

This note provides a compact common frame for the wormhole paper family so that Papers 000–003 can later be merged into a single manuscript without rewriting terminology, notation, or reference cues from scratch.

## Scope

The trilogy sits at the intersection of four research traditions:

- geometric optics and caustic structure
- general-relativistic lensing and wormhole-style transport intuition
- deterministic rendering validation
- adaptive computation guided by measured structure rather than by global heuristics alone

The present papers do not claim a new analytic solution to wormhole lensing. They contribute a deterministic validation language for a rendering system in which curved transport, remap, and observer-facing image formation are treated as one measured process.

## Related-Work Buckets

### 1. Geometric Optics and Caustics

Relevant background here includes the classical study of ray congruences, focal structure, and caustic formation in curved settings. For this family, the important inheritance is methodological: optical structure should be read as a consequence of geometry rather than as a secondary rendering artifact.

Use this bucket when discussing:

- annular concentration
- radial transition structure
- congruence behavior
- observer-visible focusing

### 2. General Relativity, Lensing, and Wormhole Intuition

This bucket supplies the physical imagination for transport across nontrivial geometry and topology. In the present work, wormhole transport is implemented in a rendering harness rather than in a full spacetime field solution, but the conceptual pressure comes from the same source: ray behavior may change qualitatively when curvature and remap are part of the transport law.

Use this bucket when discussing:

- curved transport
- topological remap
- observer-dependent image structure
- wormhole optical path families

### 3. Deterministic Validation in Rendering

Standard rendering validation often emphasizes stochastic convergence, global timing, or pixel agreement. The present trilogy instead relies on deterministic harness conditions, explicit contracts, and repeated fixed-view measurements. This should be framed as a validation choice rather than as a claim against stochastic rendering in general.

Use this bucket when discussing:

- fixed camera and input lock
- repeatable capture
- explicit pass/fail contracts
- regression gates grounded in optical structure

### 4. Structure-Aware Adaptive Computation

The trilogy does not advocate arbitrary heuristics. Its adaptive logic is constrained by measured geometric structure: preserve the annulus, bound low-value sectors, and reject operating points that weaken the observed stable regime. This places the work closer to structure-aware allocation than to simple cost pruning.

Use this bucket when discussing:

- low-value sector budgets
- bounded suppression
- coupled invariants
- stable operating regions

## Shared Terminology

Use the following terms consistently across the trilogy:

- `proto-caustic invariant`
  - the positive contract preserving the destination-side annulus
- `low-value sector budget`
  - the negative contract bounding query share in a portal-local low-yield region
- `coupled invariant system`
  - the two contracts considered simultaneously
- `stable operating region`
  - a regime in which both contracts hold without hit/write drift or degraded timing behavior
- `deterministic harness`
  - the fixed-view, fixed-input wormhole validation and measurement run
- `portal-local sectors`
  - bins indexed by `layer`, `radial_bin`, and `theta_bin`

Preferred phrasing:

- say `preserve optical structure`, not `keep the image looking right`
- say `bound low-yield expenditure`, not `kill waste rays`
- say `selected by constraints`, not `found by tuning`
- say `observer-facing structure`, not `perceptual magic`

## Shared Notation

Use the following notation consistently:

- `I1`
  - proto-caustic invariant
- `I2`
  - low-value sector budget
- `layer`
  - portal-local shell or side index used in sector aggregation
- `radial_bin`
  - portal-local radial band
- `theta_bin`
  - portal-local angular sector
- `actual_query_share`
  - measured query-share fraction for the designated low-value family
- `maximum_allowed_query_share`
  - contract threshold derived from deterministic baseline

When possible, keep equations minimal and explicit:

- `I1 = pass` if annular density, continuity, and radial-gradient thresholds all hold
- `I2 = pass` if `actual_query_share <= maximum_allowed_query_share`

## Citation Strategy for a Future Unified Manuscript

When this trilogy is merged into a longer arXiv-style note, the reference structure should likely be:

1. geometric optics / caustic background
2. lensing and wormhole optical context
3. rendering validation and deterministic measurement context
4. adaptive or structure-aware computation context

This note is intentionally not a full bibliography. It is a bridge document so later citation work can be added once the final manuscript scope is chosen.
