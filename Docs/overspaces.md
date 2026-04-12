# Overspace Demo Scope

## Purpose

Build a public-facing Godot prototype that demonstrates stable wormhole traversal, spatial remapping, and diagnostic overlays using a single `.tscn` scene organized by z-height zones rather than scene swapping.

The prototype should validate the core engine concept in a relatable environment first, then extend into a more symbolic overspace / trophy-room presentation.

## Core Architecture

### Single-scene overspace

* Keep all rooms / regions inside one Godot scene.
* Use **z-height zones** as the segregation method for different wormhole regions.
* Avoid scene-cache swapping or cross-scene teleport complexity.
* Each region exists as a spatially separated zone within the same world.

### Wormhole mapping model

* A wormhole is represented by a **boundary layer volume object** or **field source object** with an aligned local XYZ frame.
* Traversal is not a full scene change.
* On crossing the wormhole boundary surface, the camera/player is remapped to a **phase-angle-locked equivalent coordinate** in the linked wormhole sphere.
* The mapped destination preserves clocked position on the source sphere and updates facing so the user emerges oriented **away from the center** of the destination wormhole sphere.

### Coordinate / phase logic

Each wormhole sphere needs:

* local origin
* local XYZ axes
* zero phase-angle marker
* spin direction / handedness
* parent-child relationship metadata
* linked destination wormhole reference

Traversal should use:

* hit position on source boundary
* normalized direction from source center to hit point
* phase/clocking offset relative to source zero marker
* equivalent transformed direction on destination sphere
* destination position placed on or just beyond destination boundary
* camera forward vector remapped so traversal exits outward from destination center

## Diagnostic Visualization Requirements

The viewport and overlays must show wormhole mapping state clearly.

### Required overlays

* wormhole local XYZ axes
* central axis / field axis indicator
* zero phase-angle marker
* spin direction arrow
* vector from sphere center to current sampled hit point
* destination-linked vector preview when aimed at / near a portal
* active parent-child relationship label or ID
* optional text HUD showing phase angle, source wormhole ID, destination wormhole ID, and active zone

### Visual design goal

The diagnostics should make the wormhole feel like a **clocked qubit / oriented sphere mapping system**, not just an arbitrary teleport trigger.

## Deliverable 1: Trophy Room Prototype

### Variant plan

Test **two versions** of the trophy-room wormhole presentation:

* **Variant A:** child wormhole region visually bounded by a cube-like enclosure
* **Variant B:** child wormhole region visually bounded by a sphere-like enclosure

These are not final art choices. They are comparative prototypes to test traversal feel, diagnostics, and mapping legibility.

The enclosure can later receive textures and richer materials, but early versions should prioritize normals, orientation, and clean debug readability.

### Child-region geometry strategy

Each child wormhole region can remain minimal:

* no extra scene clutter required initially
* just the rectangular room / enclosure itself
* build the room from thin-thickness rectangular wall objects assembled like a gingerbread-house shell to avoid confusing back-face visibility issues
* keep collision and visual surfaces explicit and simple for debugging

This keeps early traversal tests focused on remap stability, clocking, and camera orientation rather than prop complexity.

### Codex integration step

After the scope is committed into the repo, run a first-round Codex prompt to inspect and assess:

* how this architecture fits current code
* what existing wormhole scene and render harness components can be reused
* where the z-zone strategy is already compatible
* what additive implementation path is least disruptive

Codex should be asked to prefer reuse of existing wormhole test harnesses and instrumentation wherever possible rather than inventing a fresh architecture.

### Concept

A simple rectangular room with wormhole sphere/orb portals displayed like objects on walls or shelves.

### Layout

* one central room
* four wormhole portals, one associated with each wall / quadrant
* each portal links to a distinct z-zone region within the same scene

### Behavior

* user walks around in first person
* portal surfaces show linked viewports / destination previews if feasible
* on entering a portal boundary, camera teleports/remaps to the linked clocked coordinate in the destination zone
* user exits facing outward from the destination wormhole center

### Goal

Prove:

* stable traversal
* clear clocked coordinate remapping
* useful diagnostic overlays
* single-scene z-zone architecture viability

## Deliverable 2: Mini Solar-System Overspace

### Concept

A scaled symbolic overspace where different z-zones represent distinct planetary or world regions.

### Layout

* overspace acts as parent environment
* multiple child wormhole zones represent different worlds / rooms / planets
* each world is greatly reduced in scale for intuitive exploration
* visual style can resemble a trophy room of world-orbs / wormhole spheres

### Goal

Extend the same architecture from room-scale traversal to multi-region symbolic overspace.

## Parent-Child Relationship Model

Parent-child relationships should be defined explicitly because they will matter later for GUI and system organization.

Each wormhole should support:

* `wormhole_id`
* `parent_region_id`
* `child_region_id`
* `linked_wormhole_id`
* `zero_phase_angle`
* `spin_direction`
* `zone_min_z`
* `zone_max_z`

This metadata should be available for diagnostics and future UI controls.

## Scope Boundaries

### In scope

* single-scene architecture
* z-zone segregation
* wormhole sphere traversal
* clocked coordinate remapping
* first-person camera transport
* diagnostic overlays
* simple room-scale prototype
* second symbolic overspace prototype

### Out of scope for now

* full planet-scale observation simulator
* full physically accurate GRIN transport everywhere
* cross-scene streaming or scene cache swaps
* advanced gameplay systems
* production-polished art

## Technical Intent

This project exists to justify and exercise the three core roles:

### Architect

Defines wormhole ontology, mapping rules, parent-child structure, and long-term UI implications.

### Builder

Implements z-zone architecture, traversal remap, portal rendering, and overlay instrumentation.

### Translator

Turns the system into a public-facing, intuitive, fun interface with clear diagnostics and explainable behavior.

## Milestone Sequence

1. Define wormhole metadata and mapping math
2. Implement one linked wormhole pair in a single room
3. Add diagnostic overlays for axes, zero marker, spin, and phase vector
4. Add stable first-person traversal and outward-facing remap
5. Expand to four portal spheres in the trophy room
6. Create mini solar-system overspace version using z-zones and parent-child links

## Success Criteria

* traversal feels stable and intentional
* overlays explain the mapping visually
* all linked regions live in one `.tscn` scene
* camera remap uses clocked equivalent coordinates rather than naive teleport only
* architecture is clear enough for Codex and future contributors to extend

## Codex Handoff Notes

When implementing, preserve the existing z-zone strategy already established in the repo. Avoid introducing unnecessary scene-switching abstractions unless they are strictly additive and do not conflict with the single-scene overspace model.

Favor:

* additive instrumentation
* explicit metadata
* small verifiable milestones
* debug-first visual confirmation

This demo is the first public-facing proof that the engine can represent nontrivial spatial remapping in a way users can see, navigate, and reason about.
