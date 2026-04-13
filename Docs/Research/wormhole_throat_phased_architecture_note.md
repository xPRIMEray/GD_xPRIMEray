# Wormhole Throat Phased Architecture Note

## Purpose

This note defines the next coherent architecture upgrade for the wormhole system in the active WSL repo. It is intended to be dropped into the repo under a research-oriented path and used as a stable context anchor for future Codex threads.

The goal is not to jump immediately into full Einstein-style wormhole transport. The goal is to create a practical phased bridge from the current linked-portal overspace demo toward a throat-aware system that is:

* aligned with academic wormhole terminology
* exposed through Godot inspector-friendly adaptor code
* runnable in the existing test harness flow
* capable of generating deterministic artifacts for validation

## Why this note exists

The current public-facing Overspace demo already proves a real runtime path for linked wormhole traversal and debug overlay capture. However, the present wormhole implementation is still best understood as a linked-mouth remap / portal prototype rather than a full throat-aware transport model.

This upgrade note preserves that win and organizes the next steps into bounded phases.

## Current baseline

The current repo already contains:

* a public-facing overspace trophy-room scene
* linked portal rendering and traversal hooks
* overspace metadata structures
* boundary-layer crossing support in renderer-side transport
* debug / telemetry infrastructure
* deterministic validation habits and artifact generation

This gives us enough architectural footing to introduce an explicit wormhole throat concept without rewriting the renderer.

## Design principle

We should separate three levels clearly:

### 1. Portal / linked-mouth behavior

This is the current practical traversal shell:

* linked destination preview
* camera/player remap
* public demo interaction

### 2. Throat control layer

This is the next architecture target:

* explicit wormhole throat terminology
* inspector-exposed control fields
* shell / transition behavior near the mouth
* diagnostics that make the throat legible

### 3. Full transport-through-throat physics

This is the later research destination:

* chart-aware transitions
* metric / geodesic transport
* throat-region optical effects
* academically aligned wormhole rendering behavior

The immediate upgrade should focus on level 2 while staying compatible with levels 1 and 3.

## Academic terminology to align with

The code and inspector-facing properties should begin using throat-aligned terminology where reasonable.

Recommended conceptual terms:

* **Mouth**: the entrance sphere / portal surface visible to the player
* **Throat**: the transition region associated with crossing and remap behavior
* **Throat Radius**: the characteristic radius of the wormhole entrance / transition shell
* **Chart Transition**: later-term concept for transport moving from one coordinate patch to another
* **Parent Region / Child Region**: overspace organization identity
* **Linked Mouth**: the paired destination mouth
* **Zero Phase Marker**: the local reference direction used for clocked remap
* **Spin Direction / Handedness**: orientation metadata for phase mapping

We do not need full GR chart dispatch yet, but we should avoid naming that blocks that direction later.

## Phase plan

# Phase A — Throat-Aware Portal Framework

## Objective

Introduce a formal throat architecture layer on top of the current wormhole portal system.

## Deliverable

A wormhole object with explicit throat-related inspector controls and a stable overspace demo milestone in the existing trophy-room / z-zone architecture.

## Scope

### Add explicit throat metadata

Additive inspector-facing properties should exist for at least:

* `WormholeId`
* `LinkedWormholeId`
* `ParentRegionId`
* `ChildRegionId`
* `ThroatRadius`
* `ZeroPhaseAngle`
* `SpinDirection`
* `Handedness`
* `ZoneMinZ`
* `ZoneMaxZ`
* `EnablePhaseLockedRemap`
* `EnableThroatDiagnostics`

### Preserve current strengths

* keep linked viewport preview rendering intact
* keep current overspace demo runnable
* keep additive implementation style
* avoid scene swap requirements for correctness

### Implement throat-aware traversal behavior

Traversal should use the current wormhole/portal shell but begin treating the crossing as a throat event rather than a naive teleport.

Minimum expected behavior:

* source mouth hit / crossing point is computed
* phase-locked equivalent coordinate is generated on destination mouth
* exit orientation faces outward from destination mouth center
* throat metadata is visible in diagnostics

### Diagnostics for Phase A

Required overlays:

* local XYZ axes
* zero phase marker
* spin direction arrow
* source boundary vector
* mapped destination vector preview
* compact throat HUD text

## Validation target

One portal pair in the overspace trophy-room demo with one linked child z-zone enclosure, deterministic artifact generation, and visible throat diagnostics.

---

# Phase B — Boundary-Layer Throat Behavior

## Objective

Blend `BoundaryLayerVolume` semantics with the wormhole mouth so the throat becomes a meaningful transition shell rather than only a remap event.

## Deliverable

A bounded, deterministic throat shell behavior that can be validated in the current harness and that visually strengthens the sense of crossing into a different region.

## Scope

### Integrate throat shell behavior

The wormhole mouth should optionally cooperate with a `BoundaryLayerVolume` or equivalent transition shell.

This shell can drive:

* crossing events
* directional bias
* scene-transform style transition semantics
* later optical / transport experimentation

### Keep the behavior additive

Do not replace the current validated portal traversal path immediately. Add a throat shell mode that can be enabled and tested in isolation.

### Inspector-facing controls

Possible Phase B controls:

* `EnableThroatShell`
* `ThroatShellMode`
* `ThroatBiasStrength`
* `ThroatCrossingPolicy`
* `ThroatShellThickness`
* `DrawThroatShellGlyphs`

### Validation expectations

Use the repo’s deterministic validation style:

* controlled scene
* known crossing direction
* visible shell behavior
* reproducible overlays and logs

## Validation target

A deterministic throat-shell validation scene and artifact set that proves the shell contributes visible, explainable transition behavior without breaking current traversal.

---

# Phase C — Research Bridge Toward Academic Wormhole Transport

## Objective

Create an architecture seam that could later support academic wormhole transport concepts without forcing a premature rewrite.

## Deliverable

A research-ready interface and naming structure that can eventually connect current demo logic with future chart-aware / metric-aware transport.

## Scope

### Prepare conceptual seams

The framework should be able to grow toward:

* throat-region transport evaluation
* chart transition concepts
* scene / region context switching for rays
* persistent transport state

### Do not overbuild yet

This phase does **not** require implementing a full Morris-Thorne metric integrator now. It only requires making sure that current code structure and naming do not obstruct that future.

### Suggested future-facing abstractions

Examples only, not all required now:

* `BuildPhaseLockedExitTransform(...)`
* `TryThroatTransition(...)`
* `ThroatTransitionResult`
* `IThroatMapping`
* `IThroatDiagnosticsSource`

## Validation target

A small research note or stub interface set that clearly marks how the current portal-throat demo could connect to later academic wormhole transport work.

## Godot adaptor requirement

The framework should expose the important Phase A and Phase B controls through the Godot inspector using the existing adaptor style in the repository.

This means:

* artist / experimenter visible properties
* no hidden magic required for the first demo
* clear defaults
* debug-friendly toggles
* serialized scene values that Codex can inspect and patch cleanly

Inspector exposure is important because it turns the wormhole throat from a code-only concept into a tunable experimental object.

## Test harness requirement

The phased architecture is not complete unless it is runnable through the repo’s existing validation / artifact style.

Minimum expectation for the next Codex thread:

* one overspace milestone scene path
* one deterministic runtime check
* one artifact generation path
* logs and images saved into the active WSL repo
* validation checklist for throat-related overlays and traversal behavior

## Recommended implementation order

1. Add throat metadata and inspector exposure to the portal object
2. Implement phase-locked throat remap as the first explicit throat behavior
3. Extend overlays to show throat mapping state
4. Validate one portal pair in the overspace trophy room
5. Add optional boundary-layer throat shell mode
6. Capture deterministic artifacts
7. Only then define the next seam toward deeper academic transport behavior

## Success definition for the next Codex thread

Success is **not** “full Einstein wormhole achieved.”

Success is:

* the current overspace demo now has an explicit throat framework
* throat controls are visible in the inspector
* one stable throat-aware portal pair runs in the active WSL repo
* overlays clearly communicate the throat mapping
* artifacts can be generated from the existing harness style
* the resulting code structure is coherent enough to carry forward into later research phases

## Codex thread instructions

When starting the new Codex thread, use this note as a stable context anchor and ask Codex to:

1. inspect the current wormhole / overspace architecture
2. identify the narrowest additive seam for introducing throat metadata and inspector controls
3. implement Phase A first
4. preserve current runtime stability in the active WSL repo
5. produce exact run / validation commands
6. avoid pretending Phase C physics has already been solved

## Suggested repo location

Place this file somewhere like:

* `Research/wormhole_throat_phased_architecture_note.md`
* or `Docs/research/wormhole_throat_phased_architecture_note.md`

Choose the location that best matches the repo’s current documentation layout.

## Closing principle

The throat framework should be treated as the bridge between:

* today’s linked-space portal demo
* tomorrow’s deterministic transition shell behavior
* and the longer academic path toward true throat-aware wormhole transport

That bridge is the next coherent upgrade.
