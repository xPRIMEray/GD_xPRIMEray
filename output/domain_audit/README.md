# Domain Audit

Short name: **Domain Audit — Off Mode Baseline**

## Purpose

Baseline renders with domain auditing disabled (`audit_off`), capturing the curved minimal scene without any telemetry overhead. Used as the "off" reference for comparing domain audit impact on output and performance.

## Source / Generation Context

- Script: `scripts/run_domain_audit_quick.sh` (audit\_off variant)
- Scene: `test-curved-minimal.tscn`
- Single run folder: `off/`

## What the Output Shows

Three renders (baseline, default, tight\_env pruning variants) with audit off. These PNGs are the clean reference images against which audited runs are compared in `domain_audit_visual`.

## Key Files

- `off/*curved_minimal__audit_off__baseline_prune_off*.png` — Baseline no-audit render
- `off/*curved_minimal__audit_off__prune_on_default*.png` — Default pruning, no audit
- `off/*curved_minimal__audit_off__prune_on_tight_env*.png` — Tight env, no audit
- `off/*.derivative_step.json` — Telemetry per variant

## Suggested MisterY Labs Card Summary

Interpretation pending — audit-off baseline captures for the domain audit comparison series.

## Status

Archived

## Notes / Next Steps

- Compare to `domain_audit_visual` audited renders to measure audit overhead.
