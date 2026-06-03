# Wormhole Test

Short name: **Wormhole — Validation and Performance Captures**

## Purpose

Early wormhole validation and performance investigation runs. Captures from multiple launch configurations: portal sector analysis, performance overlap tests, and the final validation render.

## Source / Generation Context

- Launch scripts: `launch_portal_sector_run1.sh`, `launch_perf_overlap_*.sh`, `launch_validation_final3.sh`, `launch_research_overlay.sh`
- Single `figures/` subfolder

## What the Output Shows

Multiple log files per configuration (portal sector, performance overlap cache/copy/query variants, validation final). `wormhole_validation_capture.domain_telemetry_summary.json` records domain telemetry for the validation run. `wormhole_portal_sector_report.json` reports portal sector geometry.

## Key Files

- `launch_validation_final3.log` — Final validation run log
- `wormhole_validation_capture.domain_telemetry_summary.json` — Validation telemetry
- `wormhole_portal_sector_report.json` — Portal sector report
- `figures/` — Figure outputs

## Suggested MisterY Labs Card Summary

Interpretation pending — early wormhole validation and performance testing captures.

## Status

Archived

## Notes / Next Steps

- Superseded by `wormhole_structure_observatory` and `wormhole_DR_Story` for current wormhole work.
