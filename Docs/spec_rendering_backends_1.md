# Specification — Rendering Backends

**Charter section:** §11 Rendering Backends
**Status:** Legacy implemented, Core/Compare stubbed
**Key source files:** `RenderBackends/LegacyBackend.cs`, `RenderBackends/CoreBackend.cs` (stub), `RenderBackends/CompareBackend.cs` (stub)

---

## 1) Purpose

Rendering backends own the output production chain — the path from completed
ray segment chains through intersection, shading, and film write. The backend
abstraction allows multiple output strategies to coexist.

---

## 2) Backend Dispatch (Implemented)

```csharp
public enum BackendMode { Legacy = 0, Core = 1, Compare = 2 }
```

`GrinFilmCamera.RenderFrameBackend` dispatches based on the `BackendMode`
inspector property.

---

## 3) LegacyBackend (Implemented)

The **only output-producing** backend. Contains the full render pipeline:

```
LegacyBackend.RenderFrame()
  → GrinFilmCamera.RenderStep()
      Pass-1 (parallel):  Integration → RaySeg[] per pixel
      Pass-2 (sequential): Segment envelope → GeometryTLAS prune →
                           Godot narrowphase → SoftGate → Shade → Film write
```

**Key characteristics:**
- Integration and intersection are fused in a single RenderStep
- Pass-2 uses Godot `DirectSpaceState` (main thread only)
- Film buffer is written sequentially per pixel
- SoftGate subdivision for uncertain misses
- Progressive row-band updates

---

## 4) CoreBackend (Stubbed)

Target: pure `RendererCore` pipeline using BLAS intersection. No Godot physics
dependency. Would enable fully parallel Pass-2.

Currently a placeholder that prints a summary and returns without rendering.

---

## 5) CompareBackend (Stubbed)

Target: run both Legacy and Core backends and diff the results for validation.

Currently prints summary only.

---

## 6) Backend Contract

All backends receive:
- `SceneSnapshot` (read-only for frame)
- `CurvatureBoundGrid`
- Camera parameters
- Film buffer reference
- Budget/config

All backends must:
- Produce film pixel updates
- Report telemetry compatible with `PerfScope` / `PerfStats`
- Respect budget guards and cancellation

---

## 7) Film Buffer

The film is a `byte[]` or `Color[]` array sized to camera resolution. Backends
write into it per-pixel or per-tile. `GrinFilmCamera` uploads the film to a
Godot `ImageTexture` after each RenderStep.

---

## 8) Planned Evolution

1. **CoreBackend implementation** (requires BLAS):
   - All-parallel tile tasks
   - Internal triangle intersection
   - No Godot API calls during rendering
   - Replaces LegacyBackend as default

2. **CompareBackend activation** (requires CoreBackend):
   - Per-pixel diff between Legacy and Core outputs
   - Regression detection for BLAS migration
   - Tolerance-based pass/fail reporting

3. **GPU backend** (Phase 3):
   - Compute shader BVH traversal
   - Shared BLAS/TLAS structures between CPU and GPU paths
   - Hybrid CPU integration + GPU intersection possible
