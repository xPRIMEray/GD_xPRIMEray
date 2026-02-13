# Specification — Wormhole Multi-Scene System

**Charter section:** §15 Wormhole System
**Status:** Planned (Phase 3)
**Target location:** `RendererCore/Wormhole/`

---

## 1) Purpose

Defines how the renderer handles wormhole throat geometry: coordinate chart
transitions, multi-scene dispatch, and throat-region rendering. This is the
most speculative tier of the transport roadmap and depends on Tier 2 (full GR
geodesic integration) being operational.

---

## 2) Physics Basis

The Morris–Thorne wormhole metric (Morris & Thorne 1988):

```
ds² = -e^(2Φ) dt² + dr²/(1 - b(r)/r) + r² dΩ²
```

Where `b(r)` is the shape function and `Φ(r)` is the redshift function.
For a traversable wormhole with zero tidal forces, `Φ = 0` and `b(r) = b₀²/r`.

The key rendering challenge: a single ray may cross the throat and emerge in
a different coordinate patch (and potentially a different scene).

---

## 3) Coordinate Atlas

### IChartMap

```csharp
public interface IChartMap
{
    ChartTransitionResult TryTransition(
        in RayState4 state,
        int currentChartId);
}

public struct ChartTransitionResult
{
    public bool Transitioned;
    public int NewChartId;
    public RayState4 NewState;    // transformed to new chart
}
```

The atlas is a collection of overlapping charts. When a ray exits one chart's
validity domain (e.g., crosses the throat coordinate singularity), the atlas
provides the transition to the next chart.

### Chart Types (Planned)

| Chart | Domain | Use |
|-------|--------|-----|
| Exterior A | r > b₀ (universe A) | Standard far-field |
| Throat | r ≈ b₀ | Transition zone |
| Exterior B | r > b₀ (universe B) | Destination scene |

---

## 4) Multi-Scene Dispatch

### Scene Hierarchy

```
RootScene (Universe A)
  ├─ WormholeMouth (position, orientation, throat radius)
  │     └─ chart transition → ChildScene (Universe B)
  └─ normal geometry
```

When a ray transitions through the throat chart, the integrator switches to
the child scene's `SceneSnapshot` and continues evaluation.

### IRaySampler

```csharp
public interface IRaySampler
{
    RaySeg[] ContinueInScene(
        in RayState4 entryState,
        int targetSceneId,
        in SceneSnapshot targetSnapshot);
}
```

---

## 5) Throat Rendering

Near the throat, several effects require special handling:

- **Metric divergence:** `g_rr → ∞` at `r = b(r)` requires regularised coordinates or proper chart overlap
- **Light ring:** Photon orbits at the effective potential peak need sub-step precision
- **Lensing ring:** Multiple images from rays that orbit before escaping
- **Redshift:** `e^(Φ)` factor applied to colour/intensity at film write

---

## 6) Data Structures

### WormholeSOA (Planned addition to SceneSnapshot)

```csharp
public sealed class WormholeSOA
{
    public int Count { get; init; }
    public Vector3[] MouthCenterWorld { get; init; }
    public float[] ThroatRadius { get; init; }     // b₀
    public int[] ChartTypeEnum { get; init; }
    public int[] ChildSceneId { get; init; }
}
```

### SceneId

`SceneSnapshot` gains a `SceneId` field. The integrator maintains a stack of
scene contexts to handle nested wormhole traversal (bounded depth).

---

## 7) Integration with Transport Tiers

Wormhole integration is **Tier 3**: it extends Tier 2 geodesic integration
with chart awareness. The transport interface remains unchanged — `IRayTransport`
returns `RaySeg[]` regardless of how many chart transitions occurred.

The chart transition logic lives inside the Tier 3 `IRayTransport` implementation,
not in the scheduler or backend.

---

## 8) Scope and Deferral

This spec intentionally omits:
- Rotating (Kerr-like) wormholes
- Time-dependent throats
- Traversability physics (stress-energy constraints)
- Multi-wormhole nesting beyond depth 2

These are deferred to a separate research note if the project reaches Phase 3.
The architecture (interface-based chart dispatch) does not preclude them.

---

## 9) References

- Morris, M.S. & Thorne, K.S. (1988). "Wormholes in spacetime and their use
  for interstellar travel." American Journal of Physics 56(5).
- James, O. et al. (2015). "Gravitational lensing by spinning black holes in
  astrophysics, and in the movie Interstellar." Classical and Quantum Gravity 32(6).
