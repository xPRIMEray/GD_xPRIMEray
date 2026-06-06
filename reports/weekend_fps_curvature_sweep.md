# Weekend FPS Curvature Sweep

Hermetic closure validates transport completion within a known scene contract. It does not establish physical correctness.

## Executive Summary

- Did it run? yes
- Did Godot exit cleanly? yes
- Did all five curvature levels complete? yes
- Did sealed-scene hit validation pass? yes
- Beauty capture status: BLANK BEAUTY
- Diagnostic artifact health: OK
- Blank beauty does not fail sealed-hit validation, but it does fail visual-render confirmation.
- Traversal budget stress: 72.7% of pixels exhausted step budget; all found hit on overrun step (budget+1); budget_exhausted_without_hit = 0
- Screenshot capture: suspected blank or unusable for visual proof; verify layer0_beauty capture
- Did resolved fixture curvature vary as requested? yes
- Are visual outputs identical across curvature levels? no
- Artifact families that changed with curvature: `curvature_field_view, curved_vs_straight_difference, diagnostic_contact_sheet, geometry_explanation, traversal_step_heatmap`
- Did FPS reach 30? no
- Did FPS reach 60? no
- Biggest bottleneck observed: pass2_phys_ms averaged 1642.430 ms across available cells.
- Visual sanity: non-identical; At least one visual artifact family changes across curvature levels; the sweep is not visually byte-identical.

Beauty capture status: BLANK BEAUTY.
The beauty frame is a valid PNG but contains only the clear/background color.
Diagnostic overlays remain valid and show sealed transport closure.
This means the benchmark currently proves traversal/evaluation behavior, but not visible beauty-layer rendering.

## Detailed Benchmark Table

| curvature % | amplitude | resolved amp | transport | mean FPS | p95 frame ms | hit % | miss count | avg traversal steps | max traversal steps | budget stress % | screenshot | visual metrics available |
|---:|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|---|---|
| 0 | 0 | 0 | off | 3.35 | 350.80 | 100.000 | 0 | 677.27 | 701† | 72.7% | [png](weekend_fps_curvature_sweep_assets/curvature_000_screenshot.png) | [raw_visual](weekend_fps_curvature_sweep_assets/curvature_000_raw_visual.png), [geometry_explanation](weekend_fps_curvature_sweep_assets/curvature_000_geometry_explanation.png), [curvature_field_view](weekend_fps_curvature_sweep_assets/curvature_000_curvature_field_view.png), [cartesian_wireframe_overlay](weekend_fps_curvature_sweep_assets/curvature_000_cartesian_wireframe_overlay.png), [normal_overlay](weekend_fps_curvature_sweep_assets/curvature_000_normal_overlay.png), [hit_miss_map](weekend_fps_curvature_sweep_assets/curvature_000_hit_miss_map.png), [traversal_step_heatmap](weekend_fps_curvature_sweep_assets/curvature_000_traversal_step_heatmap.png), [curved_vs_straight_difference](weekend_fps_curvature_sweep_assets/curvature_000_curved_vs_straight_difference.png), [budget_heatmap](weekend_fps_curvature_sweep_assets/curvature_000_budget_heatmap.png), [budget_overlay](weekend_fps_curvature_sweep_assets/curvature_000_budget_overlay.png), [diagnostic_contact_sheet](weekend_fps_curvature_sweep_assets/curvature_000_diagnostic_contact_sheet.png), [transport_ownership](weekend_fps_curvature_sweep_assets/curvature_000_transport_ownership.png), [combined_diagnostic_overlay](weekend_fps_curvature_sweep_assets/curvature_000_combined_diagnostic_overlay.png), [transport_continuity](weekend_fps_curvature_sweep_assets/curvature_000_transport_continuity.png), [ownership_seams](weekend_fps_curvature_sweep_assets/curvature_000_ownership_seams.png) |
| 25 | 0.2875 | 0.2875 | on | 3.75 | 327.42 | 100.000 | 0 | 677.27 | 701† | 72.7% | [png](weekend_fps_curvature_sweep_assets/curvature_025_screenshot.png) | [raw_visual](weekend_fps_curvature_sweep_assets/curvature_025_raw_visual.png), [geometry_explanation](weekend_fps_curvature_sweep_assets/curvature_025_geometry_explanation.png), [curvature_field_view](weekend_fps_curvature_sweep_assets/curvature_025_curvature_field_view.png), [cartesian_wireframe_overlay](weekend_fps_curvature_sweep_assets/curvature_025_cartesian_wireframe_overlay.png), [normal_overlay](weekend_fps_curvature_sweep_assets/curvature_025_normal_overlay.png), [hit_miss_map](weekend_fps_curvature_sweep_assets/curvature_025_hit_miss_map.png), [traversal_step_heatmap](weekend_fps_curvature_sweep_assets/curvature_025_traversal_step_heatmap.png), [curved_vs_straight_difference](weekend_fps_curvature_sweep_assets/curvature_025_curved_vs_straight_difference.png), [budget_heatmap](weekend_fps_curvature_sweep_assets/curvature_025_budget_heatmap.png), [budget_overlay](weekend_fps_curvature_sweep_assets/curvature_025_budget_overlay.png), [diagnostic_contact_sheet](weekend_fps_curvature_sweep_assets/curvature_025_diagnostic_contact_sheet.png), [transport_ownership](weekend_fps_curvature_sweep_assets/curvature_025_transport_ownership.png), [combined_diagnostic_overlay](weekend_fps_curvature_sweep_assets/curvature_025_combined_diagnostic_overlay.png), [transport_continuity](weekend_fps_curvature_sweep_assets/curvature_025_transport_continuity.png), [ownership_seams](weekend_fps_curvature_sweep_assets/curvature_025_ownership_seams.png) |
| 50 | 0.575 | 0.575 | on | 3.56 | 339.13 | 100.000 | 0 | 677.55 | 701† | 72.7% | [png](weekend_fps_curvature_sweep_assets/curvature_050_screenshot.png) | [raw_visual](weekend_fps_curvature_sweep_assets/curvature_050_raw_visual.png), [geometry_explanation](weekend_fps_curvature_sweep_assets/curvature_050_geometry_explanation.png), [curvature_field_view](weekend_fps_curvature_sweep_assets/curvature_050_curvature_field_view.png), [cartesian_wireframe_overlay](weekend_fps_curvature_sweep_assets/curvature_050_cartesian_wireframe_overlay.png), [normal_overlay](weekend_fps_curvature_sweep_assets/curvature_050_normal_overlay.png), [hit_miss_map](weekend_fps_curvature_sweep_assets/curvature_050_hit_miss_map.png), [traversal_step_heatmap](weekend_fps_curvature_sweep_assets/curvature_050_traversal_step_heatmap.png), [curved_vs_straight_difference](weekend_fps_curvature_sweep_assets/curvature_050_curved_vs_straight_difference.png), [budget_heatmap](weekend_fps_curvature_sweep_assets/curvature_050_budget_heatmap.png), [budget_overlay](weekend_fps_curvature_sweep_assets/curvature_050_budget_overlay.png), [diagnostic_contact_sheet](weekend_fps_curvature_sweep_assets/curvature_050_diagnostic_contact_sheet.png), [transport_ownership](weekend_fps_curvature_sweep_assets/curvature_050_transport_ownership.png), [combined_diagnostic_overlay](weekend_fps_curvature_sweep_assets/curvature_050_combined_diagnostic_overlay.png), [transport_continuity](weekend_fps_curvature_sweep_assets/curvature_050_transport_continuity.png), [ownership_seams](weekend_fps_curvature_sweep_assets/curvature_050_ownership_seams.png) |
| 75 | 0.8625 | 0.8625 | on | 3.63 | 344.15 | 100.000 | 0 | 678.09 | 701† | 72.7% | [png](weekend_fps_curvature_sweep_assets/curvature_075_screenshot.png) | [raw_visual](weekend_fps_curvature_sweep_assets/curvature_075_raw_visual.png), [geometry_explanation](weekend_fps_curvature_sweep_assets/curvature_075_geometry_explanation.png), [curvature_field_view](weekend_fps_curvature_sweep_assets/curvature_075_curvature_field_view.png), [cartesian_wireframe_overlay](weekend_fps_curvature_sweep_assets/curvature_075_cartesian_wireframe_overlay.png), [normal_overlay](weekend_fps_curvature_sweep_assets/curvature_075_normal_overlay.png), [hit_miss_map](weekend_fps_curvature_sweep_assets/curvature_075_hit_miss_map.png), [traversal_step_heatmap](weekend_fps_curvature_sweep_assets/curvature_075_traversal_step_heatmap.png), [curved_vs_straight_difference](weekend_fps_curvature_sweep_assets/curvature_075_curved_vs_straight_difference.png), [budget_heatmap](weekend_fps_curvature_sweep_assets/curvature_075_budget_heatmap.png), [budget_overlay](weekend_fps_curvature_sweep_assets/curvature_075_budget_overlay.png), [diagnostic_contact_sheet](weekend_fps_curvature_sweep_assets/curvature_075_diagnostic_contact_sheet.png), [transport_ownership](weekend_fps_curvature_sweep_assets/curvature_075_transport_ownership.png), [combined_diagnostic_overlay](weekend_fps_curvature_sweep_assets/curvature_075_combined_diagnostic_overlay.png), [transport_continuity](weekend_fps_curvature_sweep_assets/curvature_075_transport_continuity.png), [ownership_seams](weekend_fps_curvature_sweep_assets/curvature_075_ownership_seams.png) |
| 100 | 1.15 | 1.15 | on | 3.62 | 332.40 | 100.000 | 0 | 678.64 | 701† | 72.7% | [png](weekend_fps_curvature_sweep_assets/curvature_100_screenshot.png) | [raw_visual](weekend_fps_curvature_sweep_assets/curvature_100_raw_visual.png), [geometry_explanation](weekend_fps_curvature_sweep_assets/curvature_100_geometry_explanation.png), [curvature_field_view](weekend_fps_curvature_sweep_assets/curvature_100_curvature_field_view.png), [cartesian_wireframe_overlay](weekend_fps_curvature_sweep_assets/curvature_100_cartesian_wireframe_overlay.png), [normal_overlay](weekend_fps_curvature_sweep_assets/curvature_100_normal_overlay.png), [hit_miss_map](weekend_fps_curvature_sweep_assets/curvature_100_hit_miss_map.png), [traversal_step_heatmap](weekend_fps_curvature_sweep_assets/curvature_100_traversal_step_heatmap.png), [curved_vs_straight_difference](weekend_fps_curvature_sweep_assets/curvature_100_curved_vs_straight_difference.png), [budget_heatmap](weekend_fps_curvature_sweep_assets/curvature_100_budget_heatmap.png), [budget_overlay](weekend_fps_curvature_sweep_assets/curvature_100_budget_overlay.png), [diagnostic_contact_sheet](weekend_fps_curvature_sweep_assets/curvature_100_diagnostic_contact_sheet.png), [transport_ownership](weekend_fps_curvature_sweep_assets/curvature_100_transport_ownership.png), [combined_diagnostic_overlay](weekend_fps_curvature_sweep_assets/curvature_100_combined_diagnostic_overlay.png), [transport_continuity](weekend_fps_curvature_sweep_assets/curvature_100_transport_continuity.png), [ownership_seams](weekend_fps_curvature_sweep_assets/curvature_100_ownership_seams.png) |

> † overrun step — loop condition `s <= maxIntegrationSteps` allows one extra iteration past step budget. All overrun pixels found a hit; budget_exhausted_without_hit = 0.

## Visual Identity

- Non-identical artifact families: `curvature_field_view, curved_vs_straight_difference, diagnostic_contact_sheet, geometry_explanation, traversal_step_heatmap`
- Identical artifact families: `budget_heatmap, budget_overlay, cartesian_wireframe_overlay, combined_diagnostic_overlay, hit_miss_map, normal_overlay, ownership_seams, raw_visual, screenshot, transport_continuity, transport_ownership`
- Missing artifact families: `none`

## Artifact Health

- beauty_capture_health: `BLANK BEAUTY`
- diagnostic_artifact_health: `OK`
- visual_render_confirmation_passed: `false`
- diagnostic_artifacts_valid: `true`
  - Note: `transport_continuity` is curvature-invariant in this run. This overlay likely renders fixed-geometry transport paths rather than field-bent integration curves. Confirm it consumes curved transport data if curvature sensitivity is required.

## Diagnostic Layers

The contact sheet is an Observatory Story: read left-to-right as a sequence of questions.

- Raw visual: `layer0_beauty.png` / screenshot capture, reported separately as `beauty_capture_health`.
- Geometry explanation: `cartesian_scene_geometry.png` shows sealed room bounds, receiver surfaces, camera/ray origin, and field volume outline.
- Curvature field: `curvature_field_view.png` shows field bounds, center, resolved amplitude, and whether curved transport was enabled.
- Transport diagnostics: ownership regions, normal vectors, transport continuity, and combined diagnostic overlays.
- Closure diagnostics: hit/miss maps, hit counts, miss counts, miss rate, and hermetic closure summaries.
- Budget/precision diagnostics: traversal-step heatmaps, budget stress maps, and precision/epsilon warnings.
- Curved-vs-straight difference: `curved_vs_straight_difference.png` compares traversal-step cost against the 0% baseline; 0% is labeled as the baseline reference.
- Contact-sheet rule: title and caption bands are outside the image canvas; rendered/source pixels are not annotated by the sheet itself.

## Hardware

- platform: `Linux-6.6.87.2-microsoft-standard-WSL2-x86_64-with-glibc2.39`
- processor: `x86_64`
- python: `3.12.3`
- cpu(s): `24`
- model_name: `AMD Ryzen 9 7900X 12-Core Processor`
- thread(s)_per_core: `2`
- core(s)_per_socket: `12`

## Notes

- Primary gate: hermetic sealed-room hit closure.
- Optional ownership, oracle, island, and cathedral-style diagnostics are report attachments only when existing tools produce them.
- Raw output root: `/home/bb/code/godot_xPRIMEray/output/curvature_fps_benchmark/20260606T014236Z`
