# GD_xPRIMEray Architecture Probe: Publicization Pass

Date: 2026-04-11

## Scope

This probe focuses on the runtime path and the handoff points between:

- curved-ray transport
- GRIN / metric field authoring
- film rendering and overlays
- research harnesses and validation
- public-facing scene entry points

Primary files inspected:

- [GrinFilmCamera.cs](/home/bb/code/godot_xPRIMEray/GrinFilmCamera.cs:3233)
- [RayBeamRenderer.cs](/home/bb/code/godot_xPRIMEray/RayBeamRenderer.cs:8)
- [FieldSource3D.cs](/home/bb/code/godot_xPRIMEray/FieldSource3D.cs:6)
- [BoundaryLayerVolume.cs](/home/bb/code/godot_xPRIMEray/BoundaryLayerVolume.cs:3)
- [RendererCore/Config/ResearchModeConfig.cs](/home/bb/code/godot_xPRIMEray/RendererCore/Config/ResearchModeConfig.cs:40)
- [RendererCore/Testing/RenderTestRunner.cs](/home/bb/code/godot_xPRIMEray/RendererCore/Testing/RenderTestRunner.cs:9)
- [GodotAdapter/SnapshotBuilder.cs](/home/bb/code/godot_xPRIMEray/GodotAdapter/SnapshotBuilder.cs:15)
- [FilmOverlay2D.cs](/home/bb/code/godot_xPRIMEray/FilmOverlay2D.cs:6)
- [RendererCore/Common/DebugOverlayBus.cs](/home/bb/code/godot_xPRIMEray/RendererCore/Common/DebugOverlayBus.cs:6)
- [RendererCore/Common/FrameSnapshotBus.cs](/home/bb/code/godot_xPRIMEray/RendererCore/Common/FrameSnapshotBus.cs:3)
- [test.tscn](/home/bb/code/godot_xPRIMEray/test.tscn:1)
- [test-wormhole-prototype.tscn](/home/bb/code/godot_xPRIMEray/test-wormhole-prototype.tscn:1)
- [overspace_trophy_room_demo.tscn](/home/bb/code/godot_xPRIMEray/overspace_trophy_room_demo.tscn:1)

## 1. Concise Architecture Map

### Runtime flow

1. Scene authoring happens through Godot nodes:
   - `FieldSource3D` exports field shape, curve, transport model, and heavy academic debug-viz controls.
   - `BoundaryLayerVolume` exports shell/box remap or directional-bias behavior and self-registers into `boundary_layer_volumes`.
   - scene wrappers place `Camera3D`, `GrinFilmCamera`, `RayBeamRenderer`, `FilmOverlay2D`, and optionally `RenderTestRunner`.

2. Snapshot extraction happens per render step:
   - `GrinFilmCamera.RenderFrameBackend()` builds a `SceneSnapshot` through `GodotAdapter.SnapshotBuilder.BuildFromGodotScene(...)`, then adds a `CurvatureBoundGrid`, then publishes it on `FrameSnapshotBus` ([GrinFilmCamera.cs](/home/bb/code/godot_xPRIMEray/GrinFilmCamera.cs:3653), [GodotAdapter/SnapshotBuilder.cs](/home/bb/code/godot_xPRIMEray/GodotAdapter/SnapshotBuilder.cs:15), [RendererCore/Common/FrameSnapshotBus.cs](/home/bb/code/godot_xPRIMEray/RendererCore/Common/FrameSnapshotBus.cs:3)).

3. Effective runtime config is assembled in one place:
   - `GrinFilmCamera.ResolveEffectiveConfig()` merges its own exports, `RayBeamRenderer.SharedSnapshot`, broadphase policy, soft-gate policy, threading, telemetry, fixture debug, and `ResearchModeConfig` overrides into one `EffectiveConfig` ([GrinFilmCamera.cs](/home/bb/code/godot_xPRIMEray/GrinFilmCamera.cs:15408)).

4. Transport logic lives mostly in `RayBeamRenderer`:
   - field nodes are snapped into `FieldSourceSnap`
   - boundary nodes are snapped into `BoundaryLayerSnap`
   - pass-1 segment generation and transport stepping use those snapshots
   - transport mode dispatch currently routes among GRIN, metric stub, and hybrid stub ([RayBeamRenderer.cs](/home/bb/code/godot_xPRIMEray/RayBeamRenderer.cs:611), [RayBeamRenderer.cs](/home/bb/code/godot_xPRIMEray/RayBeamRenderer.cs:3087), [RayBeamRenderer.cs](/home/bb/code/godot_xPRIMEray/RayBeamRenderer.cs:5196)).

5. Film presentation and overlay happen through two layers:
   - `GrinFilmCamera` owns the film image, accumulation cadence, HUD metadata, telemetry heatmaps, and the decision to drive the shared overlay bus
   - `FilmOverlay2D` draws world-space ray debug, hit normals, film-gradient normals, comparison grid/crosshair, and any `DebugOverlayBus` text/lines ([GrinFilmCamera.cs](/home/bb/code/godot_xPRIMEray/GrinFilmCamera.cs:6666), [FilmOverlay2D.cs](/home/bb/code/godot_xPRIMEray/FilmOverlay2D.cs:6), [RendererCore/Common/DebugOverlayBus.cs](/home/bb/code/godot_xPRIMEray/RendererCore/Common/DebugOverlayBus.cs:6)).

6. Harnesses and validation sit beside the main render path, not outside it:
   - `RenderTestRunner` configures scenes and mutates `GrinFilmCamera` via `CaptureTestRunDefaults()` / `ApplyTestRunConfig(...)`
   - wormhole validation pulls many `TryGet...ForTesting()` snapshots from `GrinFilmCamera`
   - fixture controllers also stamp HUD state into `GrinFilmCamera` ([RendererCore/Testing/RenderTestRunner.cs](/home/bb/code/godot_xPRIMEray/RendererCore/Testing/RenderTestRunner.cs:42), [GrinFilmCamera.cs](/home/bb/code/godot_xPRIMEray/GrinFilmCamera.cs:4285), [GrinFilmCamera.cs](/home/bb/code/godot_xPRIMEray/GrinFilmCamera.cs:4530), [Fixtures/GrinBasicVisualController.cs](/home/bb/code/godot_xPRIMEray/Fixtures/GrinBasicVisualController.cs:37), [Wormhole/WormholePrototypeRig.cs](/home/bb/code/godot_xPRIMEray/Wormhole/WormholePrototypeRig.cs:2005)).

### Scene entry points

- `test.tscn` is still the project main scene and acts as a mixed-purpose harness scene: free-fly camera, film camera, ray renderer, overlay, render-test runner, field, and boundary layer all in one place ([project.godot](/home/bb/code/godot_xPRIMEray/project.godot:18), [test.tscn](/home/bb/code/godot_xPRIMEray/test.tscn:1)).
- `test-*.tscn` scenes are mostly fixture wrappers around `GrinFilmCamera` + `FilmOverlay2D`, with `RenderTestRunner` attached for benchmarkable fixtures.
- `test-wormhole-prototype.tscn` is the strongest candidate for “public playable mode” because it already has a distinct rig, free-fly player camera, dual-reality overlays, validation capture, and portal-topology semantics ([test-wormhole-prototype.tscn](/home/bb/code/godot_xPRIMEray/test-wormhole-prototype.tscn:1)).
- `overspace_trophy_room_demo.tscn` is a separate public/demo branch: polished environment, free-fly camera, summary label, no research-harness dependency ([overspace_trophy_room_demo.tscn](/home/bb/code/godot_xPRIMEray/overspace_trophy_room_demo.tscn:1)).

## 2. Dependency and Responsibility Breakdown

### `FieldSource3D`

Current role:

- scene authoring node for field definitions
- canonical-to-legacy compatibility bridge
- debug-viz renderer and academic explainer panel
- source of truth for `ResolveEffectiveParams(...)`
- membership in `field_sources` group for renderer discovery ([FieldSource3D.cs](/home/bb/code/godot_xPRIMEray/FieldSource3D.cs:351), [FieldSource3D.cs](/home/bb/code/godot_xPRIMEray/FieldSource3D.cs:372), [FieldSource3D.cs](/home/bb/code/godot_xPRIMEray/FieldSource3D.cs:730), [FieldSource3D.cs](/home/bb/code/godot_xPRIMEray/FieldSource3D.cs:821)).

Gameplay-facing:

- yes, for public sandbox authoring
- but its inspector is currently research-first and overloaded

Research-only weight:

- very high: academic reference strings, debug density vectors/rays/zones, legacy compatibility surface

Hidden coupling:

- `SnapshotBuilder` depends on `ResolveEffectiveParams(...)` and `GetWorldInfluenceAabbConservative()`
- `RayBeamRenderer` also builds `FieldSourceSnap` directly from scene nodes

### `BoundaryLayerVolume`

Current role:

- lightweight authoring node for remap shells or directional-bias volumes
- self-registration via `boundary_layer_volumes`
- no hot-path logic inside the node itself ([BoundaryLayerVolume.cs](/home/bb/code/godot_xPRIMEray/BoundaryLayerVolume.cs:14), [BoundaryLayerVolume.cs](/home/bb/code/godot_xPRIMEray/BoundaryLayerVolume.cs:139)).

Gameplay-facing:

- yes, especially for wormhole/public topology affordances

Research-only weight:

- low in the node itself
- higher in the validation and summary path inside `RayBeamRenderer`

### `RayBeamRenderer`

Current role:

- transport primitive owner
- shared configuration source for marching/collision/debug knobs
- field and boundary snapshot builder
- pass-1 segment builder
- transport mode dispatcher
- legacy live debug renderer
- boundary validation summary generator ([RayBeamRenderer.cs](/home/bb/code/godot_xPRIMEray/RayBeamRenderer.cs:280), [RayBeamRenderer.cs](/home/bb/code/godot_xPRIMEray/RayBeamRenderer.cs:611), [RayBeamRenderer.cs](/home/bb/code/godot_xPRIMEray/RayBeamRenderer.cs:666), [RayBeamRenderer.cs](/home/bb/code/godot_xPRIMEray/RayBeamRenderer.cs:3087), [RayBeamRenderer.cs](/home/bb/code/godot_xPRIMEray/RayBeamRenderer.cs:5840), [RayBeamRenderer.cs](/home/bb/code/godot_xPRIMEray/RayBeamRenderer.cs:6005)).

Gameplay-facing:

- partially
- today it is more “transport kernel + debug node” than a public gameplay system

Research-only weight:

- medium-high
- metric transport is still scaffold/stub-heavy and diagnostic-first

Hidden coupling:

- it exports many fields that `GrinFilmCamera` mirrors into `EffectiveConfig`
- its “shared with film camera” section makes the separation explicit, but also confirms tight coupling

### `GrinFilmCamera`

Current role:

- film renderer
- frame scheduler
- effective config assembler
- preset system
- runtime macro system
- broadphase policy controller
- telemetry heatmap generator
- render-health tracker
- HUD metadata emitter
- fixture debug classifier
- wormhole portal-sector analytics collector
- testing API surface
- bridge to both legacy and core backends ([GrinFilmCamera.cs](/home/bb/code/godot_xPRIMEray/GrinFilmCamera.cs:3233), [GrinFilmCamera.cs](/home/bb/code/godot_xPRIMEray/GrinFilmCamera.cs:3653), [GrinFilmCamera.cs](/home/bb/code/godot_xPRIMEray/GrinFilmCamera.cs:6666), [GrinFilmCamera.cs](/home/bb/code/godot_xPRIMEray/GrinFilmCamera.cs:15254), [GrinFilmCamera.cs](/home/bb/code/godot_xPRIMEray/GrinFilmCamera.cs:15408), [GrinFilmCamera.cs](/home/bb/code/godot_xPRIMEray/GrinFilmCamera.cs:17267)).

Gameplay-facing:

- yes, because every playable/sandbox scene routes through it for visible output

Research-only weight:

- extremely high

Hidden coupling:

- owns test-facing API used by `RenderTestRunner`, fixture controllers, and wormhole validation
- directly constructs snapshots instead of delegating to a dedicated runtime coordinator
- mirrors `RayBeamRenderer` state and also interprets research config

### `ResearchModeConfig`

Current role:

- pure data struct for research transport, invariants, determinism, and validation metadata ([RendererCore/Config/ResearchModeConfig.cs](/home/bb/code/godot_xPRIMEray/RendererCore/Config/ResearchModeConfig.cs:40)).

Strength:

- this is already the cleanest public/private seam in the repo

Weakness:

- the clean seam is not yet matched by a clean runtime owner; the config is portable, the orchestration is not

### `RenderTestRunner`

Current role:

- benchmark/harness controller
- scene selector
- CLI contract surface
- auto-calibration/shadow-eval/smart-scale runner
- capture exporter
- reflection-based accessor for internal `GrinFilmCamera` stats ([RendererCore/Testing/RenderTestRunner.cs](/home/bb/code/godot_xPRIMEray/RendererCore/Testing/RenderTestRunner.cs:42), [RendererCore/Testing/RenderTestRunner.cs](/home/bb/code/godot_xPRIMEray/RendererCore/Testing/RenderTestRunner.cs:145)).

Gameplay-facing:

- no

Research-only:

- yes, strongly

Risk:

- it knows too much about internals and relies on reflection for some live stats, which is a smell for missing explicit telemetry interfaces

### Overlay / telemetry / HUD layer

Pieces:

- `FilmOverlay2D`: drawing shell
- `DebugOverlayBus`: global immediate overlay bus
- `PerfStats` / `PerfScope`: frame timings and rolling printouts
- `GrinFilmCamera`: render-health overlay synthesis and HUD metadata
- wormhole-specific overlays: `WormholeResearchOverlay`, `WireframeReferenceOverlay`, `CameraSpaceCollisionOverlay`

Interpretation:

- the repo already has the bones of a very good pedagogical/debug UX layer
- the issue is not absence, but lack of mode separation and ownership clarity

## 3. Top Architectural Friction Points

### 1. `GrinFilmCamera` is a clear god object

Evidence:

- 17,876 lines
- owns `_Ready`, `_Process`, snapshot publication, preset application, runtime macros, smart-scale, broadphase auto mode, telemetry arrays, render-health windows, HUD lines, research config merge, test export methods, and wormhole diagnostics ([GrinFilmCamera.cs](/home/bb/code/godot_xPRIMEray/GrinFilmCamera.cs:3233), [GrinFilmCamera.cs](/home/bb/code/godot_xPRIMEray/GrinFilmCamera.cs:15254), [GrinFilmCamera.cs](/home/bb/code/godot_xPRIMEray/GrinFilmCamera.cs:17267)).

Why it hurts publicization:

- every new public-facing feature currently has a strong temptation to land here
- inspector discoverability suffers because the camera becomes the place for transport, HUD, benchmark, and research toggles
- testing and gameplay both depend on the same monolith

### 2. Transport ownership is split between `RayBeamRenderer` and `GrinFilmCamera`

Observed split:

- `RayBeamRenderer` owns march primitives and shared transport knobs
- `GrinFilmCamera` decides the actual runtime config and scheduling
- both contain render-space safety logic and debug overlay concerns

Why it hurts:

- the mental model is not “camera asks transport service to render”, but “camera partially absorbs renderer”
- extension points for alternative frontends are weaker than they look

### 3. Snapshot building is correct in direction, but not yet the dominant boundary

Good:

- `SnapshotBuilder` and `ResearchModeConfig` are strong architectural seeds

Current limitation:

- much of the runtime still reaches around those boundaries via scene groups, mirrored snapshots, and test hooks on `GrinFilmCamera`

Why it hurts:

- public and research modes still share too much scene-tree and Godot-node coupling in their control path

### 4. The scene layer mixes “entry scene”, “fixture wrapper”, and “developer harness”

Observed:

- `test.tscn` is main scene and also a harness
- `test-*.tscn` names read as internal fixtures, but several are the real human entry points
- wormhole and overspace experiences live beside fixture scenes rather than under a clear product/demo root

Why it hurts:

- a new user cannot immediately tell where to start
- public-facing discovery is slower than it needs to be

### 5. Overlay ownership is fragmented

Observed ownership:

- `RayBeamRenderer` has live debug overlay drawing
- `GrinFilmCamera` decides when to push overlay data
- `FilmOverlay2D` draws it
- `DebugOverlayBus` is global and mode-agnostic
- wormhole overlays are separate bespoke systems

Why it hurts:

- it is easy to produce useful diagnostics, but hard to explain which overlay belongs to which mode and user

### 6. Research harness code is not cleanly outside the runtime

Observed:

- `RenderTestRunner` mutates runtime behavior directly
- `GrinFilmCamera` exposes many `TryGet...ForTesting()` methods
- wormhole validation relies on the same testing hooks

Why it hurts:

- research and validation are not merely observers; they shape the main runtime object

## 4. Recommended Separation Strategy

### A. Research Harness Mode

Primary user:

- contributor validating transport correctness, comparing profiles, exporting telemetry, running matrix sweeps

Should own:

- `RenderTestRunner`
- CLI profile application
- auto-calibration and shadow-eval
- telemetry export
- validation capture
- benchmark-only HUD panels

Should consume runtime via:

- explicit runtime facade interfaces
- immutable diagnostic snapshots
- dedicated capture/telemetry services

Should not do:

- reflection into live internal fields
- direct mutation of dozens of `GrinFilmCamera` exports as the long-term path

### B. Sandbox Mode

Primary user:

- technically curious player / developer exploring fields, presets, overlays, and camera motion interactively

Should own:

- field authoring controls
- curated transport presets
- camera controls
- comparison overlays
- a compact “what is the renderer doing?” HUD

Needs:

- fewer inspector knobs by default
- high-signal preset groupings
- explicit scene root like `SandboxRig` or `CurvedTransportSandbox`

This is the natural home for:

- current `test-grin-basic-visual*`
- `test-metric-basic-visual*`
- a cleaned-up successor to `test.tscn`

### C. Public Playable Mode

Primary user:

- visitor who should feel the GRIN / portal world before understanding the implementation

Should own:

- authored movement, presentation, and onboarding
- mode-specific overlays with public language
- hidden advanced controls behind debug or research toggles

Should consume:

- a stable runtime render service
- a public debug-presenter layer

Current best seeds:

- `test-wormhole-prototype.tscn`
- `overspace_trophy_room_demo.tscn`

### Proposed layering

1. `RendererCore` remains the transport/data domain.
2. `GodotAdapter` becomes the only scene-to-runtime extraction layer.
3. New runtime coordinator layer sits above both `RayBeamRenderer` and `GrinFilmCamera` concerns.
4. Three frontends sit on top:
   - `ResearchHarnessController`
   - `SandboxController`
   - `PlayableExperienceController`

## 5. Concrete Additive Refactor Suggestions

### 1. Extract a `FilmRuntimeController` from `GrinFilmCamera`

Smallest useful move:

- move `RenderFrameBackend`, snapshot publication, and backend dispatch into a dedicated helper/service class

Target responsibility:

- “given scene snapshot + effective config + active camera, produce a film frame”

Immediate gain:

- `GrinFilmCamera` starts becoming a presenter/config shell instead of the entire runtime

### 2. Extract a `FilmTelemetryService`

Move out of `GrinFilmCamera`:

- render-health sampling
- overlay rolling windows
- telemetry heatmap accumulation
- adaptive-envelope telemetry bookkeeping

Immediate gain:

- research harness and sandbox HUD can consume the same typed telemetry without binding themselves to the film camera monolith

### 3. Extract a `FilmHudPresenter`

Move out of `GrinFilmCamera`:

- `EmitRenderMetricsOverlay()`
- HUD metadata formatting
- mode labels
- probe-only overlay lines

Immediate gain:

- easier distinction between:
  - research HUD
  - sandbox teaching HUD
  - public lightweight HUD

### 4. Formalize a `IRenderDiagnosticsSource` or snapshot DTO

Replace the growth of `TryGet...ForTesting()` with:

- a single immutable diagnostics snapshot returned per frame or on demand

Include:

- perf
- render health
- fixture hit stats
- telemetry summary
- wormhole remap summary

Immediate gain:

- `RenderTestRunner`, wormhole validation, and future UI panels stop depending on many camera-specific methods

### 5. Split `FieldSource3D` inspector surface into public and research strata

Additively:

- keep current fields
- add grouped booleans such as `ShowResearchInspector` or `AuthoringComplexity`
- hide legacy and most academic debug-viz by default for sandbox/public scenes

Immediate gain:

- preserves research power without overwhelming public sandbox authoring

### 6. Introduce scene-root naming and folder conventions for mode clarity

Additively create:

- `Scenes/Research/...`
- `Scenes/Sandbox/...`
- `Scenes/Public/...`

Then move or alias entry scenes gradually.

Suggested first mapping:

- `test-curved-minimal*.tscn` -> research fixtures
- `test-grin-basic-visual*.tscn` and `test-metric-basic-visual*.tscn` -> sandbox fixtures
- `test-wormhole-prototype.tscn` and `overspace_trophy_room_demo.tscn` -> public prototypes

### 7. Introduce a `TransportProfileAsset`

Problem today:

- presets live in `GrinFilmCamera`, shared values live in `RayBeamRenderer`, fixture controllers also patch values

Additive solution:

- create a serializable profile asset or resource for:
  - transport mode
  - march settings
  - film quality settings
  - broadphase policy
  - debug budget level

Immediate gain:

- makes “Walk / Preview / Cinematic / Research” feel inevitable instead of arbitrary
- supports public UX and reproducible research profiles

### 8. Move boundary validation ownership toward a dedicated validator

Today:

- `RayBeamRenderer` both runs boundary logic and prints validation summaries

Suggested split:

- transport kernel keeps counters
- validator/service formats and exports summaries

Immediate gain:

- cleaner separation between runtime behavior and research reporting

## 6. Classes Most at Risk of Becoming or Remaining God Objects

Highest risk:

- `GrinFilmCamera`

Medium risk:

- `RenderTestRunner`
- `WormholePrototypeRig`
- `RayBeamRenderer`

Interpretation:

- `WormholePrototypeRig` is acceptable as an experience-specific composition root for now
- `RenderTestRunner` can remain large if it stays harness-only
- `RayBeamRenderer` is large, but its responsibilities are more coherent than `GrinFilmCamera`

## 7. Extension Points for Public Interactive Use

### Strong existing extension points

- `FieldSource3D` as scene-authored field authoring primitive
- `BoundaryLayerVolume` as topology / medium boundary primitive
- `ResearchModeConfig` as portable research feature gate
- `SnapshotBuilder` as Godot-to-runtime bridge
- `FilmOverlay2D` as screen-space explanatory layer
- wormhole overlay stack as proof that pedagogical visualization can coexist with play

### Best next public-facing extension points

- a curated preset panel that edits a small transport-profile surface rather than raw camera exports
- a “what you are seeing” overlay fed by runtime telemetry, not harness logs
- a scene selector that clearly labels:
  - research fixture
  - sandbox exploration
  - public experience

## 8. Bottom-Line Recommendations

If the goal is to make the repo publicly explorable without sacrificing research power, the highest-leverage path is:

1. keep `RendererCore`, `ResearchModeConfig`, `SnapshotBuilder`, `FieldSource3D`, and `BoundaryLayerVolume` as the foundational substrate
2. stop growing `GrinFilmCamera` as the universal owner
3. extract telemetry/HUD/runtime orchestration from `GrinFilmCamera` before adding more public UX on top
4. make scene-mode boundaries explicit in naming and folder structure
5. treat wormhole and overspace as the first true public-facing branches, not as side experiments

In short:

- the repo already contains both a research sandbox and the beginnings of a polished interactive experience
- the main architectural obstacle is not missing systems, but missing separation of concerns around the film camera and harness orchestration
