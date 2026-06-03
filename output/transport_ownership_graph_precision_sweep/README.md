# Transport Ownership Graph Precision Sweep

Short name: **Ownership Graph — Precision Sweep**

## Purpose

Sweeps precision (epsilon) values in the transport ownership graph to find the threshold at which ray-hit normals and ownership transitions become stable. The ownership graph maps which scene geometry "owns" each rendered pixel under the transport.

## Source / Generation Context

- Script: `scripts/run_transport_ownership_graph_precision_sweep.sh`
- Scene: `test-domain-resolver-stress.tscn`
- Runs: `20260504T042350Z`, `20260504T043955Z`

## What the Output Shows

`graph_sweep_summary.md` / `.json` / `.csv` table of hit-normal stability vs. epsilon. `roi_hit_normals.png` and `full_frame_hit_normals.png` visualize the normal field at each precision level. `transport_ownership_graph.json` is the raw ownership graph. `graph_plus_hit_normals_report.md` explains which precision achieves stable ownership boundaries.

## Key Files

- `*/graph_sweep_summary.md` — Precision sweep results table
- `*/roi_hit_normals.png` — ROI normal field at each precision
- `*/full_frame_hit_normals.png` — Full-frame normal field
- `*/transport_ownership_graph.json` — Raw ownership graph
- `*/graph_sweep.log` — Full run log

## Suggested MisterY Labs Card Summary

A precision sweep for xPRIMEray's transport ownership graph: finding the epsilon threshold at which hit normals and geometry ownership boundaries stabilize. The normal field visualizations show how decreasing epsilon progressively sharpens boundary edges.

## Status

Test output

## Notes / Next Steps

- Findings inform the default epsilon setting in the domain resolver.
- Pair with `corner_transport_probe` for boundary-edge stability context.
