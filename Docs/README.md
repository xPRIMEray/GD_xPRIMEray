# xPRIMEray — Documentation

> **The Universal Baseline for Curved Field Transport Visualization.**
> Research-grade. Fixture-validated. Grounded in a century of peer-reviewed optics and relativity.

This is the technical documentation root for xPRIMEray. For the GitHub project page and visual overview, see the [root README](../README.md). For the rendered MkDocs portal, see [index.md](index.md).

---

## Vision

xPRIMEray is designed to become the **standard reproducible baseline** for studying, measuring, and communicating curved optical transport — the tool that plays the same role for field transport that Kajiya's rendering equation played for physically-based rendering in 1986.

The academic pipeline it supports:

```
Nobel-level physical foundations
    ↓
Deterministic fixture harness (hermetic, fresh-instance, reproducible)
    ↓
Transport metrics + diagnostic overlays
(OPL · coherence · phase maps · domain clustering · anomaly scores)
    ↓
Abstract → Hypothesis → Measurement → Paper
    ↓
Shared bibliography · reproducible figures · BibTeX-ready citations
```

This pipeline has already produced Papers 001–004 in the wormhole invariant trilogy. Every figure is reproducible from archived fixture outputs. Every claim is separated into: **empirical finding**, **modelling assumption**, and **conceptual analogy** — labelled explicitly.

---

## What xPRIMEray Is (precise statement)

xPRIMEray integrates the null-geodesic ray equations of the **Gordon effective metric**:

$$\dot{\mathbf{x}} = \frac{\mathbf{p}}{n(\mathbf{x})}, \qquad \dot{\mathbf{p}} = \nabla n(\mathbf{x})$$

where $n(\mathbf{x})$ is a spatially varying refractive-index field. These are the characteristic ODEs of the metric $dl^2 = n^2\,d\mathbf{x}^2$, which is the exact null-ray law of a static isotropic medium (Gordon 1923; Plebański 1960). The visual output is the correct image of that effective spacetime within the eikonal limit.

xPRIMEray does **not** solve the Einstein field equations. It renders null geodesics of an effective spacetime defined by $\nabla n$. This is a stronger claim than "inspired by GR" and an honester claim than "we render true wormholes."

---

## Key Innovations

| Innovation | Technical description |
|---|---|
| **Curved ray integration** | RK4 + derivative-aware adaptive step control on the eikonal ODE. Rays are first-class curved primitives. |
| **Dual Reality rendering** | Two causally distinct scenes joined at a topological portal; each rendered with full curved-ray physics. |
| **Hermetic fixture harness** | 100% classified pixel coverage, `escaped_no_hit = 0`, `budget_exhausted = 0` — reproducible across any run. |
| **Causal observer ladder** | Six fresh-instance checkpoints through a wormhole; transport structure separated from simulation state. |
| **Domain-emergent clustering** | Research analysis recovered transport regimes from metric structure (k=3, ARI=0.5946); renderer-integrated domain maps are heuristic diagnostics, not proof by themselves. |
| **Geometric sampling textures** | Hybrid adaptive-square (direction: 0.836/0.875) + polar (recall: 1.000/1.000) diagnostic tiles. |
| **Phase-coherence field** | Per-pixel coherence score correlating visible banding with domain-boundary transitions (gap: 0.162 at mouth). |
| **Overspace / rabbit-hole scenes** | Hermetically sealed, causally isolated wormhole universes. Each is its own complete transport domain. |

---

## Two Audience Streams

### 🔬 Research Track

The core academic infrastructure. Everything is deterministic, archived, and citable.

- Fixture harness outputs: `output/fixture_runs/`
- Derived metrics: `output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/`
- Observer ladder paper: [papers/paper_001_causal_observer_ladders/paper.md](papers/paper_001_causal_observer_ladders/paper.md)
- Shared bibliography: [papers/shared_bibliography.bib](papers/shared_bibliography.bib)

### 🎨 Creative / Entertainment Track

> ⚠️ **Artistic Capabilities Notice** — xPRIMEray produces physically motivated, not observationally validated, imagery. The visuals are correct images of an effective spacetime. They are not equivalent to data from a real astrophysical source. Use freely for creative purposes with that understanding.

- Dual Reality renders: `output/dual_reality/`
- Overspace traversal sequence: `output/overspace_first_milestone/`
- The rabbit-hole mythos: see the [root README](../README.md#the-rabbit-hole-universe)

---

## Documentation Map

### Core navigation

| Document | Contents |
|---|---|
| [glossary.md](glossary.md) | null geodesic · GRIN · Gordon metric · domain boundary · phase coherence · curvature centre · node of silence |
| [architecture/overview.md](architecture/overview.md) | Pipeline, Pass 1 / Pass 2, stored-hit system, domain emergence, Gordon metric math |
| [SPEC_INDEX.md](SPEC_INDEX.md) | Full spec index — active and legacy |

### Diagnostics

| Document | Contents |
|---|---|
| [diagnostics/README.md](diagnostics/README.md) | Diagnostic categories, reading guide for coverage.json and derived_metrics.json |
| [diagnostics/heatmaps.md](diagnostics/heatmaps.md) | Curvature heat maps — transport diagnostic, not observable flux; literature crosswalk |
| [diagnostics/tile_coherence.md](diagnostics/tile_coherence.md) | Adaptive vs polar tiles — direction fidelity vs recall trade-off with all measured numbers |
| [diagnostics/phase_coherence.md](diagnostics/phase_coherence.md) | Phase-coherence field — measured values, banding mechanism hypothesis, geometric phase memory |
| [diagnostics/domain_ownership.md](diagnostics/domain_ownership.md) | Domain decomposition — the bridge anomaly, interpolation validity by domain |

### Research notes

| Document | Contents |
|---|---|
| [research/geometric_sampling_texture.md](research/geometric_sampling_texture.md) | Sampling texture synthesis: adaptive, polar, bridge morphology, hybrid architecture |
| [research/phase_coherence_field.md](research/phase_coherence_field.md) | Phase coherence as diagnostic; geometric phase memory design framework and 5-phase roadmap |
| [research/curvature_domain_ownership.md](research/curvature_domain_ownership.md) | Domain decomposition derivation; spectral ruling-out of oscillatory model; render guidance |

### Physics and transport

| Document | Contents |
|---|---|
| [Research/curved_ray_transport_model_review.md](Research/curved_ray_transport_model_review.md) | Model ranking: Hamiltonian, RK, Eikonal, Lie, neural — what each solves |
| [Research/wormhole_curvature_heatmap_literature_crosswalk.md](Research/wormhole_curvature_heatmap_literature_crosswalk.md) | Where xPRIMEray aligns with and differs from astrophysics literature |
| [Research/DualRealityFramework.md](Research/DualRealityFramework.md) | Dual-scene composition, wormhole topology, throat-side/far-side rendering |
| [Research/overspace_architecture_layer.md](Research/overspace_architecture_layer.md) | Wormhole systems, rabbit-hole nesting, scale-clock-density transforms |
| [metric_null_geodesic_param_map.md](metric_null_geodesic_param_map.md) | Active parameter mapping for Metric_NullGeodesic transport mode |
| [metric_transport_nextgen_roadmap.md](metric_transport_nextgen_roadmap.md) | Next-generation transport: symplectic, Eikonal guidance, derivative-aware stepping |
| [papers/shared_related_work.md](papers/shared_related_work.md) | Full literature survey across five research traditions |

### Papers

| # | Title | Location |
|---|---|---|
| 000 | Unified Summary — Wormhole Invariant Trilogy | [papers/paper_000_unified_summary/paper.md](papers/paper_000_unified_summary/paper.md) |
| 001 | Causal Observer Ladders for Wormhole Ray Transport | [papers/paper_001_causal_observer_ladders/paper.md](papers/paper_001_causal_observer_ladders/paper.md) |
| 001a | Causal Observer Ladder Artifact Atlas | [papers/paper_001a_causal_observer_ladder_artifacts/paper.md](papers/paper_001a_causal_observer_ladder_artifacts/paper.md) |
| 002 | Low-Value Sector Budget as a Negative Invariant | [papers/paper_002_low_value_sector_budget/paper.md](papers/paper_002_low_value_sector_budget/paper.md) |
| 003 | Coupled Invariants and Stability Phase Space | [papers/paper_003_coupled_invariants_phase_space/paper.md](papers/paper_003_coupled_invariants_phase_space/paper.md) |
| 004 | Hermetic Throat Validation | [papers/paper_004_hermetic_throat_validation/paper.md](papers/paper_004_hermetic_throat_validation/paper.md) |

Shared bibliography: [papers/shared_bibliography.bib](papers/shared_bibliography.bib)

### Validation

| Document | Contents |
|---|---|
| [validation/hermetic_fixture_rule.md](validation/hermetic_fixture_rule.md) | The hermetic coverage contract — what 100% classified means and why it matters |
| [validation/wormhole_observer_ladder.md](validation/wormhole_observer_ladder.md) | Observer ladder protocol |
| [validation/wormhole_observer_ladder_characterization.md](validation/wormhole_observer_ladder_characterization.md) | Characterisation results |
| [validation.md](validation.md) | Validation framework overview |
| [BoundaryLayerFixtures.md](BoundaryLayerFixtures.md) | Boundary layer fixture specifications |

### Active specifications

| Area | Document |
|---|---|
| Data model | [spec_scene_snapshot_data_layout_1.md](spec_scene_snapshot_data_layout_1.md) |
| Field evaluation | [spec_field_system_grin_1.md](spec_field_system_grin_1.md) |
| FieldSource3D params | [spec_fieldsource3d_canonical_params_1.md](spec_fieldsource3d_canonical_params_1.md) |
| Metric framing | [spec_metric_models_grin_vs_gordon_1.md](spec_metric_models_grin_vs_gordon_1.md) |
| Curved transport | [spec_curved_ray_chunks_1.md](spec_curved_ray_chunks_1.md) |
| Acceleration | [spec_bvh_acceleration_1.md](spec_bvh_acceleration_1.md) |
| Scheduling | [spec_scheduler_task_graph_1.md](spec_scheduler_task_graph_1.md) |
| Backends | [spec_rendering_backends_1.md](spec_rendering_backends_1.md) |
| Telemetry | [spec_telemetry_debug_1.md](spec_telemetry_debug_1.md) |
| Multi-scene | [spec_wormhole_scene_graph_1.md](spec_wormhole_scene_graph_1.md) |
| Research mode | [spec_research_mode_1.md](spec_research_mode_1.md) |

### Calibration and working charter

- [_xPRIMEray_arch_charter_v3-ChatClaudeGrokCoherencePass2.md](_xPRIMEray_arch_charter_v3-ChatClaudeGrokCoherencePass2.md) — current working charter
- [CalibRoadmap/PatchLogs/C1_0_g_1.md](CalibRoadmap/PatchLogs/C1_0_g_1.md) — canonical signature fields
- [CalibRoadmap/PatchLogs/C1_7_g_X.md](CalibRoadmap/PatchLogs/C1_7_g_X.md) — AutoCal weak-signal stopgap

---

## Epistemic Posture

Throughout all documentation, claims are separated by type:

| Label | Meaning |
|---|---|
| **Empirical finding** | Measured from fixture outputs; reproducible; citable |
| **Modelling assumption** | Chosen for tractability; stated explicitly; open to revision |
| **Conceptual analogy** | Borrowed vocabulary; useful for organising thinking; not a physical equivalence claim |
| **Hypothesis** | Grounded in correlation; not yet confirmed by controlled experiment |

Preferred phrasing:
- "null geodesics of the Gordon metric" — not "physically accurate wormholes"
- "transport-distortion diagnostic" — not "observable intensity map"
- "observed / measured / correlated / hypothesised" — not "proves / demonstrates"

---

## License

MIT — academic, commercial, and creative use welcome.
Citation templates: [papers/shared_bibliography.bib](papers/shared_bibliography.bib)
