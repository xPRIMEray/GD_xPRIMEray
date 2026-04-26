# Architecture Overview

xPRIMEray is a self-contained rendering system embedded in Godot 4.x. It owns its simulation, intersection, and scheduling logic; Godot provides scene extraction and display. This document describes the pipeline structure, the two rendering passes, the stored-hit system, and the concept of domain emergence.

Related: [architecture_overview.md](../architecture_overview.md) (detailed subsystem breakdown with code contracts), [SPEC_INDEX.md](../SPEC_INDEX.md) (full spec list).

---

## Pipeline Overview

```
Godot Scene
    │
    ▼
Scene Snapshot          ← immutable per-frame representation
    │
    ▼
Acceleration Layer      ← BLAS (per-mesh BVH) + TLAS (world-space BVH)
    │
    ▼
Field System            ← GRIN field evaluation, curvature bounds
    │
    ▼
Ray Integrator          ← RK4 curved-ray integration → RayChunks
    │
    ▼
Intersection System     ← BVH traversal over RayChunks → HitRecords
    │
    ▼
Shading + Film          ← material evaluation, pixel writeback
    │
    ▼
Final Image
```

Each layer communicates through explicit data contracts. Cross-layer coupling is a design violation.

---

## Pass 1 — Transport (Scout Pass)

Pass 1 integrates rays from the camera outward through the GRIN field, producing classified transport results for every pixel. This is the authoritative validation pass.

**Guarantees (hermetic mode):**

| Metric | Required value |
|---|---|
| `classified_coverage_ratio` | 1.0 |
| `escaped_no_hit` | 0 |
| `budget_exhausted` | 0 |
| `unclassified` | 0 |

Every pixel must terminate at a classified event — `geom_hit`, `portal_hit`, `throat_event`, `throat_entry`, `throat_exit`, `throat_shell_transform`, or `throat_inner_absorb`. A pixel that fails to classify is a renderer gap, not a physical result.

**Ray integration:** The integrator steps along the eikonal ODE

$$\dot{\mathbf{x}} = \frac{\mathbf{p}}{n}, \qquad \dot{\mathbf{p}} = \nabla n$$

using RK4 with a derivative-aware adaptive step controller. At each step, the controller estimates local curvature $\kappa$ and its first derivative $\dot\kappa$ to predict whether the step length can safely increase or must shrink. The result is a sequence of **RayChunks** — conservative bounding envelopes over which BVH traversal is performed.

**Stored-hit output:** Pass 1 records the first accepted intersection hit per pixel (position, normal, material ID, transport classification). This stored-hit table is the ground truth for Pass 2.

---

## Pass 2 — Research / Diagnostic Pass

Pass 2 operates on the stored-hit table from Pass 1 without re-running the full integrator. It supports:

- **Coherence diagnostics** — per-pixel phase-coherence and neighbour-normal-delta scores
- **Tile analysis** — adaptive square and polar/radial tile overlays
- **Sampling texture comparisons** — edge-alignment and direction-similarity metrics
- **Morphology analysis** — Hough-line counts, contour eccentricity, connected-component maps

**Critical constraint:** Pass 2 results are interpretive diagnostics. They do not modify, override, or replace Pass 1 classifications. The raw row-pass is always the truth source.

---

## Stored-Hit System

The stored-hit table records, for each pixel:

```
StoredHit = {
    position: Vector3,        ← world-space hit position
    normal: Vector3,          ← surface normal at hit
    materialId: int,
    transportClass: enum,     ← geom_hit | portal_hit | throat_event | …
    opl: float,               ← optical path length (cumulative n·ds)
    crossings: int,           ← number of portal/throat crossings
    segments: int             ← integration segments consumed
}
```

Derived metrics — portal-hit density, throat-event density, crossings per pixel, segments per crossing, OPL mean/max — are computed from this table and form the input to all downstream analysis.

---

## Domain Emergence

xPRIMEray does not assign domain labels to scene objects. Domains emerge from metric structure: the field $n(\mathbf{x})$ and the transport events it produces determine which transport regime a pixel belongs to.

In the wormhole observer ladder, three domains emerge automatically from k-means clustering of the stored-hit metrics (ARI = 0.5946 at k = 3):

| Domain | Checkpoints | Characteristics |
|---|---|---|
| Near-side | mouth, mouth-to-throat, throat | Increasing density, decreasing cost, smooth interpolation |
| Bridge | post-throat backstep | Sparse, high-cost transport; multi-metric anomaly |
| Far-side | exit-approach, exit-lookback | Re-densification, tangential orientation shift |

The bridge domain is not defined by its geometric position between the throat and the exit. It is defined by its transport signature: low interaction density, minimum OPL mean, and maximum segments-per-crossing (366 vs 50–153 for all other checkpoints). This is a **transport-side domain boundary**, not a coordinate boundary.

**Implication for interpolation:** World-space interpolation between observer positions is only valid within a domain. Interpolation that crosses a domain boundary produces undefined results. This is why a mixed strategy — interpolation on the near-side leg, discovered checkpoints for the bridge — is required for coherent traversal.

---

## GRIN Field and the Gordon Metric

The field system evaluates a scalar refractive-index field $n(\mathbf{x})$ at each integration step. The ray equation is the Hamiltonian system

$$H = \frac{|\mathbf{p}|^2}{2n^2} = \frac{1}{2}$$

with $\dot{\mathbf{x}} = \partial H/\partial\mathbf{p}$ and $\dot{\mathbf{p}} = -\partial H/\partial\mathbf{x}$. This is the null-geodesic equation of the Gordon effective metric

$$\tilde{g}^{\mu\nu} = g^{\mu\nu} + \left(1 - \frac{1}{n^2}\right)u^\mu u^\nu$$

For a static isotropic medium, this reduces exactly to $dl^2 = n^2(\mathbf{x})\,d\mathbf{x}^2$, and the visual output is the correct image of that effective spacetime within the eikonal limit.

The current metric stub (`TransportModel = Metric_NullGeodesic`) implements a weak-field scalar proxy for the Gordon metric. Active parameters: `Amp`, `CanonicalBetaScale`, `ROuter`. All other canonical parameters influence results only indirectly through the GRIN fallback path.

---

## Godot Integration Boundary

```
Godot Nodes
    │
    ▼
Renderer Adapter Layer     ← SceneSnapshot extraction, display writeback
    │
    ▼
Renderer Core              ← engine-agnostic simulation, acceleration, intersection
```

The renderer core is architecturally independent of Godot. Godot provides: scene extraction, display output, debug visualisation. The renderer core owns: simulation, acceleration structures, intersection, scheduling, telemetry.

---

## Design Invariants

The following must remain true across all changes:

- Scene snapshots are immutable per frame.
- Intersection is renderer-owned; Godot's native BVH is not used for curved-ray queries.
- Curved rays are first-class primitives, not straight-line approximations.
- Subsystems communicate through explicit contracts.
- Hot paths avoid dynamic allocation (struct-of-arrays layout, frame arenas).
- Telemetry is always available; no diagnostic path can be silently disabled.
- Pass 1 classifications are never modified by Pass 2 analysis.

---

## Cross-References

- Detailed subsystem contracts: [architecture_overview.md](../architecture_overview.md)
- Hermetic fixture rule: [validation/hermetic_fixture_rule.md](../validation/hermetic_fixture_rule.md)
- GRIN field spec: [spec_field_system_grin_1.md](../spec_field_system_grin_1.md)
- Curved ray integration spec: [spec_curved_ray_chunks_1.md](../spec_curved_ray_chunks_1.md)
- Observer ladder paper: [papers/paper_001_causal_observer_ladders/paper.md](../papers/paper_001_causal_observer_ladders/paper.md)
- Transport model review: [Research/curved_ray_transport_model_review.md](../Research/curved_ray_transport_model_review.md)
