# Reference Transport Oracle — Unresolved Island

Short name: **Transport Oracle — Unresolved Island Investigation**

## Purpose

Targeted investigation of the "unresolved island" — a connected region of pixels that the transport oracle cannot converge on even at fine stride. Identifies whether the island is a geometric singularity, a domain resolver gap, or a precision floor issue.

## Source / Generation Context

- Script: `scripts/run_reference_transport_oracle_unresolved_island.sh`
- Scene: `test-domain-resolver-stress.tscn`
- Runs: five timestamped under `output/reference_transport_oracle_unresolved_island/`

## What the Output Shows

`unresolved_island_summary.md` / `.json` describes the island's extent, center, and classification. `ownership_transition_map.png` shows the transport class transitions around the island's boundary. `normal_angle_delta_map.png` maps the normal discontinuity at the island's edge. `local_transport_shape_regions.csv` lists sub-regions by shape type. `island_convergence_ladder.png` plots oracle confidence vs. precision for the island pixels.

## Key Files

- `unresolved_island_summary.md` — Island description and classification
- `island_convergence_ladder.png` — Oracle confidence vs. precision
- `ownership_transition_map.png` — Transport class transitions
- `normal_angle_delta_map.png` — Normal discontinuity at island edge
- `local_transport_shape_regions.csv` — Sub-region breakdown
- `unresolved_island_root_summary.md` / `.csv` — Root cause summary

## Suggested MisterY Labs Card Summary

A deep investigation into a persistent "unresolved island" in the transport oracle: a region of pixels that cannot be classified even at the finest precision. The convergence ladder and transition maps show exactly where the domain resolver's confidence drops — and why.

## Status

Validation candidate

## Notes / Next Steps

- Root summary (`unresolved_island_root_summary.md`) contains the causal hypothesis.
- Resolution may require domain resolver changes or a scene geometry fix.
- Cross-reference with `corner_transport_probe` — corner normal discontinuities may be contributing.
