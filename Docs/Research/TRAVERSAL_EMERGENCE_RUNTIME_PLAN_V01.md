# Traversal Emergence Runtime Plan V01

## Summary

Phase 3.2 introduces a restrained runtime choreography layer for xPRIMEray
observability. The goal is to reveal measured renderer state progressively over
time without changing transport semantics, scheduler behavior, integrator state,
hit selection, or oracle authority.

The implemented scaffold is `TraversalEmergenceSequencer`, an optional Godot node
that stages existing overlay toggles. `ObservatoryModeController` now exposes a
capture-friendly `TraversalEmergenceObservatoryMode` preset on Ctrl+7 when a
sequencer node is present.

This is an observability sequence, not a rendering feature. It controls what the
observer sees, not what the renderer computes.

## Runtime Safety Contract

The sequence may only display measured runtime or diagnostic state already present
in the project:

- traversal completion and tile states from `FilmOverlay2D`
- active tile, row, band, or subtile borders from traversal overlay state
- ownership classifications through fixture debug hit coloring
- ray and hit overlays from `FilmOverlay2D` and `RayBeamRenderer`
- continuity indicators from existing film-gradient diagnostics
- boundary confidence and oracle diagnostics only when already produced by existing
  telemetry or diagnostic paths

The sequence must not:

- add fake physics effects
- add decorative particles
- add synthetic turbulence
- add cinematic lens effects
- invent transport data
- modify scheduler order
- alter transport, hit selection, field evaluation, or oracle semantics

`ReferenceTransportOracle` remains diagnostic-only. Oracle-derived visuals may be
shown in an oracle/microscopy context, but they must not feed rendering or scheduling.

## Stage Sequence

| Stage | Dominant concept | Runtime behavior | Current scaffold |
| --- | --- | --- | --- |
| A | Traversal completion | Show pending/complete regions and active traversal region only. | `ShowTraversalOverlay=true`, `ShowTraversalMinimap=true`; rays, normals, comparison, and renderer debug off. |
| B | Ownership basin emergence | Reveal hit-confirmed ownership/classification structure. | Enables hit-only rays and fixture debug hit coloring; keeps traversal minimap secondary. |
| C | Transport disagreement | Surface straight/curved comparison posture without inventing split-frame data. | Enables ray paths plus comparison grid/crosshair; relies on matched straight/curved capture workflow for actual disagreement evidence. |
| D | Continuity/risk where disagreement exists | Reveal risk diagnostics only when there is measured disagreement context. | Conservative by default: broad continuity fallback is disabled unless `EnableFallbackContinuityWithoutDisagreementMask=true`. |
| E | Stabilized observatory | Return to quiet settled instrument state. | Suppresses dense overlays; keeps traversal minimap as a low-weight status artifact. |

Stage D is intentionally conservative. The current runtime does not yet provide a
live measured disagreement mask that can localize continuity/risk overlays only to
disagreement regions. The scaffold therefore does not pretend one exists. A prototype
fallback can be enabled for review, but it is labeled as broad continuity rather than
localized disagreement.

## Timing and Controls

`TraversalEmergenceSequencer` provides lightweight sequencing:

- fixed-duration stages via `StageDurationSeconds`
- timed advancement via `EnableTimedAdvance`
- optional looping via `Loop`
- public `StartSequence()`, `StopSequence()`, `AdvanceStage()`, `RetreatStage()`,
  and `SetStage(...)` methods
- optional manual stepping hotkeys: Ctrl+Alt+Right and Ctrl+Alt+Left

The timing system advances overlay states only. It does not affect render cadence,
scheduler policy, traversal order, step budgets, or capture timing.

## Overlay Hierarchy Enforcement

Each stage has one dominant observability concept. Overlay choices are intentionally
limited so the frame does not flatten into equal-weight dashboard noise.

Hierarchy rules:

- Stage A: traversal completion dominates; no ray or continuity overlays.
- Stage B: ownership/classification dominates; traversal is reduced to a minimap.
- Stage C: transport assumption comparison dominates; classification remains
  contextual rather than visually maximal.
- Stage D: continuity/risk dominates only if measured disagreement support exists.
- Stage E: stabilization dominates; dense diagnostics are suppressed.

Secondary overlays should remain either absent or visibly subordinate. If a later
implementation adds panels, panels must follow the stage question instead of exposing
all available diagnostics simultaneously.

## Runtime Integration

Implemented runtime scaffolding:

- `TraversalEmergenceSequencer.cs`
  - optional node
  - stages existing measured overlays
  - restores initial fixture hit-coloring state when stopped
  - defaults to no broad risk overlay without a measured disagreement mask
- `ObservatoryModeController.cs`
  - adds `TraversalEmergenceObservatoryMode`
  - maps Ctrl+7 to the capture-friendly sequence preset
  - starts/stops the sequencer when switching into or out of the mode
- observe harness wiring
  - `test-grin-basic-visual-offaxis-observe.tscn`
  - `test-straight-basic-visual-offaxis-observe.tscn`

The sequencer node paths default to the current observe-scene layout:

```text
OverlayPath  = ../CanvasLayer/FilmOverlay2D
HudPath      = ../CanvasLayer/DemoHud
RendererPath = ../FixtureGrinBasicVisual/RayBeamRenderer
FilmCameraPath = ../GrinFilmCamera
```

Scenes with different fixture roots can still use the sequencer by adjusting exported
node paths in the Inspector.

## Future Runtime Work

Low-risk next additions:

- expose stage/state in verification snapshots
- add a compact stage label style for capture output
- add stage-specific color presets that only affect overlay colors, not transport
- add an explicit measured disagreement mask input once paired straight/curved
  classification deltas are available live

Medium-risk additions:

- live synchronized straight/curved split-frame composition
- live ownership seam confidence overlay from domain telemetry
- measured continuity/risk masking limited to disagreement regions
- dashboard panel orchestration that enforces one dominant concept per stage

Oracle-dependent additions:

- oracle closure stage using existing oracle artifacts
- convergence ladder reveal for microscopy captures
- production-versus-oracle disagreement masks
- precision closure overlays, kept diagnostic-only

## Acceptance Checks

- Ctrl+7 enters `TraversalEmergenceObservatoryMode` in observe scenes with the
  sequencer node.
- The sequence advances through A-E by timer or manual stepping.
- Stage A shows traversal completion only.
- Stage B enables ownership/classification visualization without scheduler changes.
- Stage C shows comparison posture without inventing disagreement data.
- Stage D remains quiet by default when no measured disagreement mask is available.
- Stage E returns to a quiet observatory state.
- Switching away from Ctrl+7 stops the sequence and restores initial fixture
  hit-coloring state.
- `dotnet build "Physical Light and Camera Units.csproj" --no-restore` succeeds.

## Guardrails

Traversal emergence is measured-state choreography. It should feel calm, legible,
and computational. The sequence should help an observer understand where the renderer
is working, what has resolved, what evidence is being surfaced, and where the current
runtime does not yet have enough measured data to show more.

When in doubt, keep the stage quiet.
