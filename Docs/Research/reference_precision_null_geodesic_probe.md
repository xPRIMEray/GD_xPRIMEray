# Reference-Precision Null Geodesic Probe

## Goal

This is a tiny daytime experiment for local stepper convergence. It reuses object/domain anchors from the object-seeded scheduler path, biases sampling toward projected corners, edge midpoints, principal axes, and domain-boundary samples, then compares coarser probes against a reference-precision baseline.

The reference baseline is the smallest local step tested, not absolute truth.

## CLI Wiring

Enable the experiment with:

```bash
--reference-geodesic-probe=1
```

Optional budget controls:

```bash
--reference-geodesic-probe-max-anchors=2
--reference-geodesic-probe-max-steps=2048
--reference-geodesic-probe-diagonals=0
```

The render-test harness writes diagnostics after capture:

- `*.reference_geodesic_probe.csv`
- `*.reference_geodesic_probe.json`

The probe temporarily adjusts `RayBeamRenderer` stepper fields inside the diagnostics loop, restores them in `finally`, and does not alter final rendering, shading, hit selection, resolver state, or scheduler behavior.

## Minimal C# Implementation Plan

Implemented as a research-only artifact writer in `GrinFilmCamera`:

1. Select anchors from `SceneTransportFingerprint`.
2. Prefer centroid anchors first, then screen-relevant AABB corners, edge midpoints, principal-axis samples, and domain-boundary anchors.
3. Project each anchor to film space.
4. Build a radial micro-search around the projected anchor:
   - center pixel
   - radius 1, 2, 4, 8, and 16 pixels
   - cardinal and diagonal offsets at each radius
   - centroid-radial inward/outward offsets where an object centroid projects into frame
5. For each micro-search sample, call `RayBeamRenderer.BuildRaySegmentsCamera_Pass1` at:
   - `0.015`
   - `0.0125`
   - `0.00625`
   - `0.003125` reference
6. Compare each result to the `0.003125` reference for that anchor/offset.
7. Export CSV/JSON diagnostics only.

## Analyzer Outputs

Run:

```bash
python3 tools/reference_probe_analyzer.py output/reference_geodesic_probe_smoke/<timestamp>
```

The analyzer writes:

- `reference_probe_summary.csv`
- `risk_vs_step_by_anchor.png`
- `nonconvergent_anchor_report.md`
- `decision_risk_heatmap.png`
- `required_precision_heatmap.png`
- `convergence_class_heatmap.png`
- `risk_node_map.png`
- `transport_risk_nodes.csv`
- `risk_node_report.md`
- `transport_risk_regions.csv`
- `risk_region_report.md`
- `radial_risk_profile_by_node.png`
- `risk_region_overlay.png`
- `radial_dist_vs_required_precision.png`
- `transport_coherence_basins.csv`
- `unstable_seams.csv`
- `scene_transport_memory.json`
- `coherence_basin_map.png`
- `transport_entropy_heatmap.png`
- `basin_boundary_overlay.png`
- `unstable_seam_overlay.png`
- `coherence_decay_profile.png`
- `transport_coherence_basin_summary.md`
- `transport_coherence_basin_summary.json`

`TransportRiskNodes` are diagnostic screen-space samples flagged by local decision-risk maxima, persistent mismatch at `0.00625`, or threshold-snap behavior that only matches the reference-precision baseline at `0.003125`.

`TransportRiskRegions` expand each node into nearby sampled pixels and centroid-radial corridors. They estimate the high-risk radius, first stable outer bound where risk falls below epsilon, required precision inside the region, and classify the sampled region as `CORE_STABLE`, `EDGE_TRANSITION`, `CORNER_CURVATURE_SNAP`, `OUTER_STABLE_BOUND`, or `UNSEALED_NONCONVERGENT`.

`TransportCoherenceBasins` are passive local transport-field neighborhoods sampled around high-risk centers. They measure collider/domain/event agreement, normal continuity, hit-distance continuity, path-length continuity, entropy, fragmentation, and local precision floors. `SceneTransportMemory` is diagnostic-only: it must not feed render scheduling, hit selection, shading, resolver decisions, or adaptive precision until a separate future plan explicitly approves that feedback path.

Enable the basin experiment with:

```bash
--transport-coherence-basin=1
--transport-coherence-basin-radii=4,8,16
--transport-coherence-basin-max-centers=32
```

Every basin run records probe budget metrics in the probe JSON and analyzer summary JSON/Markdown. `scene_transport_memory.json` intentionally excludes nondeterministic runtime/path metadata so fixed-camera repeatability can compare its hash directly.

- `probe_sample_count`
- `probe_runtime_ms`
- `max_centers_used`
- `centers_skipped_due_to_budget`
- `rows_written`

## Decision Risk

The diagnostic decision risk is:

```text
DecisionRisk =
  hit/miss mismatch
+ collider mismatch
+ domain mismatch
+ boundary event mismatch
+ portal/throat event mismatch
+ hit distance delta
+ normal angle delta
+ path length delta
+ resolver state changes
```

Resolver state is reserved for a later pass where the probe can sample resolver telemetry without mutating render state.

## Diagnostics Schema

CSV columns:

```text
anchor_id
object_id
step_length
reference_step_length
projected_x
projected_y
object_centroid_projected_x
object_centroid_projected_y
radial_dist_from_object_centroid
radial_angle_from_object_centroid
nearest_anchor_kind
nearest_corner_dist
nearest_edge_dist
projected_tile
radial_offset_x
radial_offset_y
hit
collider_id
domain_id
boundary_events
portal_events
hit_distance
path_length
decision_risk
matched_reference_decision
required_precision_label
```

JSON includes the same row data plus:

- fixture
- scene fingerprint
- reference step length
- row count
- schema list

## Smoke Command

```bash
bash scripts/run_reference_geodesic_probe_smoke.sh
```

The smoke uses 5 frames at `320x180`, two anchors, and a capped probe step budget so it stays cheap while scheduler DOE runs elsewhere.

## Guardrails

- Do not tune beauty aesthetics.
- Do not smooth or post-process artifacts.
- Do not use probe results to choose final color, hit, normal, collider, domain, or resolver state.
- Do not treat the reference-precision baseline as absolute truth.
- Keep budgets explicit and small.
