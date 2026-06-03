# Transport Coherence Basin Smoke

Short name: **Transport Coherence — Risk Basin Smoke**

## Purpose

Smoke run for the transport coherence basin probe: identifies high-curvature instability zones (UNSEALED\_NONCONVERGENT regions) in the curved minimal scene and validates that the oracle correctly maps them before running the full precision sweep.

## Source / Generation Context

- Script: `scripts/run_transport_coherence_basin_smoke.sh`
- Scene: `test-domain-resolver-stress.tscn`
- Run: `20260503T001944Z`

## What the Output Shows

`risk_region_report.md` identifies 289 UNSEALED\_NONCONVERGENT regions requiring precision 0.003125, centered around pixel coordinates (128, 58) and (128, 122) — two symmetric high-curvature bands in the scene. `convergence_class_heatmap.png` maps these regions. `unstable_seam_overlay.png` highlights boundary seams. `risk_node_map.png` shows the node-level risk graph. All 289 regions at the same precision indicates a systematic instability rather than isolated anomalies.

## Key Files

- `*/risk_region_report.md` — Risk region classification report
- `*/convergence_class_heatmap.png` — Convergence class heatmap
- `*/unstable_seam_overlay.png` — Seam instability overlay
- `*/risk_node_map.png` — Risk node graph
- `*/transport_risk_nodes.csv` / `transport_risk_regions.csv` — Machine-readable risk data
- `*/transport_coherence_basin_summary.json` — Run summary

## Suggested MisterY Labs Card Summary

A convergence risk map for xPRIMEray's curved transport: 289 instability regions, all classified UNSEALED\_NONCONVERGENT, symmetrically distributed in the high-curvature band. This smoke run validated the oracle's mapping capability before the full precision sweep.

## Status

Validation candidate

## Notes / Next Steps

- See `transport_coherence_basin_repeatability` for the stability-over-reruns check.
- 289 unsealed regions may indicate a systematic field geometry issue worth investigating.
