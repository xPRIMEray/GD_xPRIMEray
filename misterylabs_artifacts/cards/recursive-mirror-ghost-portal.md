# Recursive Mirror Ghost Portal — Phase 1: Material System

**Hook:** Two mirrors facing each other, a GRIN field between them, an observer looking down the infinite recursion. The canonical stress test for optical transport. Phase 1 complete; benchmark image incoming.

## Scientific Context

The recursive mirror ghost portal is xPRIMEray's planned flagship benchmark: two high-reflectivity mirror shells facing each other with a GRIN corridor between them. Rays entering the corridor bounce back and forth while being simultaneously bent by the field — producing visible recursive depth that depends on the transport engine's ability to handle both discrete reflection events and continuous curvature integration without energy loss or banding.

Phase 1 implemented the material system required to attempt this render:
- `PerfectMirrorReflection` — discrete reflection at boundary crossing, normal computed from box face for crisp edges or sphere surface for smooth curvature
- `DielectricRefraction` — Snell's law refraction at interfaces, with total internal reflection fallback
- Per-volume properties: IOR, Reflectivity, MaterialTint

## Observation

Phase 1 validated on the overspace hermetic fixture proxy scene (20260601T013902Z):
- BoundaryLayerVolume summary: film=480×270, entries=69,988, exits=69,988, impulses=139,976 — 140k crossing events, zero missing exits
- RenderHealth: stalledSteps=0, noCandidates=0 — integrator remained stable under heavy boundary load
- Both `PerfectMirrorReflection` and `DielectricRefraction` code paths confirmed hot in `ApplyBoundaryLayerCrossings`

No beauty PNG exists yet — the proxy scene does not produce the mirror recursion. The dedicated fixture scene is the next step.

## Why It Matters

Mirror recursion is the classical benchmark for optical transport correctness: a renderer that can't handle recursive reflections stably will show banding, missing depths, or energy accumulation after a few bounces. Phase 1 proved the crossing machinery is numerically stable at 140k events/frame. Phase 2 will produce the image.

## Next Step

Create `test-recursive-mirror-ghost-portal.tscn` with two box-shell `BoundaryLayerVolume` nodes (PerfectMirrorReflection, CrossingPolicy=EntryAndExit, Reflectivity=1.0) + AtomicEigenmode GRIN field + GrinFilmCamera aimed along the Z axis. Re-run the testbench and promote the resulting PNG as the project's front-page benchmark image.

---

*Source:* `output/recursive_mirror_ghost_portal/`  
*Key image:* None yet — benchmark image pending Phase 2 scene creation  
*Tier:* 3 — Draft, high priority when complete
