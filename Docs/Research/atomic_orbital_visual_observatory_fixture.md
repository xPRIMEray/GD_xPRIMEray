# Atomic Orbital Visual Observatory Fixture

`atomic_orbital_visual_observatory` is a deterministic visual fixture for human interpretation of a macro-scaled hydrogen-like GRIN field. It is not a closure-validation fixture and must not be used as pass/fail proof of transport closure.

The closure-validation atomic fixture remains `atomic_orbital_grin_room`.

## Purpose

- Reveal hydrogen orbital GRIN curvature as visible transport deformation.
- Compare static, exaggerated, and clocked hydrogen captures.
- Produce beauty-image temporal diffs for tick-to-tick interpretation.
- Keep visual interpretation artifacts separate from validation gates.

## Geometry Contract

The render-test film sees intentional raytrace reference surfaces: a centered rear wall, full-frame grid bars, central target/crosshair geometry, near-field vertical poles, floor/side references, and the central core marker. These are allowed in `raytrace_geometry` because they are the visual targets whose deformation is being interpreted.

Non-raytrace guide geometry is rooted under:

- `VisualGuidesRoot`
- `DensityMarkersRoot`
- `LaserGuidesRoot`
- `BeamGuidesRoot`

Each root is in the `visual_only` group. `GodotAdapter.SnapshotBuilder` excludes `visual_only` nodes and descendants from field/geometry snapshots, so these guides cannot enter the raytrace hit set. They must not have collision objects and must not use `raytrace_geometry`, `fixture_geometry`, `fixture_background`, or receiver groups.

## Visual Defaults

- fixture token: `atomic_orbital_visual_observatory`
- aliases: `atomic_visual_observatory`, `atomic_visual`
- default render size: `640x360`
- default preset: `hydrogen`
- default electron count: `1`
- default orbital radius: `8.0`
- default curvature strength: `0.05`
- default modulation depth: `0.35`
- default capture stride: `2`
- primary visual mode: existing `normal_rgb`
- diagnostic visual mode: existing `depth_heatmap`

Curvature strengths above `0.1` are clamped with a warning unless `--atomic-visual-allow-extreme=1` is passed.

## Capture Script

Run:

```bash
bash scripts/run_atomic_orbital_visual_observatory.sh
```

Smoke:

```bash
ATOMIC_ORBITAL_VISUAL_SMOKE=1 bash scripts/run_atomic_orbital_visual_observatory.sh
```

The script captures each cell in both `normal_rgb` and `depth_heatmap`:

- `V0_baseline_no_field`
- `V1_static_hydrogen`
- `V2_exaggerated_hydrogen`
- `V3_tick0`
- `V4_tick1`

It emits `atomic_visual_observatory_report.md`, `atomic_visual_contact_sheet.png` when Pillow is available, normal-rgb beauty diffs (`tick0_vs_tick1_scaled_diff.png`, `V0_vs_V1_scaled_diff.png`, `V0_vs_V2_scaled_diff.png`), and `atomic_visual_diff_summary.json`.

All metrics are descriptive only and are not validation gates.
