# Domain-Aware First Hit Validation

Short name: **Domain-Aware First Hit — Before/After Validation**

## Purpose

Validates the domain-aware first-hit path by comparing renders before and after enabling it. Confirms that domain-aware mode improves boundary sharpness and does not introduce regressions in either the curved or wormhole scenes.

## Source / Generation Context

- Scene: `test-curved-minimal.tscn` and wormhole witness fixture
- Subfolders: `after/`, `before/`, plus wormhole variants
- ~5 subfolders total

## What the Output Shows

`before.log` / `after.log` capture the full Godot output for both modes. Rendered PNGs and domain ID overlays for the `after` (domain-aware) case. `domain_telemetry_summary.json` records domain resolution stats. The wormhole variants (`wormhole_before.log`, `wormhole_after.log`) extend the comparison to the topology-change scene.

## Key Files

- `after/` — Domain-aware renders and telemetry
- `before.log` / `after.log` — Mode comparison logs
- `wormhole_before.log` / `wormhole_after.log` — Wormhole extension logs
- `*domain-aware-after*.png` — After-mode renders
- `*domain_telemetry_summary.json` — Domain stats

## Suggested MisterY Labs Card Summary

Interpretation pending — before/after validation of the domain-aware first-hit transport path across the curved and wormhole scenes.

## Status

Validation candidate

## Notes / Next Steps

- Confirm the `after` domain ID overlay shows sharper boundaries than `before`.
- Promote as the canonical domain-aware validation evidence.
