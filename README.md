<div align="center">
  <img src="Docs/assets/xPRIMEray-LOGO.png" alt="xPRIMEray" />
</div>

<div align="center">

# xPRIMEray

**Curved Ray Transport Engine for GRIN Fields**

*Where physics performs.*

[![Docs](https://img.shields.io/badge/docs-GitHub_Pages-6644aa?style=flat-square)](https://aethertopologist.github.io/GD_xPRIMEray/)
[![License: MIT](https://img.shields.io/badge/license-MIT-green?style=flat-square)](LICENSE)
[![Godot 4.x](https://img.shields.io/badge/Godot-4.x-blue?style=flat-square)](https://godotengine.org)

</div>

---

A research-grade hybrid symbolic–numeric ray transport system built on Godot Engine.
xPRIMEray augments Godot's renderer with **curved ray physics** — enabling simulation of graded refractive media,
wormhole geometries, and non-Euclidean propagation domains.

Rather than tracing straight rays through space, xPRIMEray integrates rays through fields defined by
continuous curvature functions, supporting higher-fidelity modeling of refractive and metric-driven optical transport.

---

## Documentation

**→ [aethertopologist.github.io/GD_xPRIMEray/](https://aethertopologist.github.io/GD_xPRIMEray/)**

| Section | Description |
|---------|-------------|
| [Core Docs](https://aethertopologist.github.io/GD_xPRIMEray/architecture/) | Architecture, code map, validation framework |
| [Physics & Transport](https://aethertopologist.github.io/GD_xPRIMEray/metric_null_geodesic_param_map/) | Metric models, GRIN transport, null geodesics |
| [Specifications](https://aethertopologist.github.io/GD_xPRIMEray/spec_scene_snapshot_data_layout_1/) | Current and legacy spec documents |
| [Research](https://aethertopologist.github.io/GD_xPRIMEray/Research/DualRealityFramework/) | Dual Reality, Overspaces, fixture studies |
| [Papers](https://aethertopologist.github.io/GD_xPRIMEray/papers/) | Wormhole invariant trilogy (pre-print) |
| [Validation](https://aethertopologist.github.io/GD_xPRIMEray/validation/hermetic_fixture_rule/) | Hermetic fixture rule, observer ladder |

---

## Research Papers

The xPRIMEray invariant trilogy defines geometric contracts for wormhole rendering correctness:

- **[Paper 001](Docs/papers/paper_001_proto_caustic_invariant/paper.md)** — Proto-Caustic Invariant in Geometry-Aware Wormhole Transport
- **[Paper 002](Docs/papers/paper_002_low_value_sector_budget/paper.md)** — Low-Value Sector Budget as a Negative Invariant
- **[Paper 003](Docs/papers/paper_003_coupled_invariants_phase_space/paper.md)** — Coupled Invariants and Stability Phase Space
- **[Paper 004](Docs/papers/paper_004_hermetic_throat_validation/paper.md)** — Hermetic Throat Validation

---

## Wormhole Validation Snapshot

Each deterministic harness run produces a figure quartet:

| Figure | Description |
|--------|-------------|
| A | Raw render output |
| B | Render + research overlay |
| C | Portal-sector density map |
| D | Invariant + performance summary |
| E | Coupled phase-space plot |

---

## License & Citation

Licensed under **MIT** — suitable for academic and creative use.

If this work informs your research, please cite from the [papers index](Docs/papers/index.md).
