# Domain Telemetry Validation

Short name: **Domain Telemetry — Validation Across Scenes**

## Purpose

Validates domain telemetry output across four scene configurations: curved baseline, curved domain-fast, wormhole prototype baseline, and wormhole prototype with domain-fast mode. Confirms that telemetry records correct domain resolution behavior in both straight and topology-changing scenes.

## Source / Generation Context

- Scenes: `test-curved-minimal.tscn` and wormhole prototype fixture
- Eight run subfolders, each with a configuration-labeled log

## What the Output Shows

Per-configuration logs: `curved_baseline.log`, `curved_domain_fast.log`, `curved_baseline_fast.log`, `wormhole_prototype_baseline.log`, `wormhole_prototype_domain.log`, etc. Also rendered PNGs and derivative step telemetry JSONs. The comparison between `baseline` and `domain_fast` runs reveals the performance gain of the fast domain resolver path.

## Key Files

- `*/curved_baseline.log` / `curved_domain_fast.log` — Curved scene configuration pair
- `*/wormhole_prototype_baseline.log` / `wormhole_prototype_domain.log` — Wormhole pair
- `*domain_fast*.derivative_step.json` — Domain-fast telemetry
- `*curved_minimal__domain-fast*.png` — Domain-fast renders

## Suggested MisterY Labs Card Summary

Interpretation pending — cross-scene domain telemetry validation covering curved and wormhole topology.

## Status

Test output

## Notes / Next Steps

- Extract the domain-fast speedup ratio from log comparisons.
- Connect to `domain_aware_first_hit_validation` for the correctness companion.
