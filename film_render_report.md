# Film Renderer Audit + Perf Notes

## Step 1 - Repo Audit (verification)
- `test.tscn`: `FilmView` and `FilmOverlay2D` are both under `CanvasLayer` with `layer = -2`, and `FilmOverlay2D` is after `FilmView` in the scene tree.
- `FilmOverlay2D.cs`: overlay draws in screen space via `GetGlobalTransformWithCanvas().AffineInverse()` and does not use `ToLocal()`.
- `GrinFilmCamera.cs`: overlay redraws once per band via `SetData(...)->QueueRedraw()` and clears via `ClearOverlay()` when debug is off and film-owned.
- `RayBeamRenderer.cs`: debug overlay only draws internally when `DebugOverlayOwnedByFilm == false`.

## Changes (this round)
- Added per-frame timing stats with rolling averages, band counters, and per-call physics query counts.
- Added optional verbose logging to keep per-band prints off by default.
- Added safe, opt-in optimizations (field source cache, broadphase policy, tiny segment skip, early-out, collider name gating).
- Added debug overlay assertions for capacity/bounds consistency.
- Added perf preset helpers for quick A/B setups.

## Optimization toggles (default false)
1) **UseFieldSourceCache** + **FieldSourceRefreshIntervalFrames**  
   Caches `field_sources` snapshots across bands; refresh on transform/instance changes or interval.
2) **UseBroadphasePolicy** + **BroadphasePolicy** (`None`, `QuickRayOnly`, `OverlapOnly`, `Both`)  
   Picks one broadphase path (prevents double-broadphase unless explicitly set to `Both`).
3) **TinySegmentSkipLen**  
   Skips physics for segments shorter than this length.
4) **EarlyOutDistanceEps**  
   Early exit per-pixel loop when `NearestHitOnly` and best hit is already extremely near.
5) **NeedColliderNames**  
   Only resolves collider names when explicitly requested.

## Perf counters (per full frame)
- pass1_ms, pass2_physics_ms, pass2_shading_ms
- film_update_ms (texture update)
- overlay_build_ms, overlay_draw_enqueue_ms
- pixels in band, total segs, IntersectRay/IntersectShape/SubdividedRayHit counts

## Perf preset helpers
- `ApplyPerfPresetFastPreview()` enables the safe optimizations for fast preview.
- `ApplyPerfPresetQuality()` resets to baseline behavior.
