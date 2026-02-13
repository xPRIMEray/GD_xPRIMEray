# Specification — Research Mode

**Charter section:** §14 Research Mode
**Status:** Planned
**Target location:** `RendererCore/Research/`

---

## 1) Purpose

Research Mode is a configuration layer that selects physics tier combinations,
enables validation instrumentation, and provides deterministic comparison
between transport implementations. It supports the project's academic goal of
GR-correct ray tracing while maintaining a fast preview path for authoring.

---

## 2) Configuration

```csharp
public sealed class ResearchModeConfig
{
    public TransportTier TransportTier { get; init; }
    public IntegratorTier IntegratorTier { get; init; }
    public bool TrackConstraintDrift { get; init; }
    public bool TrackEnergyInvariant { get; init; }
    public bool DeterministicMode { get; init; }
    public float ErrorTolerance { get; init; }
    public string ReferenceSolutionPath { get; init; }
}
```

---

## 3) Tier Presets

| Preset | Transport | Integrator | Validation | Use Case |
|--------|-----------|------------|-----------|----------|
| Tier0_Preview | GRIN | Heuristic Euler | None | Real-time authoring |
| Tier1_ErrorBounded | Gordon / GRIN | RK45 Dormand–Prince | Local truncation error | Quality renders |
| Tier2_InvariantPreserving | Full GR geodesic | Hamiltonian symplectic | Null constraint + energy | Academic validation |

---

## 4) Validation Harness

### 4.1 Constraint Monitoring

At Tier 2, the integrator tracks the null geodesic constraint `g_μν kᵘ kᵛ = 0`.
Drift from zero indicates numerical error. The harness records per-ray
maximum constraint violation and flags rays exceeding the tolerance.

### 4.2 Cross-Tier Comparison

The harness can run two tiers on the same camera and diff:
- Per-pixel hit position error (Euclidean distance)
- Per-pixel path length error
- Miss/hit disagreements
- Visual difference (film-level RMSE)

### 4.3 Known-Solution Tests

For metrics with closed-form solutions (Schwarzschild photon orbits, flat-space
straight lines), the harness compares integrated paths against analytic
trajectories and reports maximum deviation.

---

## 5) Determinism Rules

When `DeterministicMode = true`:
- Tile/row enumeration order is fixed
- No work-stealing reordering
- Thread count is locked
- Random seeds (if any) are fixed per pixel
- Output must be bitwise reproducible across runs

---

## 6) Output Artefacts

Research Mode produces optional diagnostic files:
- Per-ray segment chain dumps (binary or JSON)
- Constraint drift plots (per-ray maximum violation vs path length)
- Cross-tier diff images (colour-coded pixel-level error)
- Aggregate statistics (mean/max error, hit-rate, timing)

---

## 7) Integration with Backends

Research Mode configuration is consumed by the backend dispatcher.
It influences:
- Which `IRayTransport` implementation is instantiated
- Which `IIntegrator` tier is selected
- Whether validation instrumentation is active
- Budget parameters (research may use larger budgets)

The backend interface itself is unchanged — Research Mode is configuration,
not a separate code path.
