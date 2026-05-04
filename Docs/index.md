---
title: xPRIMEray
description: Curved-ray geodesic rendering in Godot 4 C# — GRIN lensing through exotic wormhole metrics
---

# xPRIMEray

xPRIMEray is a curved-ray geodesic rendering system built in Godot 4 C#, spanning interactive GRIN lensing through exotic wormhole metrics. It targets academically rigorous gravitational optics with a tiered architecture from Tier 0 GRIN through Tier 3 exotic metrics including Morris-Thorne wormholes. The transport integrator solves null-geodesic equations of the Gordon effective metric using RK4, and fixture renders are checked for complete pixel classification within the eikonal limit.

## Visual Evidence

![Cathedral Probe contact sheet](assets/cathedral_probe/cathedral_probe_contact_sheet_row_0015.png)

*Six-layer Cathedral Probe diagnostic contact sheet — domain resolver stress scene, step_length=0.015, row traversal. From left: beauty render, geometric wireframe, transport ownership map, risk probe markers, spacetime transport diagram, transport continuity vectors.*

![Scheduler resonance stride plot](assets/cathedral_probe/scheduler_resonance_stride_plot.png)

*Scheduler stride sweep (56-cell DOE). Stride 1: ~31% band coverage across all step lengths. Stride 4: < 0.7%. Traversal cadence — not physics precision — is the primary amplifier of row-global banding.*

![Four-mode traversal comparison](assets/cathedral_probe/traversal_contact_sheet_4mode_0015.png)

*Traversal mode comparison at step_length=0.015. Scheduler decorrelation (tile, checkerboard) reduces banding. Local corner instability persists unchanged across all modes — two independent failure layers confirmed.*

The most important finding: **transport instability is topological and localized, not globally smoothable**. The fix is scheduler decorrelation, not precision increase or post-process smoothing. See the [Cathedral Probe architecture paper](Research/cathedral_probe_architecture.md) for the full methodology, evidence, and open questions.

---

## Current Status

Active development. The full wormhole observer ladder (six checkpoints through a Morris-Thorne wormhole) is test-complete under the current validation fixtures. Three feature flags gate experimental diagnostic capabilities:

- **`EnableDomainTelemetry`** — exports heuristic per-pixel renderer diagnostics: `domain_id`, `domain_confidence`, `boundary_confidence`, `selection_flip`, and `normal_discontinuity`.
- **`EnableDomainAwareFirstHitResolver`** — enables an experimental domain-aware first-hit heuristic (requires `EnableDomainTelemetry`; off by default).
- **`EnableTileMetricsScaffold`** — gates the tile-metrics subsystem including reorder simulation, execution, and persistent-priors scheduling.

The bridge (post-throat backstep) is confirmed as the transport anomaly: 366 segments/crossing vs. 50–153 at all other checkpoints (z-score 4.40). Three independent anomaly detectors agree.

## Architecture

xPRIMEray uses a tiered transport hierarchy: Tier 0 GRIN ray integration → Tier 1 metric parameter extraction → Tier 2 Gordon Metric bridge → Tier 3 exotic metrics. The multi-scene wormhole system joins two causally isolated overspaces at the wormhole throat. The hermetic fixture rule (`escaped_no_hit = 0`) enforces complete pixel classification. See [architecture/overview.md](architecture/overview.md) for the pipeline, stored-hit system, and domain emergence, and [architecture_overview.md](architecture_overview.md) for subsystem contracts and data-flow diagrams.

## Research Notes

Domain-aware analysis characterizes three transport regimes (near-side, bridge anomaly, far-side) using PCA and k-means clustering (k=3, ARI=0.595). The renderer-integrated domain maps are heuristic diagnostics for inspecting those signals during fixture runs; they are not proof of metric-only domain ownership by themselves. Penrose, Kajiya, and Bandyopadhyay references in the research notes are inspiration and positioning, not validation evidence for the integrated maps. See [Research/curvature_domain_ownership.md](Research/curvature_domain_ownership.md), [Research/phase_coherence_field.md](Research/phase_coherence_field.md), and [papers/paper_001_causal_observer_ladders/paper.md](papers/paper_001_causal_observer_ladders/paper.md).

## Repository

[https://github.com/AetherTopologist/GD_xPRIMEray](https://github.com/AetherTopologist/GD_xPRIMEray)
