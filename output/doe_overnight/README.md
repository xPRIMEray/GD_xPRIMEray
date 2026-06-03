# DOE Overnight

Short name: **Overnight DOE — Step Length vs. Telemetry Mode**

## Purpose

Full-factorial design-of-experiments sweep run overnight: varies integration step length (0.00625 to 0.025) against telemetry mode (off vs. telemetry\_on). Measures how telemetry overhead affects band pixel counts and whether output changes between modes (hash-match check).

## Source / Generation Context

- Script: `scripts/run_doe_overnight.sh`
- Scene: curved minimal
- Runs: `20260502T055538Z` (1 cell, aborted), `20260502T060652Z` (18 cells complete)

## What the Output Shows

`DOE_overnight_summary.md` / `.json` / `.csv` table: 18 cells across 9 step lengths × 2 modes. Key findings: finer step lengths capture significantly more band pixels (26% at 0.00625 vs. 0.6% at 0.025). Telemetry-on changes pixel output at most step lengths (hash\_matches\_off=false, changed\_pixels: 960–2560). At coarse steps (0.02+) telemetry overhead is negligible. `DOE_overnight_band_plot.png` visualizes band pixel fraction vs. step length.

## Key Files

- `*/DOE_overnight_summary.md` — Human-readable results table
- `*/DOE_overnight_summary.json` — Machine-readable per-cell results
- `*/DOE_overnight_band_plot.png` — Band pixel fraction plot
- `*/doe_overnight.log` — Full run log

## Suggested MisterY Labs Card Summary

An overnight design-of-experiments run sweeping step length and telemetry mode across 18 cells. The results quantify the resolution / performance tradeoff: fine steps capture 26% of pixels in the high-curvature band vs. 0.6% at coarse steps, while telemetry adds measurable pixel change at most resolutions.

## Status

Test output

## Notes / Next Steps

- Results establish the step-length operating range for production renders (0.01 is a useful middle ground).
- Re-run with the causal tile scheduler to see if scheduler choice interacts with step-length sensitivity.
