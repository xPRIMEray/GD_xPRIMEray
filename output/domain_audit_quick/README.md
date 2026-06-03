# Domain Audit Quick

Short name: **Domain Audit Quick — Telemetry Smoke**

## Purpose

Quick validation of domain audit telemetry: confirms that enabling telemetry produces the expected domain ID and normal discontinuity overlay images without breaking the render. Three quick runs using the curved minimal scene.

## Source / Generation Context

- Script: `scripts/run_domain_audit_quick.sh`
- Scene: `test-curved-minimal.tscn`
- Runs: three timestamped under `output/domain_audit_quick/`

## What the Output Shows

Each run produces: a rendered PNG, a `domain_id.png` overlay (per-pixel domain ID color), a `normal_discontinuity.png` overlay (edges where normal changes sharply), and a `domain_telemetry_summary.json`. `effective_status.txt` records pass/fail. These confirm the telemetry overlay pipeline is wired correctly.

## Key Files

- `*domain_id.png` — Per-pixel domain ID overlay
- `*normal_discontinuity.png` — Normal discontinuity edge map
- `*domain_telemetry_summary.json` — Domain telemetry summary
- `effective_status.txt` — Pass/fail
- `dotnet_build.log` — Build log (confirms clean build)

## Suggested MisterY Labs Card Summary

Quick smoke validation that xPRIMEray's domain telemetry overlay pipeline produces correct domain ID and normal discontinuity images. The domain ID overlay color-codes each pixel by which geometric region the ray resolved to — a key diagnostic for transport correctness.

## Status

Test output

## Notes / Next Steps

- Superseded by `domain_audit_visual` for full visual comparison. Use these for regression smoke only.
