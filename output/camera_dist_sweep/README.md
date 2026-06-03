# Camera Distance Sweep

Short name: **Camera Distance — Near-Field vs. Mid-Field Sweep**

## Purpose

Sweeps camera distance (backoff) from the scene to identify the ideal near-field and presentation-mid camera positions. Ensures the scene is fully visible and correctly composed at different observer distances.

## Source / Generation Context

- Scene: overspace / hermetic fixture
- Subfolders: `fine_grained/`
- Log files indicate backoff values: 0, 5, 10

## What the Output Shows

Multiple PNG renders at different backoff distances: `dual_backoff_0.png`, `main_backoff_10.png`. Validation logs (`validation_nearfield.log`) confirm the near-field framing is correct. The presentation-mid report (`presentation_mid_report.json`) captures metrics for the mid-range position.

## Key Files

- `dual_backoff_0.png` — Zero-backoff (nearest) render
- `main_backoff_10.png` — Far backoff render
- `fine_grained/` — Fine-grained backoff sweep
- `validation_nearfield.log` — Near-field validation
- `presentation_mid_report.json` — Mid-range metrics

## Suggested MisterY Labs Card Summary

Interpretation pending — camera distance sweep for scene composition tuning.

## Status

Archived

## Notes / Next Steps

- Use `presentation_mid_report.json` to set the canonical observer distance for site renders.
