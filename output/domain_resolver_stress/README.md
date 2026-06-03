# Domain Resolver Stress

Short name: **Domain Resolver — Stress Test**

## Purpose

Stress-tests the domain resolver under adversarial geometry conditions (corner intersections, overlapping boundaries, degenerate normals) to find failure modes before they appear in production scenes. Each run applies different stress configurations.

## Source / Generation Context

- Scene: `test-domain-resolver-stress.tscn`
- Multiple subfolders (five+), each with `dotnet_build.log`

## What the Output Shows

Primary artifacts are build logs confirming the resolver compiles cleanly under each configuration, plus any render output and derivative step telemetry produced by the stress scene. The stress scene is the shared workhorse for `corner_transport_probe`, `reference_geodesic_probe_smoke`, and related runs.

## Key Files

- `*/dotnet_build.log` — Build confirmation per configuration
- Any `*.png` / `*.derivative_step.json` produced by the stress runs

## Suggested MisterY Labs Card Summary

Interpretation pending — domain resolver stress test harness outputs.

## Status

Test output

## Notes / Next Steps

- This folder is a staging area for domain resolver investigations. Specific findings live in `corner_transport_probe`, `reference_geodesic_probe_smoke`, and `transport_coherence_basin_*`.
