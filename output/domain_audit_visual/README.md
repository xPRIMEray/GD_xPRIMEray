# Domain Audit Visual

Short name: **Domain Audit — Visual Comparison Suite**

## Purpose

Full visual comparison of domain audit on vs. off: side-by-side contact sheets and diff heatmaps showing what the domain telemetry overlays reveal vs. the clean render. Nine runs across multiple audit configurations.

## Source / Generation Context

- Script: `scripts/run_domain_audit_visual.sh`
- Scene: `test-curved-minimal.tscn`
- Runs: eight timestamped under `output/domain_audit_visual/` (late April – May 2026)

## What the Output Shows

Each run produces: a `contact_sheet.png` with baseline, telemetry, and domain-ID panels side-by-side; `off_vs_tel_diff_heatmap.png` showing pixel differences introduced by telemetry; `resolver_diff.png` showing domain resolver changes. Together they confirm that telemetry overlays are additive (don't corrupt the base image) and that the domain ID correctly partitions the scene.

## Key Files

- `*/contact_sheet.png` — Multi-panel comparison sheet
- `*/off_vs_tel_diff_heatmap.png` — Diff: telemetry vs. baseline
- `*/resolver_diff.png` — Domain resolver diff
- `*/dotnet_build.log` — Build confirmation

## Suggested MisterY Labs Card Summary

A nine-run visual suite comparing xPRIMEray renders with domain auditing on vs. off. Contact sheets and pixel-diff heatmaps confirm that the domain ID and normal discontinuity overlays add diagnostic information without corrupting the base image.

## Status

Visual reference

## Notes / Next Steps

- Select best contact sheet for site documentation of the telemetry system.
- Cross-reference with `domain_telemetry_validation` for the quantitative companion.
