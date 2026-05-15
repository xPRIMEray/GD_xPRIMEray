# xPRIMEray — Technical Documentation

**xPRIMEray** is a research-grade curved-ray transport engine embedded in Godot 4.x. It replaces straight-line ray casting with physics-driven propagation through gradient-index (GRIN) fields, enabling simulation of graded refractive media, wormhole-like topological structures, and general metric-driven optical transport.

---

## What Problem It Solves

Standard ray tracers assume light travels in straight lines. This is valid in flat, uniform media but breaks down in:

- **Curved spacetimes** — null geodesics bend around massive objects and through wormhole throats.
- **GRIN media** — gradient-index materials (e.g. gravitational-lens analogues, optical fibre cores) deflect rays continuously along the path.
- **Topological transitions** — a ray entering a wormhole mouth may exit into a physically distinct coordinate region; world-space interpolation across that boundary is undefined.

xPRIMEray addresses all three cases within a single transport framework grounded in the **Gordon effective metric**: the engine integrates the eikonal ray equations $\dot{\mathbf{x}} = \mathbf{p}/n$, $\dot{\mathbf{p}} = \nabla n$, which are the characteristic ODEs of null-geodesic transport through a spatially varying refractive-index field. This is not an approximation to GR — it is the exact null-ray law of the Gordon metric, valid for any static isotropic medium.

---

## Key Innovations

| Innovation | Description |
|---|---|
| **Curved ray integration** | RK4 integration of the eikonal ODE, with derivative-aware adaptive step control. Rays are first-class curved primitives, not post-hoc approximations. |
| **Domain-aware rendering** | Research analysis estimates distinct transport regimes (near-side, bridge, far-side); renderer-integrated maps are heuristic diagnostics and may include runtime/fixture signals. |
| **Geometric sampling textures** | Multiple tile geometries — adaptive square, polar/radial, curvature-centred — each tuned for different diagnostic goals. Hybrid architecture retains raw row-pass truth as scout reference. |
| **Hermetic validation** | Every fixture run must satisfy `classified_coverage = 1.0`, `escaped_no_hit = 0`, `budget_exhausted = 0`. No pixel may be lost to the computational equivalent of a singularity. |
| **Fresh-instance observer ladder** | Each checkpoint is evaluated on a fully fresh renderer instance, ensuring reported differences reflect transport structure rather than accumulated state. |

---

## Documentation Map

### Start here

| Document | Contents |
|---|---|
| **This file** | Entry point and orientation |
| [architecture/overview.md](architecture/overview.md) | Render pipeline, Pass 1 vs Pass 2, stored-hit system, domain emergence |
| [glossary.md](glossary.md) | Definitions for null geodesic, GRIN, domain boundary, phase coherence, curvature centre |

### Diagnostics

| Document | Contents |
|---|---|
| [diagnostics/README.md](diagnostics/README.md) | What each diagnostic measures and when to use it |
| [diagnostics/heatmaps.md](diagnostics/heatmaps.md) | Curvature heat maps — transport diagnostic, not flux observable |
| [diagnostics/tile_coherence.md](diagnostics/tile_coherence.md) | Adaptive vs polar tiling, direction fidelity vs recall trade-off |
| [diagnostics/phase_coherence.md](diagnostics/phase_coherence.md) | Phase-coherence field — band boundaries, coherence scores |
| [diagnostics/domain_ownership.md](diagnostics/domain_ownership.md) | Curvature-domain boundary detection and regime decomposition |

### Research notes

| Document | Contents |
|---|---|
| [Research/geometric_sampling_texture.md](Research/geometric_sampling_texture.md) | Sampling texture analysis synthesis — adaptive, polar, hybrid architecture |
| [Research/phase_coherence_field.md](Research/phase_coherence_field.md) | Phase coherence as a transport-domain diagnostic |
| [Research/curvature_domain_ownership.md](Research/curvature_domain_ownership.md) | Domain decomposition, clustering, and the bridge anomaly |

### Specifications

See [SPEC_INDEX.md](SPEC_INDEX.md) for the full list of active and legacy spec files.

Key active specs:

- [spec_field_system_grin_1.md](spec_field_system_grin_1.md) — GRIN field evaluation
- [spec_curved_ray_chunks_1.md](spec_curved_ray_chunks_1.md) — curved ray segment integration
- [spec_metric_models_grin_vs_gordon_1.md](spec_metric_models_grin_vs_gordon_1.md) — metric model framing
- [spec_scheduler_task_graph_1.md](spec_scheduler_task_graph_1.md) — tile scheduling
- [spec_wormhole_scene_graph_1.md](spec_wormhole_scene_graph_1.md) — multi-scene wormhole system

### Research papers

Papers are located in [papers/](papers/) and indexed at [papers/index.md](papers/index.md).

| # | Title | Status |
|---|---|---|
| [000](papers/paper_000_unified_summary/paper.md) | Unified Summary of the Wormhole Invariant Trilogy | Draft |
| [001](papers/paper_001_causal_observer_ladders/paper.md) | Causal Observer Ladders for Wormhole Ray Transport | Active |
| [002](papers/paper_002_low_value_sector_budget/paper.md) | Low-Value Sector Budget as a Negative Invariant | Draft |
| [003](papers/paper_003_coupled_invariants_phase_space/paper.md) | Coupled Invariants and Stability Phase Space | Draft |
| [004](papers/paper_004_hermetic_throat_validation/paper.md) | Hermetic Throat Validation | Draft |

Shared bibliography: [papers/shared_bibliography.bib](papers/shared_bibliography.bib)  
Shared related work: [papers/shared_related_work.md](papers/shared_related_work.md)

### Validation

| Document | Contents |
|---|---|
| [validation/hermetic_fixture_rule.md](validation/hermetic_fixture_rule.md) | The hermetic coverage contract |
| [validation/wormhole_observer_ladder.md](validation/wormhole_observer_ladder.md) | Observer ladder protocol |
| [validation.md](validation.md) | Validation framework overview |

### Existing architecture and specification navigation

The following top-level files remain the canonical home for their domains:

- [index.md](index.md) — MkDocs site index (rendered documentation portal)
- [architecture_overview.md](architecture_overview.md) — Detailed subsystem breakdown
- [code_map_big12.md](code_map_big12.md) — Contributor-facing code map
- [_xPRIMEray_arch_charter_v3-ChatClaudeGrokCoherencePass2.md](_xPRIMEray_arch_charter_v3-ChatClaudeGrokCoherencePass2.md) — Working architecture charter

---

## Epistemic Posture

xPRIMEray renders null geodesics of the **Gordon effective metric**, not solutions to the Einstein field equations. This is a precise and honest claim: the physics is well-defined, the visual output is the correct image of that effective spacetime within the eikonal limit, and the approximation degrades gracefully as the medium becomes dynamic or anisotropic. Claims that go beyond this — e.g. that the engine produces observationally faithful black-hole or wormhole images — require additional ingredients (radiative transfer, thin-disk emissivity, redshift weighting) that are not yet implemented.

Preferred phrasing throughout the documentation:

- "null geodesics of the Gordon metric" — not "physically accurate wormholes"  
- "transport-distortion diagnostic" — not "observable intensity map"  
- "observed," "measured," "correlated," "hypothesised" — not "proves," "demonstrates physically," "is equivalent to"

---

## License

MIT — suitable for academic and creative use. If this work informs your research, cite the relevant paper from the invariant trilogy (BibTeX at [papers/shared_bibliography.bib](papers/shared_bibliography.bib)).
