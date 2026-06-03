# Corner Transport Probe

Short name: **Corner Transport — Edge Stability Probe**

## Purpose

Investigates transport stability at geometry corners and edges, where normal discontinuities cause the domain resolver's first-hit logic to be most sensitive. Identifies which corner configurations produce stable vs. unstable transport outcomes.

## Source / Generation Context

- Script: `scripts/run_corner_transport_probe.sh`
- Scene: `test-domain-resolver-stress.tscn`
- Runs: `20260503T132402Z`, `20260503T132655Z`

## What the Output Shows

`corner_normal_delta.png` maps the normal discontinuity at each corner. `corner_threshold_report.md` identifies stable vs. unstable corners. `effective_status.txt` summarizes pass/fail. `corner_transport_probe.csv` has per-corner metrics. The derivative step JSON captures integration behavior at each corner configuration.

## Key Files

- `*/corner_normal_delta.png` — Normal discontinuity map
- `*/corner_threshold_report.md` — Stability classification
- `*/effective_status.txt` — Pass/fail summary
- `*/corner_transport_probe.csv` — Per-corner metrics
- `*reference_geodesic_probe.json` — Geodesic probe data per corner

## Suggested MisterY Labs Card Summary

A targeted probe of transport stability at geometry corners: the domain resolver's hardest test case. The normal discontinuity map shows exactly where the integrator struggles, and the threshold report identifies which configurations produce reliable hits.

## Status

Validation candidate

## Notes / Next Steps

- Findings should feed into `BoundaryLayerVolume.ComputeBoundaryNormal` — corner normals are now computed per face for box shapes (Phase 1).
- Re-run after any changes to the domain resolver's first-hit or normal computation logic.
