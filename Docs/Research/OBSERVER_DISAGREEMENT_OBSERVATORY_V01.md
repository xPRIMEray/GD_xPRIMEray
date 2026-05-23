# Observer Disagreement Observatory V01

## Summary

The Observer Disagreement Observatory is a restrained comparison mode for exposing
measured differences between matched straight-reference and curved-GRIN transport
assumptions.

This is an observability comparison system. It is not a physics-proof system. It
does not assert that one transport model is physically correct. It reports where
two configured renderer runs, held to matched camera and fixture conditions, produce
different measured outputs.

The first implementation should be capture-first and presentation-only: compare
existing classification and telemetry artifacts, then render a quiet disagreement
overlay. Live synchronized split-frame orchestration is feasible later, but should
not be required for the first useful observatory.

## Available Measured Inputs

| Signal | Source | Current availability | Notes |
| --- | --- | --- | --- |
| Transport classification image | `GrinFilmCamera.TryCopyTransportClassificationFilmImageForTesting(...)` | Already available when `FixtureTransportClassificationEnabled=true` | Pixel colors encode fixture transport class after normalization. |
| Transport coverage summary | `TryGetFixtureTransportCoverageForTesting(...)` | Already available | Reports classified coverage, geom/background/portal/throat/budget/escaped buckets. |
| Fixture hit coloring | `FixtureDebugHitColoringEnabled` | Live fixture visualization | Useful for ownership presentation, but not sufficient alone for pairwise delta without capture pairing. |
| Paired straight/curved scenes | `GrinObserveDemoHud.PairedScenePath` | Already available as scene switch workflow | Current runtime switches scenes rather than displaying both simultaneously. |
| Overlay state snapshots | `FilmOverlay2D.GetOverlayRenderSnapshot()` | Already available | Verifies whether comparison grid, rays, traversal overlays, and minimap are active. |
| Domain telemetry maps | `EnableDomainTelemetry`, `TryCopyDomainTelemetryImageForTesting(...)` | Available in telemetry/export workflows | Includes domain id, domain confidence, boundary confidence, selection flip, normal discontinuity. |
| Resolver change summary | `BuildDomainResolverTelemetrySummaryJsonForTesting()` | Available when domain telemetry is active | Compares domain-aware selected hit to first accepted nearest hit; not a straight-vs-curved delta by itself. |
| Oracle comparison artifacts | `ReferenceTransportOracle` outputs | Diagnostic/offline only | May support microscopy overlays, but must not become runtime authority. |

The lowest-risk Phase 3.3 path is therefore artifact comparison: run the matched
straight and curved fixtures, export classification images and coverage summaries,
then compute a disagreement mask from measured pixels.

## 1. Ownership Classification Delta

The first comparison should operate on normalized fixture transport classification
images.

Input pair:

- straight fixture classification image
- curved fixture classification image

Per-pixel delta rule:

- unchanged: straight classification equals curved classification
- changed: straight classification differs from curved classification
- unresolved: either side is unclassified, escaped, budget-exhausted, or missing

The visual output should leave unchanged regions visually quiet. Changed regions
receive a sparse, low-opacity tint or thin contour. Unresolved regions should be
marked separately and quietly; they are missing evidence, not stronger disagreement.

Runtime-feasible first pass:

| Candidate | Feasibility | Implementation path |
| --- | --- | --- |
| Classification delta PNG | Low risk | Compare two `TryCopyTransportClassificationFilmImageForTesting(...)` outputs from matched captures. |
| Changed-pixel count and ratio | Low risk | Count unequal normalized classification pixels. |
| Bucket transition summary | Low risk | Count transitions such as `background_hit -> geom_hit`; report top transitions only. |
| Thin seam contour from delta mask | Low risk | Extract edges of changed mask in a post-capture tool or future `Control` overlay. |
| Live in-engine delta overlay | Medium risk | Requires paired images resident in one scene or a comparison node that can load a prior capture. |

Important limit: classification color equality is a fixture-level comparison. It
shows changed transport class, not necessarily changed collider identity. Collider
identity comparison requires per-pixel collider id records or hit tables from both
runs.

## 2. Transport-Dependent Visibility Redistribution

Visibility redistribution means the observer receives a different terminal result
under the two transport assumptions.

Measured redistribution candidates:

- changed hit class: geometry, background, portal/throat event, escaped, exhausted
- changed collider id when per-pixel hit records are available
- changed domain id when domain telemetry is exported for both runs
- changed boundary confidence when telemetry is exported for both runs
- changed normal discontinuity or selection-flip signal when telemetry is exported
  for both runs

Recommended comparison ladder:

| Level | Dominant question | Required data | Risk |
| --- | --- | --- | --- |
| L1 Classification redistribution | Did the terminal class change? | classification images from straight and curved runs | Low |
| L2 Ownership redistribution | Did the collider/wall owner change? | per-pixel hit table or collider id image from both runs | Medium |
| L3 Domain redistribution | Did domain ownership/confidence change? | domain telemetry maps from both runs | Medium |
| L4 Oracle-stabilized redistribution | Did production and oracle disagree differently across assumptions? | oracle comparison artifacts for both runs | Future/oracle-dependent |

The observatory should not call L1 a topology map. L1 is a classification delta.
Topology language becomes appropriate only when connected regions, seams, and
ownership boundaries are derived from measured owner/domain data.

## 3. Disagreement Overlay Language

The visual language should be sparse and contour-oriented.

Allowed primitives:

- thin seam contours around changed classification regions
- low-opacity tint only inside changed regions
- distinct quiet mark for unresolved or missing evidence
- confidence-weighted opacity when confidence data exists
- small summary readout: changed pixels, changed ratio, unresolved count, top
  transition buckets

Avoid:

- glow effects
- turbulence
- decorative particles
- equal-weight overlays
- synthetic field energy
- animated disagreement that is not driven by measured state changes

Hierarchy rule:

| Mode | Dominant comparison concept | Secondary support |
| --- | --- | --- |
| Classification Delta | changed terminal class | thin contours, changed ratio |
| Ownership Redistribution | changed wall/collider owner | basin tint, seam contour |
| Boundary Confidence Delta | changed confidence | sparse scalar overlay, no class tint unless selected |
| Continuity Delta | changed normal/selection signals | localized risk overlay only inside measured disagreement |
| Oracle Microscopy | production/reference closure | oracle diff, precision notes |

Only one comparison concept should dominate the frame at a time. The dashboard should
not show classification delta, domain confidence delta, oracle diff, and continuity
vectors at equal visual weight.

## 4. Runtime Pairing Architecture

### Current Architecture

The current observe workflow has matched straight/curved scene pairing through
`GrinObserveDemoHud.PairedScenePath`. F2 can switch to the paired scene, and F6 can
toggle comparison grid/crosshair. This is useful for operator comparison, but it
does not keep both render results resident in one runtime surface.

### Low-Risk Architecture: Capture-First Comparator

Use the existing capture pipeline:

1. Render straight fixture with classification enabled.
2. Export classification image, coverage summary, and optional domain telemetry.
3. Render curved fixture with the same camera, resolution, traversal mode, and
   classification/telemetry flags.
4. Compare artifacts offline or in a lightweight post-capture tool.
5. Produce a disagreement overlay and summary.

This path gives Phase 3.3 useful output without adding a live dual-render pipeline.

### Medium-Risk Architecture: Paired Observatory Surfaces

Introduce a presentation node that owns two surfaces:

- straight reference surface
- curved GRIN surface

Required constraints:

- synchronized camera transform
- matched resolution and capture settings
- explicit transport mode label per surface
- no cross-feeding of transport data
- comparison overlay derives only from completed frame outputs

This is presentation orchestration, not solver integration. Each surface should
render independently under its own configured transport assumption.

### Future Architecture: Live Delta Surface

A live delta surface can compute disagreement after both surfaces produce measured
frame outputs. It should compare completed buffers only. It must not alter traversal
order, early-exit conditions, hit selection, field sampling, or scheduler behavior.

## 5. Runtime-Feasible Comparisons

| Comparison | Status | Notes |
| --- | --- | --- |
| Straight/curved scene switching | Already available | `PairedScenePath` provides matched-scene navigation. |
| Classification image capture | Already available | Requires `FixtureTransportClassificationEnabled`. |
| Classification coverage summary | Already available | Good for hermetic sanity and unresolved/budget checks. |
| Overlay state verification | Already available | `FilmOverlay2D` snapshots verify visual state. |
| Domain telemetry export | Already available in diagnostic workflows | Useful for confidence and continuity comparisons, but not a live overlay yet. |
| Classification delta mask | Low-risk addition | Compare two captured normalized classification images. |
| Transition bucket summary | Low-risk addition | Summarize changed class pairs. |
| Seam contour from delta mask | Low-risk addition | Derived from measured delta mask; no invented topology. |
| Live split-frame orchestration | Medium-risk addition | Requires two synchronized surfaces or cached paired frames. |
| Live ownership/collider delta | Medium-risk addition | Requires paired per-pixel collider id access in one comparison context. |
| Confidence-weighted live overlay | Medium-risk addition | Requires paired domain telemetry buffers or captured maps. |
| Oracle-stabilized disagreement | Future/oracle-dependent | Diagnostic-only; useful for microscopy, not runtime authority. |

## 6. Low-Risk Implementation Path

1. Add a capture artifact comparator for two normalized classification images.
2. Require explicit matched metadata before comparing:
   - width and height
   - fixture identity or accepted pair id
   - camera pose key
   - traversal mode
   - scheduler mode
   - transport labels: straight reference and curved GRIN
3. Emit:
   - `classification_delta.png`
   - `classification_delta_summary.json`
   - optional `classification_delta_contours.png`
4. Keep unchanged pixels transparent or very low contrast.
5. Render unresolved pixels with a separate neutral mark.
6. Add the output as a panel candidate for the existing observatory/capture docs.

This path should precede live split-frame work. It proves the disagreement language
against real measured data before adding runtime orchestration complexity.

## 7. Future / Oracle-Dependent Items

These should remain out of Phase 3.3 runtime authority:

- oracle-stabilized visibility topology
- production-versus-oracle disagreement overlays
- convergence ladder disagreement stage
- per-pixel precision closure disagreement
- topology claims based on connected components unless owner/domain data supports
  them
- confidence-weighted comparison when confidence exists for only one side

Oracle data can annotate a diagnostic panel. It must not decide which runtime result
is displayed as correct.

## Guardrails Against Overclaiming

- Say "classification changed," not "space changed."
- Say "visibility redistributed under this transport assumption," not "true
  topology emerged," unless measured owner/domain topology supports that claim.
- Leave unchanged regions quiet.
- Mark unresolved/missing evidence separately from disagreement.
- Do not infer confidence where no confidence channel exists.
- Do not use oracle output as runtime authority.
- Do not let split-frame composition imply proof by symmetry.
- Do not introduce glow, turbulence, particles, or synthetic field energy.
- Do not modify transport semantics, scheduler order, hit selection, or resolver
  decisions.

## Validation Criteria

- No transport semantic changes.
- No scheduler modifications.
- No oracle authority escalation.
- Comparison is presentation-only instrumentation.
- Classification deltas are computed only from measured straight and curved outputs.
- Unchanged pixels remain visually quiet.
- Dashboard hierarchy exposes one dominant comparison concept at a time.

The observatory succeeds when a viewer can see, without elaborate explanation, where
matched straight-reference and curved-GRIN assumptions produce different measured
renderer outcomes.
