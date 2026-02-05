# Architecture Review: GD_xPRIMEray Curved-Ray GRIN Renderer

> **Reviewer focus:** Build a clear mental model before proposing changes.
> Experimental GRIN curved-ray engine — prioritizing correctness and extensibility.

---

## 1. Full Control Flow — Camera → Integration → Intersection → Shading

```
_Process(delta)                                       GrinFilmCamera.cs:1400
  └─ RenderStep()                                     GrinFilmCamera.cs:1410
       │
       ├─ ResolveEffectiveConfig(out cfg)              GrinFilmCamera.cs:5854
       │    Snapshots all RayBeamRenderer + GrinFilmCamera settings
       │    into a frozen EffectiveConfig struct for the entire step.
       │
       ├─ EnsureFilmImageSize(cfg)                     Allocate/resize Image + ImageTexture
       │
       ├─── BAND LOOP (progressive rows) ─────────────────────────────
       │    _rowCursor advances RowsPerFrame rows per call.
       │    Budget watchdog (RenderStepMaxMs) can abort mid-band.
       │
       │  ┌──────────────────────────────────────────────────────────┐
       │  │  PASS 1 — Ray Integration (Parallel.For)                │
       │  │  GrinFilmCamera.cs:2453                                  │
       │  │                                                          │
       │  │  For each pixel (pi) in band:                            │
       │  │    1. Compute NDC (u,v) from pixel coords     :2490      │
       │  │    2. Build camera ray:                                  │
       │  │       dirCam = (u*tan*aspect, v*tan, -1).Normalized()    │
       │  │       dirWorld = basis * dirCam                :2494-2502 │
       │  │       bendDir = basis.X                                  │
       │  │    3. Call _rbr.BuildRaySegmentsCamera_Pass1() :2507     │
       │  │       ┌─ RayBeamRenderer.cs:1938 ───────────────────┐    │
       │  │       │  for s in 0..StepsPerRay:               :2022│   │
       │  │       │    a. Field eval (grid→snap→radial)  :2040-57│   │
       │  │       │    b. Clamp accel (50 cap)           :2060-63│   │
       │  │       │    c. Adaptive step sizing            :2065-78│   │
       │  │       │    d. Screen-space cadence            :2084-93│   │
       │  │       │    e. v = normalize(v + a*step)          :2095│   │
       │  │       │       next = p + v*step                  :2096│   │
       │  │       │    f. Emit RaySeg every `ce` steps   :2122-46│   │
       │  │       │    g. Optional pass-1 probe raycast  :2148-94│   │
       │  │       └──────────────────────────────────────────────┘    │
       │  │    4. Store segCountPerPixel, pass1Hit* per pixel        │
       │  └──────────────────────────────────────────────────────────┘
       │
       │  ┌──────────────────────────────────────────────────────────┐
       │  │  PASS 2 — Collision + Shading (Sequential, main thread) │
       │  │  GrinFilmCamera.cs:3333                                  │
       │  │                                                          │
       │  │  For each pixel in band (stride-aligned):                │
       │  │    for pass in 0..1:                             :3447   │
       │  │      for si in segments:                         :3466   │
       │  │        A. Insight-plane filter                   :3535   │
       │  │        B. Broadphase quick-ray (cached)          :3602   │
       │  │        C. Broadphase overlap (IntersectShape)    :3562   │
       │  │        D. Soft-gate scoring decision             :3494ff │
       │  │        E. SubdividedRayHit (RBR.cs:1226)                │
       │  │           Subdivides curved segment into sub-rays        │
       │  │           and raycasts each                              │
       │  │        F. Track nearest hit (bestHp, bestHn)             │
       │  │    Shade:                                        :4383   │
       │  │      DepthHeatmap | NormalRGB | NdotV | TwoSidedNdotV   │
       │  │    FillPixelBlock → _img.SetPixel()              :4437   │
       │  └──────────────────────────────────────────────────────────┘
       │
       ├─ _tex.Update(_img)                           Upload to GPU
       ├─ UpdateDebugOverlayFromFilm(...)             Optional debug overlay
       └─ Advance _rowCursor; wrap at film height
```

The system is a **two-pass progressive renderer**. Pass 1 is embarrassingly parallel
(one thread per pixel), while Pass 2 is sequential on the main thread because it calls
Godot physics APIs (`IntersectRay`, `IntersectShape`) which require main-thread access.

---

## 2. Key Classes and Responsibilities

| Class | File | Lines | Responsibility |
|-------|------|-------|---------------|
| **GrinFilmCamera** | `GrinFilmCamera.cs` | ~6780 | **Orchestrator.** Owns the film buffer (`Image`/`ImageTexture`), drives the two-pass render loop, resolves effective configuration from presets/quality modes, manages budgets/watchdogs, performs Pass-2 collision on the main thread, shades pixels, uploads to GPU. |
| **RayBeamRenderer** | `RayBeamRenderer.cs` | ~2713 | **Ray physics engine.** Owns all curved-ray integration logic (field evaluation, adaptive stepping, segment emission), field source snapshotting, collision subdivision (`SubdividedRayHit`), and the standalone 3D debug visualization (MultiMesh billboards). Also serves as a **config container** — most ray-march and collision parameters are `[Export]` properties on this node. |
| **FieldSource3D** | `FieldSource3D.cs` | 373 | **GRIN field definition.** A `Node3D` that describes one refractive index source in the scene. Supports 4 profiles (Power, InversePower, Gaussian, Shell) with per-source overrides for gamma/beta. Also handles its own in-game debug visualization via `ImmediateMesh`. |
| **FieldGrid3D** | `FieldGrid3D.cs` | 115 | **Field acceleration cache.** A plain class (not a Node) that pre-computes a dense 3D grid of acceleration vectors from all `FieldSourceSnap`s, then provides O(1) trilinear-interpolated lookups via `TrySample()`. |
| **FilmOverlay2D** | `FilmOverlay2D.cs` | 299 | **2D debug overlay.** A `Control` node that draws projected ray polylines, hit normals, and film-gradient normals on screen. Driven by data pushed from `GrinFilmCamera`. |
| **PerfScope / FramePerf** | `PerfScope.cs` | ~161 | **Timing infrastructure.** RAII-style `ref struct` using `Stopwatch.GetTimestamp()` for zero-alloc stage timing. `FramePerf` accumulates 30+ counters per frame. |
| **PerfStats** | `PerfStats.cs` | ~400 | **Rolling statistics.** Circular buffer of `PerfFrameReport` structs for sliding-window averages and diagnostic printing. |
| **CurvedCamera** | `CurvedCamera.cs` | 23 | **Minimal camera extension.** Provides `GetCurvedRay(ndc)` with a simple analytic power-law bend — appears to be an early prototype superseded by the full integration in RayBeamRenderer. |
| **RayViz** | `RayViz.cs` | 167 | **Standalone 3D ray debug.** Samples 9 screen points, draws analytic curved-ray polylines in 3D. Change-detected rebuild. |

### Ownership Diagram

```
GrinFilmCamera (Node)
  ├── references → RayBeamRenderer (Node3D)  [via RayBeamRendererPath]
  │                  └── owns MultiMesh billboard visualization
  │                  └── reads FieldSource3D nodes from scene tree
  │                  └── optionally uses FieldGrid3D (plain object)
  ├── references → Camera3D                  [viewport active camera]
  ├── references → FilmOverlay2D (Control)   [via FilmOverlayPath]
  ├── owns → Image _img / ImageTexture _tex  [film buffer]
  └── owns → CanvasLayer + TextureRect       [display overlay]
```

---

## 3. How Curved-Ray Logic is Injected into the Pipeline

The curved-ray behavior is **not** a modification of Godot's built-in rendering. Instead,
it is an entirely **custom software renderer** running alongside Godot's rasterizer.

### 3a. Integration replaces straight-line raycasting

In `BuildRaySegmentsCamera_Pass1` (`RayBeamRenderer.cs:2022-2198`), instead of a single
`origin + t*dir` parametric ray, the engine performs a **symplectic Euler integration loop**:

```csharp
v = SafeNormalized(v + a * step, v);   // velocity kick
next = p + v * step;                    // position drift
```

The acceleration `a` comes from evaluating the GRIN refractive-index field at `p`. This
turns each ray into a piecewise-linear **curved polyline** stored as `RaySeg[]`.

### 3b. Field evaluation hierarchy

At each integration step (`RayBeamRenderer.cs:2039-2057`), acceleration is resolved
through a three-tier fallback:

1. **FieldGrid3D cache** (trilinear interpolation, O(1)) — if the grid was built
   and the point is in-bounds
2. **FieldSourceSnap array** (`ComputeAccelerationAtPointSnap`, `RayBeamRenderer.cs:2246`)
   — iterates all snapped sources, applies per-source profile
3. **Analytic radial field** — single-source `r^gamma` fallback centered on `FieldCenter`

### 3c. Interaction with Godot's physics

Curved segments are tested against the Godot physics world via direct
`PhysicsDirectSpaceState3D` calls:

- **Pass-1 probes** (`IntersectRay` on sampled segments, `RayBeamRenderer.cs:2163`)
- **Pass-2 broadphase** (`IntersectRay` for quick-ray, `IntersectShape` for overlap,
  `GrinFilmCamera.cs:3602-3600`)
- **Pass-2 subdivision** (`SubdividedRayHit` → N sub-raycasts per segment,
  `RayBeamRenderer.cs:1226`)

The renderer never touches Godot's `RenderingServer`. It writes directly to a CPU-side
`Image`, uploads it to an `ImageTexture`, and composites it via a `TextureRect` overlay
on a `CanvasLayer`.

### 3d. Analytic fallback mode

When `UseIntegratedField = false` (`RayBeamRenderer.cs:2101-2111`), the engine skips
numerical integration entirely and uses a closed-form curve:

```csharp
float bend = beta * Pow(t, gamma) * bendScale;
next = origin + dir * t + bendDir * bend;
```

This is a power-law displacement in the camera's X-axis direction — a fast approximation
for previewing.

---

## 4. Hot-Path Narrative

What happens in the **tightest per-ray loop** — the body of
`BuildRaySegmentsCamera_Pass1` called once per sampled pixel, potentially thousands
of times per `RenderStep`.

### Per-step (innermost loop, `RayBeamRenderer.cs:2022`)

**Step 1: Field evaluation** (~2040-2057)
The engine checks `fieldGrid.TrySample(p, out a)`. If the grid hits, this is 8 array
lookups + 7 lerps (trilinear). If it misses (or no grid), it falls through to
`ComputeAccelerationAtPointSnap` which loops over `FieldSourceSnap[]` — for each source:
one `Vector3` subtraction, one `Length()` (sqrt), softening sqrt, radius gating, one
`Pow()` call, and profile-dependent math (Gaussian adds `Exp()`).

**Step 2: Acceleration clamping** (~2060-2063)
One `Length()` call, one finite check, conditional scale-down if >50.

**Step 3: Adaptive step sizing** (~2065-2078)
Division `stepLength / (1 + aLen * gain)`, clamp. If low-curvature boost is enabled:
decompose `a` into perpendicular component (1 dot, 1 subtract, 1 length), conditional
multiply.

**Step 4: Screen-space cadence** (~2084-2093)
Only when enabled: `PerpAccelLen()` (recomputes perpendicular accel), camera distance
(`Length()`), then `ComputeCeFromScreenError` (sqrt, division, floor, clamp). Adjusts
how often segments are emitted.

**Step 5: Position/velocity update** (~2095-2099)
`v + a * step` → normalize (one `Length()`, one divide) → `p + v * step`. One traveled
accumulation.

**Step 6: Segment emission** (~2122-2146)
Every `ce` steps: optional insight-plane check (dot product + compare), then write
`RaySeg{A, B, TraveledB}` to pre-allocated array. No allocation.

**Step 7: Optional pass-1 probe** (~2148-2194)
Every N segments or travel distance: `space.IntersectRay()` — a Godot physics call that
descends into the physics BVH. On hit: dictionary unboxing for position/normal/collider_id.
Nearest-hit tracking.

### Iteration cost summary

For a typical configuration (`StepsPerRay=64`, 1 field source, no grid):
- 64x `ComputeAccelerationAtPointSnap` (each: 1 sqrt, 1 Pow, scalar math)
- 64x normalize + position update
- ~64/ce segment emissions (ce = 1-4)
- ~1-4 `IntersectRay` calls (pass-1 probing)

The dominant cost per step is the field evaluation (`Pow` + `sqrt`), followed by
normalization.

---

## 5. Top 5 Performance Hotspots

**H1. `ComputeAccelerationAtPointSnap` — per-step per-ray field evaluation**
(`RayBeamRenderer.cs:2246-2322`)

This is the innermost computation. Each call does `Pow(r, gamma)` per source —
`Mathf.Pow` is typically 50-100ns. With N sources x 64 steps x thousands of pixels,
this dominates. The `FieldGrid3D` cache mitigates this, but grid misses fall through
to the full evaluation. **The `Pow` call is the single most expensive math operation
in the system.**

**H2. Pass-2 `SubdividedRayHit` — Godot physics calls on main thread**
(`RayBeamRenderer.cs:1226-1286`)

Each subdivided segment generates up to `MaxCollisionSubsteps` (default 16) individual
`IntersectRay` calls. These go through Godot's physics BVH and are not parallelizable
(main-thread only). The soft-gate mechanism exists precisely to budget these calls, but
they remain the dominant wall-clock cost of Pass-2.

**H3. `Godot.Collections.Dictionary` unboxing in physics results**
(scattered across both passes)

Every `IntersectRay` returns a `Godot.Collections.Dictionary`. Extracting `"position"`,
`"normal"`, `"collider_id"` involves string key lookups and `(Vector3)` unboxing from
`Variant`. This is not cache-friendly and generates GC pressure. See
`RayBeamRenderer.cs:2168-2181`.

**H4. Pass-1 `Parallel.For` physics contention**
(`GrinFilmCamera.cs:2457-2469`)

The `pass1DoHitTest` path calls `space.IntersectRay` from worker threads. While
`PhysicsDirectSpaceState3D` read operations are thread-safe in Godot 4, they contend on
the physics server lock.

**H5. `FillPixelBlock` per-pixel `SetPixel` calls**
(`GrinFilmCamera.cs:4437`)

For stride > 1, each sampled pixel fills a stride x stride block by calling
`_img.SetPixel(px, py, col)` in a loop. `Image.SetPixel` validates bounds on every call.
At stride=4, that is 16 calls per sampled pixel. Direct `byte[]` writes would be faster.

---

## 6. Top 5 Architecture Risks

**R1. GrinFilmCamera is a ~6800-line god class**

`GrinFilmCamera.cs` owns configuration resolution, band scheduling, Pass-1 dispatch,
Pass-2 collision, shading, film management, debug overlay coordination, depth
auto-ranging, broadphase policy, soft-gate scoring, performance logging, and preset
management. The `RenderStep()` method alone spans lines 1410-4533 (~3100 lines).

**R2. Cross-class configuration coupling**

Configuration lives on `RayBeamRenderer` as `[Export]` properties, is read via
`GetSharedSnapshot()`, then merged into `EffectiveConfig` by `ResolveEffectiveConfig`.
Some settings are mirrored back to GrinFilmCamera exports for inspector display. This
creates a bidirectional dependency where changing a parameter requires understanding both
classes and the snapshot/mirror machinery.

**R3. Pass-2 blocks Godot's main thread**

All subdivision raycasts and overlap queries happen sequentially on the main thread
during `_Process`. The budget system provides a soft ceiling, but if the budget is
exceeded mid-band, the entire band's collision work is lost and must be re-done.

**R4. No separation between integration and collision concerns**

`BuildRaySegmentsCamera_Pass1` mixes ray integration (position/velocity update, field
evaluation) with pass-1 hit probing (physics raycasts). The method has 20+ `out`
parameters. This makes it difficult to test integration independently from collision,
or to substitute a different integration method (e.g., RK4).

**R5. Implicit contracts on thread safety**

Pass-1 writes to shared arrays (`_segBuf`, `_segCountPerPixel`, etc.) using
`pi`-indexed non-overlapping regions — but this is enforced only by pixel index
partitioning, not by any type-system or runtime guard. A future change to the pixel
indexing scheme could silently introduce data races.

---

## 7. Areas Where Intent is Unclear or Underspecified

**U1. Two-pass collision loop** (`GrinFilmCamera.cs:3447`)

```csharp
for (int pass = 0; pass < 2; pass++)
```

Pass 0 uses configured stride; pass 1 forces stride=1. Entry conditions involve
`pass1StoppedEarly`, `forceInstabilityThisPixel`, `skippedAnyByStrideThisPixel`, and
`testedAnyInPass0ThisPixel`. Intent appears to be "retry with finer stride if the first
pass may have missed a hit," but this is undocumented and the conditions are complex.

**U2. `bendDir = basisLocal.X`** (`GrinFilmCamera.cs:2502`)

Every ray uses the camera's X-axis as bend direction regardless of ray direction. This is
physically meaningful only for a specific radial distortion model. In integrated mode, the
field acceleration handles bending correctly — but `bendDir` is still passed and used in
the analytic fallback. The relationship between `bendDir` (analytic) and
`ComputeAccelerationAtPointSnap` (integrated) is not explained.

**U3. Acceleration clamp at 50** (`RayBeamRenderer.cs:2063`)

```csharp
else if (aLen > 50f) { a *= (50f / aLen); aLen = 50f; }
```

The magic number 50 is unexplained. Not clear whether this is a physical bound, numerical
stability guard, or empirical tuning value.

**U4. `fieldEvals++` counting** (`RayBeamRenderer.cs:2058`)

Counter incremented unconditionally even when the grid cache was used. Makes `fieldEvals`
misleading — it counts integration steps, not actual field evaluations.

**U5. Grid boundary miss drops acceleration to zero** (`RayBeamRenderer.cs:2044-2048`)

When a grid exists but the point is outside it, the code increments `fieldGridMisses` but
does **not** fall through to `ComputeAccelerationAtPointSnap`. Acceleration stays at
`Vector3.Zero`. Rays leaving the grid bounds silently lose all field influence.

**U6. Relationship between `SimulateRayCamera` and `BuildRaySegmentsCamera_Pass1`**

Both contain very similar integration loops. `SimulateRayCamera` uses the old
`Godot.Collections.Array<Node>` API; `BuildRaySegmentsCamera_Pass1` uses
`FieldSourceSnap[]`. The film pipeline only calls the latter. Unclear whether
`SimulateRayCamera` is still used or is dead code.

---

## 8. Three Incremental Refactors

### Refactor 1: Extract Pass-2 collision into a `FilmCollisionPass` helper

**Problem:** Pass-2 per-pixel collision (`GrinFilmCamera.cs:3333-4462`) is ~1100 lines
of deeply nested code with inline local functions, soft-gate scoring, broadphase
dispatch, and multiple early-out paths — all inside `RenderStep()`.

**Proposal:** Extract a `FilmCollisionPass` class with:
```csharp
HitResult TestPixelCollisions(
    in EffectiveConfig cfg,
    ReadOnlySpan<RaySeg> segments,
    PhysicsDirectSpaceState3D space,
    in PixelContext ctx);
```

**Benefit:** Reduces `RenderStep` by ~1000 lines. Makes collision strategy testable
in isolation and pluggable.

### Refactor 2: Fast-path `Pow(r, gamma)` for common gamma values

**Problem:** `Mathf.Pow` is the most expensive single operation in the per-step hot path
(`ComputeAccelerationAtPointSnap`, `RayBeamRenderer.cs:2288`).

**Proposal:** Add a switch on common values before falling through:
```csharp
float rPow = gamma switch
{
    -2f => 1f / (r * r),
    -1f => 1f / r,
     0f => 1f,
     1f => r,
     2f => r * r,
     _  => Mathf.Pow(r, gamma)
};
```

**Benefit:** For gamma=-2 (inverse-square), eliminates the `Pow` call entirely.
Numerically identical for exact matches, zero-risk fallthrough otherwise.

### Refactor 3: Fix `FieldGrid3D` boundary miss to fall through to source evaluation

**Problem:** When a ray leaves the grid bounds, acceleration silently becomes
`Vector3.Zero` rather than falling through to per-source evaluation
(`RayBeamRenderer.cs:2044-2048`). Almost certainly a bug.

**Proposal:** Change:
```csharp
else if (fieldGrid != null)
{
    fieldGridMisses++;
}
```
to:
```csharp
else if (fieldGrid != null)
{
    fieldGridMisses++;
    if (hasSources)
        a = ComputeAccelerationAtPointSnap(p, fieldSnaps, beta, gamma, bendScale, fieldStrength);
}
```

**Benefit:** Eliminates rays abruptly straightening at grid edges. One-line fix with
minimal performance impact (grid misses should be rare with proper sizing).
