# Diagnostics — Overview

xPRIMEray produces several categories of diagnostic output from each fixture run. This page describes what each diagnostic measures, what it is useful for, and what it is not.

All diagnostics are derived from Pass 1 stored-hit data or from approved debug captures. No diagnostic modifies or overrides the Pass 1 transport classification.

---

## Diagnostic Categories

| Diagnostic | What it measures | Primary use |
|---|---|---|
| [Curvature heat maps](heatmaps.md) | Cumulative turn angle per pixel | Identify transport-structure zones, high-bending annuli |
| [Tile coherence](tile_coherence.md) | Edge recall and direction-fidelity per tile geometry | Choose sampling texture for a given diagnostic goal |
| [Phase coherence](phase_coherence.md) | Per-pixel coherence score relative to neighbourhood | Locate phase-boundary transitions, correlate with banding |
| [Domain ownership](domain_ownership.md) | Transport-regime cluster membership per checkpoint | Identify interpolation-safe zones, domain boundary positions |

---

## Reading Diagnostic Outputs

### Coverage table (`coverage.json`)

The primary hermetic validation output. Fields:

- `classified_coverage_ratio` — must be 1.0 for a passing run
- `escaped_no_hit` — rays that left the scene without classifying; must be 0
- `budget_exhausted` — rays stopped by step budget, not by a hit; must be 0
- Per-class pixel counts: `geom_hit`, `portal_hit`, `throat_event`, `throat_entry`, `throat_exit`, `throat_shell_transform`, `throat_inner_absorb`

### Derived metrics (`derived_metrics.json`)

Computed from the stored-hit table:

| Field | Description |
|---|---|
| `opl_mean`, `opl_max` | Optical path length statistics |
| `portal_hit_density` | Portal hits per pixel |
| `throat_event_density` | Throat events per pixel |
| `crossings_per_pixel` | Portal/throat crossings per pixel |
| `segments_per_crossing` | Integration segments consumed per crossing — high values indicate expensive traversal |
| `average_segments_per_ray` | Mean integration segments per ray |

### Anomaly scores (`bridge_anomaly_scores.json`, `anomaly_detection/scores.json`)

Three independent anomaly measures: Euclidean z-score in standardised feature space, isolation forest, and local outlier factor (LOF). All three ranked `post_throat_backstep_01` (the bridge) as the top anomaly in the six-checkpoint ladder (z = 4.40, isolation forest = 0.616, LOF = 1.35).

---

## What Diagnostics Are Not

- **Heat maps are not observable flux.** A curvature heat map shows cumulative turn angle, not pixel brightness, emissivity, or redshift-weighted intensity. See [heatmaps.md](heatmaps.md).
- **Coherence previews are not validated truth.** Phase-coherence and tile-coherence diagnostics are interpretive layers. They explain structure; they do not override classifications.
- **Clustering is not ground truth.** The k = 3 regime decomposition (ARI = 0.5946) recovers the manual regime labels imperfectly. It is evidence for domain structure, not a definitive boundary map.

---

## Cross-References

- [architecture/overview.md](../architecture/overview.md) — how Pass 1 and Pass 2 produce these outputs
- [validation/hermetic_fixture_rule.md](../validation/hermetic_fixture_rule.md) — coverage requirements
- [papers/paper_001_causal_observer_ladders/paper.md](../papers/paper_001_causal_observer_ladders/paper.md) — full analysis of all six checkpoints
