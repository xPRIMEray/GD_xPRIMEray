# Orbital Transport Observatory Runtime Interpretation V01

## Reading Constraint

This document interprets `ORBITAL_TRANSPORT_OBSERVATORY_SELECTED_V01.png` as a
runtime observability target for xPRIMEray.

It is not a recreation prompt. It is not a directive to reproduce the frame pixel
for pixel, add cinematic effects, or invent physics visuals. The value of the frame
is its restraint: it presents measured computational structure as an instrument
surface.

Runtime-observable means derived from renderer state, overlay state, fixture
classification, scheduler or traversal telemetry, domain telemetry, or diagnostic
oracle outputs already present in the project. The target is to expose what the
renderer did, not to dramatize what the field might be doing.

## Emotional Legibility Constraint

The observatory visualization system exists to make invisible computational
structure intuitively legible.

A viewer should be able to understand, within a few seconds of viewing:

- where computation is occurring
- what transport mode is active
- how transport assumptions differ
- what regions remain unresolved or pending
- how observability changes spatially

This applies to non-experts as an engineering constraint, not as presentation
polish. If a visualization requires extensive textual explanation before its core
distinction is perceptible, the overlay density or presentation complexity is too
high.

The preferred posture is geometric clarity: one visible structure, one operational
question, one readable distinction.

## Visual Hierarchy Rule

At any moment, one observability concept must dominate the frame.

Valid dominant concepts include:

- traversal emergence
- ownership redistribution
- dual-reality divergence
- oracle closure
- transport continuity

Secondary overlays must support the dominant concept rather than compete equally
for attention. A dashboard that gives every panel the same visual weight flattens
the evidence hierarchy and reduces observability clarity.

The frame may contain multiple instruments, but it should not contain multiple
simultaneous focal claims.

## 1. Transport Divergence Language

The selected frame establishes a left/right disagreement between straight reference
transport and curved GRIN transport. The important runtime behavior is not the
symmetry of the layout. It is the asymmetry of the evidence.

The straight side reads as reference geometry: rays remain rectilinear, ownership
regions are closer to direct projection, and the visual grammar should stay stable
and sparse. The curved side reads as field-dependent transport: ray paths bend,
ownership basins redistribute, and local visibility topology changes.

Runtime interpretation:

| Observable behavior | Runtime source | Feasibility |
| --- | --- | --- |
| Straight versus curved transport paths | `FilmOverlay2D` ray polylines, `RayBeamRenderer` debug rays, straight/GRIN fixture captures | Partially possible live; already possible in captured comparison workflows |
| Hit-only transport visibility | `RayBeamRenderer.DebugDrawOnlyHits`, fixture hit data | Already implemented |
| Observer disagreement between transport assumptions | paired straight/curved captures with matched camera and scene setup | Low-risk dashboard addition |
| Classification difference between assumptions | `FixtureTransportClassificationEnabled`, fixture hit coloring, transport coverage outputs | Partially possible; needs paired presentation layer |
| Live synchronized dual-reality runtime | two transport solves with synchronized camera and panel composition | Medium-risk addition |

The curved side should not be made "more active" through decorative motion. Its
difference must come from measured path curvature, changed hit ownership, changed
classification, altered path length, boundary confidence, or oracle comparison.

Curved versus straight is therefore a transport grammar, not an art direction:

- straight grammar: rectilinear paths, reference classification, direct projection
- curved grammar: bent paths, redistributed classifications, topology-sensitive seams
- disagreement grammar: same observer condition, different transport assumption,
  different terminal evidence

## 2. Observatory Atmosphere

The frame succeeds because it feels like a computational chamber, not a sci-fi HUD.
The atmosphere is dark, quiet, and instrumented. Typography is restrained. Overlays
are sparse enough that the film-plane evidence remains primary.

Runtime interpretation:

- Use overlays as annotations on measured state, not as decorative atmosphere.
- Keep typography small, fixed, and label-oriented.
- Prefer thin lines, low-opacity fills, and stable panel positions.
- Let empty space remain empty when there is no measured signal.
- Avoid idle animation in measurement contexts.

Existing systems already support much of this posture:

| Runtime element | Current role |
| --- | --- |
| `ObservatoryModeController` | named overlay presets that answer one question at a time |
| `GrinObserveDemoHud` | mode/status readout and verification-safe cockpit reporting |
| `FilmOverlay2D` | film-plane rays, normals, gradient normals, grid, crosshair, traversal overlay |
| `RayBeamRenderer` | 3D ray and normal debug layer |
| `FieldSource3D` debug visualization | available but should remain diagnostics-only unless visually restrained |

The image's dark chamber composition should be treated as a hierarchy reference:
large film evidence first, field context second, metrics and minimaps third.

The atmosphere should never imply unmeasured energy transfer, radiation, turbulence,
or force. If the runtime does not measure it, the observatory should not visualize
it.

### Silence As Signal
Absence of overlays is itself an observability state.

Regions with no active diagnostics should remain visually quiet rather than filled with decorative instrumentation. Sparse frames preserve evidence hierarchy and improve operator attention to active transport distinctions.

Stillness and empty space are not missing presentation layers; they are part of the instrument language.

## 3. Traversal Emergence

The selected frame makes computation visible as a reveal process. Tiles are
untouched, pending, complete, or active. This is a runtime behavior, not a
decorative loading treatment.

Relevant existing runtime state:

- `FilmOverlay2D` stores traversal tile states.
- `FilmOverlay2D` stores traversal rows completed.
- `FilmOverlay2D` has active tile or band border coloring.
- `GrinObserveDemoHud` can report traversal overlay and minimap state in snapshots.
- `GrinFilmCamera` exposes row completion and tile-metrics scaffolding paths.
- render test capture paths can emit tile metrics summaries when instrumentation is enabled.

Runtime interpretation:

| Visual target behavior | Runtime meaning | Feasibility |
| --- | --- | --- |
| Tile completion visibility | which parts of the film have been visited or completed | Already implemented at overlay-data level |
| Active traversal region | current row, band, subtile, or tile being processed | Already implemented for current overlay concept; polish is low risk |
| Computational revelation pacing | film becomes visible as computation resolves | Low-risk presentation refinement |
| Row versus tile traversal comparison | scheduler order changes the spatial pattern of emergence | Partially possible; already supported by diagnostic runs |
| Per-region unresolved state | pixels or tiles that remain unclassified or unstable | Medium-risk live addition; already available in offline diagnostics |

The reveal should remain discrete. A tile is pending or complete; it should not fade
as if undergoing a physical transformation. Stillness is also meaningful: a stalled
completion indicator is an instrument reading, not an invitation to add decorative
activity.

## 4. Ownership Basin Implications

Ownership basins are the frame's most important structural implication. The visual
target suggests that transport changes which wall, collider, region, or domain owns
each pixel. That is a runtime-observable redistribution problem.

Relevant existing signals:

- fixture hit classification
- `FixtureDebugHitColoringEnabled`
- `FixtureTransportClassificationEnabled`
- `DomainTelemetry.CurvatureDomainKind`
- `DomainTelemetry.BoundaryConfidence`
- `SceneTransportMemory` diagnostic basin and seam records
- `ReferenceTransportOracle` comparison data
- offline ownership transition maps and production-versus-oracle diffs

Runtime interpretation:

| Ownership behavior | Meaning | Feasibility |
| --- | --- | --- |
| Seam structures | adjacent pixels disagree about owner, domain, normal, path, or confidence | Already visible offline; medium-risk live overlay |
| Classification redistribution | straight and curved runs assign different terminal categories | Partially possible; needs paired dashboard extraction |
| Boundary confidence | confidence of domain or boundary assignment | Runtime data exists; live panel integration is medium risk |
| Transport-dependent visibility topology | field changes the map of what can be seen from the observer | Future/oracle-dependent for robust claims |
| Basin comparison panels | compact straight/curved ownership maps | Low risk when using captured data; medium risk live |

The important language is redistribution, not deformation. The observatory should
show changed ownership and changed confidence. It should not imply that geometry
itself moved unless the scene state actually changed.

Seams should remain thin and semantic. A seam is not a glow effect; it is a boundary
between competing transport classifications.

## 5. Runtime-Feasible Implementation Candidates

| Classification | Candidate | Notes |
| --- | --- | --- |
| Already implemented | 2D film ray polylines | `FilmOverlay2D.DrawRays` exposes measured film-plane ray paths. |
| Already implemented | Hit normals and film-gradient normals | `FilmOverlay2D.DrawHitNormals` and `DrawFilmGradientNormals` support geometry and continuity inspection. |
| Already implemented | Comparison grid and crosshair | `FilmOverlay2D.ShowComparisonGrid` and `ShowComparisonCrosshair` provide spatial reference. |
| Already implemented | 3D ray debug overlay with hit-only filtering | `RayBeamRenderer.DebugMode` and `DebugDrawOnlyHits` support restrained ray inspection. |
| Already implemented | Named observatory mode presets | `ObservatoryModeController` provides Observer, Geometry, Ownership, Risk, Oracle, and Presentation modes. |
| Already implemented | Traversal overlay data path | `FilmOverlay2D` stores tile states, active tile or band information, and rows completed. |
| Already implemented | Hermetic classification outputs | `FixtureTransportClassificationEnabled`, coverage reporting, and fixture debug hit coloring support classification inspection. |
| Already implemented | Offline oracle artifacts | Existing diagnostic assets include production-versus-oracle diffs, ownership transition maps, normal/path deltas, and parent trajectory sheets. |
| Low-risk addition | Restrained split-frame dashboard composition | Can compose existing straight and curved captures without changing transport. |
| Low-risk addition | Observatory typography and sparse status panels | Styling/presentation layer only; should preserve fixed semantic positions. |
| Low-risk addition | Traversal minimap extraction | Existing tile state can drive a compact minimap-style panel. |
| Low-risk addition | Active traversal tile polish | Thin border and quiet state colors are presentation-level refinements. |
| Low-risk addition | Ownership basin panel from classification data | Existing classification/hit coloring data can be summarized into a small panel. |
| Medium-risk addition | Live dual-reality split frame | Requires synchronized straight and curved runtime state or paired render surfaces. |
| Medium-risk addition | Live ownership seam confidence map | Requires careful wiring from domain telemetry and boundary confidence into a live overlay. |
| Medium-risk addition | Transport metrics panel | Aggregates hits, path length, unique owners, wasted samples, and confidence without overloading the frame. |
| Medium-risk addition | Observer-disagreement overlay | Requires paired straight/curved classification deltas and clear visual hierarchy. |
| Medium-risk addition | Dashboard hierarchy orchestration | Must prevent all panels from competing equally; one dominant concept per mode. |
| Future/oracle-dependent | Oracle-stabilized path overlays in live mode | Oracle data is diagnostic-only and must not feed runtime decisions. |
| Future/oracle-dependent | Convergence ladder and precision closure panels | Best suited to offline or oracle microscopy contexts. |
| Future/oracle-dependent | Transport-dependent visibility topology | Requires robust multi-run or oracle-backed comparison before strong claims. |
| Future/oracle-dependent | Classification redistribution confidence beyond heuristics | Needs validated confidence model rather than visual inference. |
| Future/oracle-dependent | Phase-space contour drift | Only acceptable if backed by measured transport metadata. |

## 6. Dashboard Extraction Opportunities

### Minimap Concepts

The minimap should answer one question: where is computation or uncertainty located?

Candidate minimaps:

- traversal completion grid from `FilmOverlay2D` tile states
- active row, band, subtile, or tile indicator
- ownership basin thumbnail from classification data
- boundary confidence thumbnail from domain telemetry
- oracle difference thumbnail in microscopy mode only

The minimap should be small, low contrast, and secondary. It must not become a
second primary frame.

### Split-Frame Concepts

The split-frame opportunity is straight reference versus curved GRIN transport under
matched scene and camera conditions.

Useful split-frame modes:

- ray path comparison
- terminal wall or collider classification comparison
- ownership basin comparison
- boundary confidence comparison
- production versus oracle comparison for diagnostic review

The divider should be neutral and stable. The split should not animate unless the
interaction itself is explicitly about moving between assumptions.

### Traversal HUD Opportunities

Traversal HUD elements should expose renderer state without becoming a loading
screen.

Useful readouts:

- traversal mode
- rows completed
- active row, tile, band, or subtile
- tiles complete versus total
- classified coverage
- unresolved or pending region count when available
- elapsed render time in capture contexts

The traversal HUD should support the film reveal. It should not compete with the
film reveal.

### Observatory Mode Candidates

The existing observatory modes already provide a strong control surface. The target
frame suggests several higher-level dashboard presentations built on those modes:

| Candidate mode | Dominant concept | Supporting overlays |
| --- | --- | --- |
| Transport Divergence | dual-reality disagreement | split frame, ray paths, minimal classification labels |
| Traversal Emergence | computation becoming visible | tile states, active region, rows completed |
| Ownership Basin | classification redistribution | basin tint, seam emphasis, boundary confidence |
| Continuity / Risk | local transport ambiguity | film-gradient normals, seam markers, unresolved regions |
| Oracle Microscopy | convergence and closure | oracle diff, precision closure, path deltas |
| Presentation Observatory | non-expert readability | minimal labels, one concept, no dense numeric panels |

Each mode should decide the dominant concept before selecting overlays. Overlay
selection follows the question; it should not begin from the available widgets.

## Guardrails

- Do not recreate the selected image directly.
- Do not add fake field energy, decorative particles, unmeasured radiation, or
  synthetic turbulence.
- Do not use speculative physics visuals as if they were runtime evidence.
- Do not make every panel equally bright or equally important.
- Do not allow typography to become explanatory clutter.
- Do not turn oracle diagnostics into runtime authority.
- Do not let minimaps or metrics displace the film-plane evidence.
- Do not use motion unless a measured state changed.
- Do not hide uncertainty; show unresolved or low-confidence regions when available.
- Do not trade calm computational legibility for visual excitement.

The frame's success condition is quiet observability: the viewer can see where the
renderer is working, which transport assumption is active, how the assumptions
disagree, and where the evidence becomes uncertain.
