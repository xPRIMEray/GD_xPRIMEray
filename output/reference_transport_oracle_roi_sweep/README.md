# Reference Transport Oracle ROI Sweep

Short name: **Transport Oracle — ROI Stride Sweep**

## Purpose

Full ROI sweep using the transport oracle: varies row-stride across the domain resolver stress scene to measure how stride affects transport ownership precision and oracle hit rate. Quantifies the trade-off between render speed (high stride) and boundary accuracy (low stride).

## Source / Generation Context

- Script: `scripts/run_reference_transport_oracle_roi_sweep.sh`
- Scene: `test-domain-resolver-stress.tscn`
- Runs: six timestamped under `output/reference_transport_oracle_roi_sweep/`

## What the Output Shows

`reference_transport_oracle_summary.md` / `.json` table: oracle hit rate, changed pixels, and effective status per stride. `reference_transport_oracle.log` captures full output. Each cell's `derivative_step.json` records integration telemetry at that stride. Low strides produce better oracle hit rates; the summary identifies the crossover point where stride begins to degrade accuracy.

## Key Files

- `*/reference_transport_oracle_summary.md` — Oracle accuracy vs. stride table
- `*/reference_transport_oracle_summary.json` — Machine-readable results
- `*row_stride_1*.png` — Full-density oracle render
- `*/reference_transport_oracle.log` — Full run log
- `*/status.txt` / `effective_status.txt` — Pass/fail per cell

## Suggested MisterY Labs Card Summary

A stride sweep of the transport oracle: measuring how row-skip density affects the oracle's ability to correctly classify transport ownership across the scene. The results establish the minimum stride for reliable classification — essential for setting production render parameters.

## Status

Test output

## Notes / Next Steps

- Connect findings to `reference_transport_oracle_unresolved_island` — that run targets the specific regions where even low stride leaves unresolved ownership.
