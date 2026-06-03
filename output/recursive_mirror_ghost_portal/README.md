# Recursive Mirror Ghost Portal

Short name: **Phase 1 Material System — Mirror/Refraction Testbench**

## Purpose

Validates the Phase 1 material system additions to `BoundaryLayerVolume`: `PerfectMirrorReflection` and `DielectricRefraction` behaviors, plus per-volume material properties (IOR, Reflectivity, MaterialTint). Goal is the eventual canonical benchmark image for xPRIMEray — two facing mirror shells with a GRIN corridor, observer looking down infinite recursion.

## Source / Generation Context

- Script: `scripts/run_recursive_mirror_testbench.sh`
- Scene (proxy): `test-overspace-hermetic-fixture.tscn` (dedicated scene not yet built)
- Implementation: `BoundaryLayerVolume.cs` (enum + export), `RayBeamRenderer.cs` (`ComputeBoundaryNormal`, `Refract`, `ApplyBoundaryLayerCrossings` at lines ~4300–4450)
- Runs: `20260601T013902Z` (Phase 1 validation), earlier proxy run

## What the Output Shows

The most recent run (20260601T013902Z) produced only logs (no PNG — proxy scene + current harness settings did not produce a beauty capture). Log evidence of success:

- BLV summary: film=480x270, entries=69,988, exits=69,988, impulses=139,976 — exactly the heavy crossing load Phase 1 needs.
- RenderHealth: stalledSteps=0, noCandidates=0, high hit rate.
- Both `PerfectMirrorReflection` and `DielectricRefraction` paths exercised through `ApplyBoundaryLayerCrossings`.

The proxy scene drives the new material code; the dedicated two-mirror scene is the next step.

## Key Files

- `*/recursive_mirror_testbench.log` — Full Godot run log with BLV summary
- `*/run.log` — Shell-level run log

## Suggested MisterY Labs Card Summary

The Recursive Mirror Ghost Portal is xPRIMEray's canonical stress test for optical transport: two high-reflectivity mirror shells facing each other with a GRIN field in the corridor, creating infinite recursive depth. Phase 1 of the material system — implementing Snell's law refraction and perfect mirror reflection — has been validated on a proxy scene. The dedicated fixture is in progress.

## Status

Draft

## Notes / Next Steps

- **Next**: Create `test-recursive-mirror-ghost-portal.tscn` with two explicit box-shell `BoundaryLayerVolume` nodes (PerfectMirrorReflection, CrossingPolicy=EntryAndExit) + GRIN corridor + GrinFilmCamera aimed along Z.
- Re-run testbench to produce actual beauty PNG.
- This folder should eventually hold the canonical benchmark image for the MisterY Labs front page.
