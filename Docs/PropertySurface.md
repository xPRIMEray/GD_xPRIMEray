# Property Surface

This document summarizes the inspector-facing knobs for the two-pass film renderer. Pass 1 builds curved-ray segments (sampling/curvature); Pass 2 performs physics collision checks and shades the film. The same film image then feeds optional overlays.

## A) Sampling / Resolution knobs

- FilmResolutionScale: Scales the render target resolution; lower values boost speed.
- PixelStride: Traces every Nth pixel and fills blocks; larger values reduce detail but improve performance.
- RowsPerFrame: Controls band height per frame; larger values cost more per frame.

## B) Pass 1 (segment building) knobs

- RayBeamRenderer.StepsPerRay: Integration step count; higher = smoother curves, slower.
- RayBeamRenderer.StepLength / MinStepLength / MaxStepLength: Step size controls; smaller steps increase detail.
- RayBeamRenderer.StepAdaptGain: Adaptive step sizing strength.
- RayBeamRenderer.CollisionEveryNSteps: Segment density (more segments means more pass-2 tests).
- FieldStrength / BendScale / Beta / Gamma: Curvature controls that affect segment path.
- Segment limits: Any caps on segments per ray (renderer-side) will clamp pass-1 output.

## C) Pass 2 Physics knobs

- Collision policy: UseBroadphaseQuickRay / UseBroadphaseOverlap / BroadphasePolicy.
- Subdivision: CollisionRaySubdivideThreshold / MaxCollisionSubsteps / UseAdaptiveSubsteps.
- Early-out: NearestHitOnly / EarlyOutDistanceEps / TinySegmentSkipLen.
- New stride controls (skip some collision checks):
  - UsePass2CollisionStride: Master toggle (off = identical behavior).
  - Pass2CollisionStrideNear: Stride close to the camera.
  - Pass2CollisionStrideFar: Stride at far distances.
  - Pass2CollisionStrideFarStartT: Depth t where far stride begins.

Gotchas
- Pass2CollisionStride reduces query count but can miss tiny geometry unless Near = 1.
- Always testing the last segment prevents missing far hits.
- PixelStride affects fill; large strides reduce detail but boost speed.

## D) Shading knobs

- ShadingMode: DepthHeatmap / NormalRGB / NdotV.
- FlipNormalToCamera: Flips hit normals for shading consistency.
- SkyColor / FilmOpacity: Visual output only; does not affect physics.

## E) Overlay knobs

- Rays: DrawRays, RayWidth, RayColor, HitRayColor.
- World normals: DrawHitNormals, WorldNormalColor, WorldNormalWidth, WorldNormalLen.
- Film normals: DrawFilmGradientNormals, FilmNormalColor, FilmNormalWidth, FilmGradientScale.
- DebugEveryNPixels / DebugMaxFilmRays: Thins overlay sampling and caps per-band cost.

## F) FieldSource3D authoring surface

- Canonical controls for new scenes:
- `Field Model (Canonical)`: `MetricModel`, `RInner`, `ROuter`, `Amp`, `ModeFlags`, `Softening`, `Sigma`
- `Shape`: `ShapeType`, `BoxExtents`
- `Curve`: `CurveType`, `CurveA`, `CurveB`, `CurveC`
- Legacy controls are grouped under `Legacy (Deprecated)` for compatibility only.
- Effective runtime params are resolved by `ResolveEffectiveParams(...)`:
- Canonical values are used when set.
- If canonical looks unset and legacy has meaningful values, legacy is mapped once to canonical.
- If both are set and differ, canonical wins and legacy is ignored (warning logged).
- Inspector equation previews:
- `EffectiveEquationCore`: core snapshot equation (`FieldSystem.AccelAt` form).
- `EffectiveEquationIntegrated`: integrated ray equation (`ComputeAccelerationAtPointSnap` form).

## Presets (documentation only)

Preview (fastest)
- FilmResolutionScale: 0.5
- PixelStride: 2
- RowsPerFrame: 16
- UseBroadphasePolicy: QuickRayOnly
- UsePass2CollisionStride: true
- Pass2CollisionStrideNear: 1
- Pass2CollisionStrideFar: 8

Walk (gameplay stable)
- FilmResolutionScale: 0.5
- PixelStride: 2
- RowsPerFrame: 16
- UseBroadphasePolicy: QuickRayOnly
- UsePass2CollisionStride: true
- Pass2CollisionStrideNear: 1
- Pass2CollisionStrideFar: 4

Quality (cinematic)
- FilmResolutionScale: 1.0
- PixelStride: 1
- RowsPerFrame: 4
- UseBroadphasePolicy: Both
- UsePass2CollisionStride: false
