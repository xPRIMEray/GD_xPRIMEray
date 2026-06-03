# Reference Geodesic Probe Smoke

Short name: **Geodesic Probe — Convergence Risk Smoke**

## Purpose

Smoke run for the reference geodesic probe: maps convergence class (stable, risk, nonconvergent) across a rendered frame and identifies high-risk nodes before the full ROI sweep. Validates that the probe oracle correctly classifies the scene's transport topology.

## Source / Generation Context

- Script: `scripts/run_reference_geodesic_probe_smoke.sh`
- Scene: `test-domain-resolver-stress.tscn`
- Runs: four timestamped under `output/reference_geodesic_probe_smoke/`

## What the Output Shows

`convergence_class_heatmap.png` shows per-pixel convergence class across the frame. `decision_risk_heatmap.png` highlights high-risk decision points. `risk_node_map.png` graphs the risk node topology. `transport_risk_nodes.csv` lists nodes ordered by risk score. `effective_status.txt` records pass/fail.

## Key Files

- `*/convergence_class_heatmap.png` — Convergence class map
- `*/decision_risk_heatmap.png` — Decision risk heatmap
- `*/risk_node_map.png` — Risk node graph
- `*/transport_risk_nodes.csv` — Ranked risk node list
- `*/effective_status.txt` — Pass/fail

## Suggested MisterY Labs Card Summary

Interpretation pending — smoke validation of the geodesic probe convergence oracle.

## Status

Test output

## Notes / Next Steps

- See `reference_transport_oracle_roi_sweep` for the full ROI sweep using these probe results.
