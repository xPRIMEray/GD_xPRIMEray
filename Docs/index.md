---
title: xPRIMEray
description: Curved-ray geodesic rendering in Godot 4 C# — GRIN lensing through exotic wormhole metrics
---

# xPRIMEray

xPRIMEray is a curved-ray geodesic rendering system built in Godot 4 C#, spanning interactive GRIN lensing through exotic wormhole metrics. It targets academically rigorous gravitational optics with a tiered architecture from Tier 0 GRIN through Tier 3 exotic metrics including Morris-Thorne wormholes. The transport integrator solves null-geodesic equations of the Gordon effective metric using RK4, and every render is hermetically validated — no unclassified pixels escape within the eikonal limit.

## Current Status

Active development. The full wormhole observer ladder (six checkpoints through a Morris-Thorne wormhole) is test-complete and hermetically validated. Three feature flags gate advanced diagnostic capabilities:

- **`EnableDomainTelemetry`** — exports per-pixel domain classification maps: `domain_id`, `domain_confidence`, `boundary_confidence`, and `selection_flip`.
- **`EnableDomainAwareFirstHitResolver`** — enables domain-coherent first-hit resolution (requires `EnableDomainTelemetry`).
- **`EnableTileMetricsScaffold`** — gates the tile-metrics subsystem including reorder simulation, execution, and persistent-priors scheduling.

The bridge (post-throat backstep) is confirmed as the transport anomaly: 366 segments/crossing vs. 50–153 at all other checkpoints (z-score 4.40). Three independent anomaly detectors agree.

## Architecture

xPRIMEray uses a tiered transport hierarchy: Tier 0 GRIN ray integration → Tier 1 metric parameter extraction → Tier 2 Gordon Metric bridge → Tier 3 exotic metrics. The multi-scene wormhole system joins two causally isolated overspaces at the wormhole throat. The hermetic fixture rule (`escaped_no_hit = 0`) enforces complete pixel classification. See [architecture/overview.md](architecture/overview.md) for the pipeline, stored-hit system, and domain emergence, and [architecture_overview.md](architecture_overview.md) for subsystem contracts and data-flow diagrams.

## Research Notes

Domain-aware rendering characterizes three transport regimes (near-side, bridge anomaly, far-side) using PCA and k-means clustering (k=3, ARI=0.595). Band detection in the bridge regime reveals anomalous transport geometry. The domain telemetry exports (`domain_id`, `domain_confidence`, `boundary_confidence`, `selection_flip` maps) are live and archive-ready. See [Research/curvature_domain_ownership.md](Research/curvature_domain_ownership.md), [Research/phase_coherence_field.md](Research/phase_coherence_field.md), and [papers/paper_001_causal_observer_ladders/paper.md](papers/paper_001_causal_observer_ladders/paper.md).

## Repository

[https://github.com/AetherTopologist/GD_xPRIMEray](https://github.com/AetherTopologist/GD_xPRIMEray)
