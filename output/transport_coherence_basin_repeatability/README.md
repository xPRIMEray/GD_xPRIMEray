# Transport Coherence Basin Repeatability

Short name: **Transport Coherence — Repeatability Check**

## Purpose

Confirms that the transport coherence basin probe produces stable, deterministic results across repeated runs. If the risk regions and precision requirements shift between runs, it indicates non-determinism in the integrator or oracle.

## Source / Generation Context

- Script: `scripts/run_transport_coherence_basin_repeatability.sh`
- Scene: `test-domain-resolver-stress.tscn`
- Run: `20260503T002217Z`

## What the Output Shows

Same structure as the smoke run: risk region report, heatmap, seam overlay, node map, CSVs. Comparing two runs confirms whether region counts, positions, and precision requirements are stable. The reference probe summary (`reference_probe_summary.csv`) anchors the comparison.

## Key Files

- `*/risk_region_report.md` — Risk region report (compare to smoke run)
- `*/convergence_class_heatmap.png` — Convergence class heatmap
- `*/unstable_seam_overlay.png` — Seam overlay
- `*/reference_probe_summary.csv` — Reference geodesic probe anchor
- `*/transport_coherence_basin_summary.json` — Run summary

## Suggested MisterY Labs Card Summary

Interpretation pending — repeatability verification for the transport coherence basin probe.

## Status

Validation candidate

## Notes / Next Steps

- Compare risk\_region counts and positions to `transport_coherence_basin_smoke` to confirm determinism.
