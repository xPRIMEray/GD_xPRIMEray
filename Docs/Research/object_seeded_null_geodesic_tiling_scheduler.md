# Object-Seeded Null Geodesic Tiling Scheduler

## Summary

The object-seeded scheduler is a research and diagnostics layer for reducing row-order resonance without hiding artifacts. It uses scene anchors, lightweight camera-to-anchor probes, projected screen-space tiles, and deterministic tile ordering to schedule attention around transport-interest regions before fallback full-frame coverage.

This is not an image-space smoothing pass. Probe results may influence traversal order and diagnostics only. They must not mutate selected hits, shading inputs, candidate lists, resolver decisions, or nearest-hit behavior.

```text
v1:
anchors -> probes -> projected tiles -> deterministic schedule

v2/v3:
scene fingerprint -> object observers -> probe cache -> precision profile -> transport risk field -> scheduler
```

## V1 Architecture

Candidate objects come from data already available to the render loop through `FrameSnapshotBus.CurrentSnapshot`: geometry bounds and domain/field bounds. Each candidate gets deterministic anchors:

- centroid
- AABB corners
- principal AABB axis samples
- domain boundary samples, represented conservatively by field/domain bounds in v1
- portal throat centers, reserved for the portal data path once portal observers are exposed to the scheduler

For each anchor, the scheduler runs an isolated lightweight characteristic probe from the active camera toward the anchor. In the current v1 implementation this probe is deliberately conservative: it estimates path length, projection, source object/domain identity, boundary event hints, and screen tile influence. It does not call into final render hit selection and does not feed final color or normal decisions.

Projected probe results build a `TransportRiskField` over horizontal subtiles. Seeded tiles are sorted first using risk/confidence and a fixed deterministic seed. Unseeded and stale low-confidence regions are appended as fallback tiles, preserving complete full-frame coverage.

## Toward Scene Transport Fingerprinting

The long-term architecture turns object seeding into object-memory seeding:

- `SceneTransportFingerprint` stores the frame-level observer set and scene version stamp.
- `ObjectTransportObserver` stores stable object id, bounds, centroid, anchor samples, last probe results, required step precision estimate, decision-risk score, confidence score, screen-space influence tiles, cache version stamp, and invalidation reason.
- `NullGeodesicProbeCache` reuses probe results when camera, scene, and stepper state are compatible.
- `StepperPrecisionProfile` compares coarse, medium, and fine probes against a reference-precision baseline.
- `TransportRiskField` projects observer risk/confidence into tiles.
- `CacheInvalidationPolicy` decides whether observer data is reused, decayed, refreshed, or invalidated.

The reference-precision baseline is the best local numerical reference available to the probe system; it is not treated as absolute truth.

## Decision Risk

The precision profile estimates the coarsest local step precision whose decision risk remains below epsilon relative to the reference-precision baseline.

```text
DecisionRisk =
  collider mismatch
+ domain mismatch
+ portal crossing mismatch
+ hit distance error
+ normal angle error
+ path deviation
+ resolver changes
+ boundary event mismatch
```

In v1 this precision oracle is diagnostic only. It must not drive adaptive stepping or final shading.

## Confidence And Cache Invalidation

Observer confidence decays when cached data is reused:

- confidence decays with camera delta
- confidence decays with object, portal, or domain version uncertainty
- high-risk observers refresh first under a probe budget
- stale low-confidence observers fall back to normal tile coverage

Invalidation reasons are explicit:

- `camera_moved_slightly`: reproject cached anchors, decay confidence, refresh high-risk zones first
- `camera_moved_significantly`: re-run object probes
- `scene_fingerprint_changed`: invalidate incompatible observers
- `stepper_settings_changed`: recompute precision profile
- `probe_budget_exhausted`: keep fallback coverage for unrefreshed observers
- `cache_reused`: cached observer/probe state remained compatible

## Proposed C# Modules

Implemented or reserved module names:

- `ObjectSeededTileScheduler`
- `TransportAnchor`
- `TransportAnchorExtractor`
- `CharacteristicProbeRunner`
- `SceneTransportFingerprint`
- `ObjectTransportObserver`
- `NullGeodesicProbeCache`
- `StepperPrecisionProfile`
- `TransportRiskField`
- `CacheInvalidationPolicy`

The current implementation lives under `RendererCore.Scheduling` and is gated by `GrinFilmCamera.EnableObjectSeededTileScheduler` or the CLI flag `--object-seeded-tile-scheduler=1`.

## Diagnostics

Tile metrics summaries include object-seeded diagnostics when the scheduler is active:

- observer count
- anchor count
- probe count
- seeded tile count
- fallback tile count
- stale observer fallback count
- probe cache hits/misses
- max decision risk
- max observer confidence
- per-observer records for stable id, anchor samples, last probe count, required precision label, risk, confidence, cache version stamp, and invalidation reason

These diagnostics explain scheduling attention. They do not certify final physical correctness.

## First Fixture And DOE Tests

Compare traversal modes:

- scanline baseline
- shuffled row
- normal tile
- object-seeded tile
- later: object-memory-seeded tile using cached fingerprint data

Initial fixture:

- existing domain/resolver stress scene
- `320x180`
- `5` frames for smoke, `90` frames for real DOE
- step lengths `0.015`, `0.0125`, `0.00625`

Metrics:

- band pixels
- band percent
- horizontal band score
- row band coverage percent
- max contiguous horizontal band width
- band pixels by row modulo stride
- beauty hash determinism under fixed seed
- changed pixels versus scanline baseline
- seeded versus fallback tile coverage
- observer risk distribution
- observer confidence distribution and decay reasons
- stale observer fallback count
- probe cache hit rate
- required precision profile by object/domain/portal

Success criteria:

- horizontal band score drops versus scanline if scheduling resonance is the amplifier
- artifacts become localized instead of row-global
- fixed seed produces deterministic OFF render hashes
- true geometry edges remain sharp
- stale low-confidence observers do not influence seeded scheduling
- cached observers never affect final hit or shading decisions in v1/v2
- precision oracle outputs nontrivial local precision estimates without mutating render state

## Guardrails

- Do not blur the beauty image.
- Do not hide artifacts with post-process smoothing.
- Do not treat the reference-precision baseline as absolute truth.
- Do not use probe results to choose final color, hit, normal, collider, domain, or resolver state.
- Preserve fallback full-frame coverage.
- Preserve real geometric discontinuities.
- Keep probe budgets bounded and report budget exhaustion.
- Keep claims tied to DOE outputs and diagnostics.
