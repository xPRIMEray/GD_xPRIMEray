# Weekend FPS Curvature Sweep

Hermetic closure validates transport completion within a known scene contract. It does not establish physical correctness.

## Executive Summary

- Did it run? yes
- Did Godot exit cleanly? yes
- Did all five curvature levels complete? yes
- Did sealed transport closure pass? yes — 0 misses across all traced pixels
- Visual render confirmation: yes
- Run scale: mini (160×112) — visual sanity
- Render mode: partial-frame diagnostic snapshot (37.7% traced minimum)
- Coverage: traced 37.7%, beauty written 24.6% minimum across cells
- Closure hit rate is measured over traced pixels only. Coverage metrics measure how many of the film's total pixels have been processed in this partial-frame snapshot; they are orthogonal dimensions.
- Traced = film pixels that have had rays fired through them. Beauty written = film pixels whose final color has been written to the beauty buffer at least once.
- Full-frame coverage: partial
- Traversal budget stress: 30.9% of pixels exhausted step budget; all found hit on overrun step (budget+1); budget_exhausted_without_hit = 0
- Diagnostic artifact health: OK
- Did resolved fixture curvature vary as requested? yes
- Are visual outputs identical across curvature levels? no
- Artifact families that changed with curvature: `budget_heatmap, budget_overlay, cartesian_wireframe_overlay, combined_diagnostic_overlay, curvature_field_view, curvature_signature, diagnostic_contact_sheet, frame_coverage_map, geometry_explanation, hit_miss_map, normal_overlay, ownership_seams, raw_visual, screenshot, transport_continuity, transport_ownership, traversal_step_heatmap`
- Contact sheet layout: `storyboard` (9 columns x 1 rows, row-major order)
- Did mean diagnostic FPS reach 30? no
- Did mean diagnostic FPS reach 60? no
- Biggest bottleneck observed: No perf-stage timings were available.
- This run measures diagnostic throughput, not full-frame rendered FPS.
- Mean diagnostic FPS is frames per second in partial-frame diagnostic mode; each frame may process only a subset of film pixels, so it is not production rendering speed.

## Detailed Benchmark Table

| curvature % | amplitude | resolved amp | transport | mean diagnostic FPS | p95 frame ms | traced % | beauty written % | hit % | miss count | avg traversal steps | max traversal steps | budget stress % | screenshot | visual metrics available |
|---:|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|---|
| 0 | 0 | 0 | off | 0.56 | 1827.23 | 37.7 | 24.6 | 100.000 | 0 | 683.94 | 701† | 30.5% | [png](weekend_fps_curvature_sweep_assets/curvature_000_screenshot.png) | [raw_visual](weekend_fps_curvature_sweep_assets/curvature_000_raw_visual.png), [geometry_explanation](weekend_fps_curvature_sweep_assets/curvature_000_geometry_explanation.png), [curvature_field_view](weekend_fps_curvature_sweep_assets/curvature_000_curvature_field_view.png), [cartesian_wireframe_overlay](weekend_fps_curvature_sweep_assets/curvature_000_cartesian_wireframe_overlay.png), [normal_overlay](weekend_fps_curvature_sweep_assets/curvature_000_normal_overlay.png), [hit_miss_map](weekend_fps_curvature_sweep_assets/curvature_000_hit_miss_map.png), [frame_coverage_map](weekend_fps_curvature_sweep_assets/curvature_000_frame_coverage_map.png), [traversal_step_heatmap](weekend_fps_curvature_sweep_assets/curvature_000_traversal_step_heatmap.png), [curvature_signature](weekend_fps_curvature_sweep_assets/curvature_000_curvature_signature.png), [budget_heatmap](weekend_fps_curvature_sweep_assets/curvature_000_budget_heatmap.png), [budget_overlay](weekend_fps_curvature_sweep_assets/curvature_000_budget_overlay.png), [diagnostic_contact_sheet](weekend_fps_curvature_sweep_assets/curvature_000_diagnostic_contact_sheet.png), [transport_ownership](weekend_fps_curvature_sweep_assets/curvature_000_transport_ownership.png), [combined_diagnostic_overlay](weekend_fps_curvature_sweep_assets/curvature_000_combined_diagnostic_overlay.png), [transport_continuity](weekend_fps_curvature_sweep_assets/curvature_000_transport_continuity.png), [ownership_seams](weekend_fps_curvature_sweep_assets/curvature_000_ownership_seams.png) |
| 25 | 0.2875 | 0.2875 | on | 0.61 | 1777.29 | 38.0 | 24.6 | 100.000 | 0 | 684.29 | 701† | 30.9% | [png](weekend_fps_curvature_sweep_assets/curvature_025_screenshot.png) | [raw_visual](weekend_fps_curvature_sweep_assets/curvature_025_raw_visual.png), [geometry_explanation](weekend_fps_curvature_sweep_assets/curvature_025_geometry_explanation.png), [curvature_field_view](weekend_fps_curvature_sweep_assets/curvature_025_curvature_field_view.png), [cartesian_wireframe_overlay](weekend_fps_curvature_sweep_assets/curvature_025_cartesian_wireframe_overlay.png), [normal_overlay](weekend_fps_curvature_sweep_assets/curvature_025_normal_overlay.png), [hit_miss_map](weekend_fps_curvature_sweep_assets/curvature_025_hit_miss_map.png), [frame_coverage_map](weekend_fps_curvature_sweep_assets/curvature_025_frame_coverage_map.png), [traversal_step_heatmap](weekend_fps_curvature_sweep_assets/curvature_025_traversal_step_heatmap.png), [curvature_signature](weekend_fps_curvature_sweep_assets/curvature_025_curvature_signature.png), [budget_heatmap](weekend_fps_curvature_sweep_assets/curvature_025_budget_heatmap.png), [budget_overlay](weekend_fps_curvature_sweep_assets/curvature_025_budget_overlay.png), [diagnostic_contact_sheet](weekend_fps_curvature_sweep_assets/curvature_025_diagnostic_contact_sheet.png), [transport_ownership](weekend_fps_curvature_sweep_assets/curvature_025_transport_ownership.png), [combined_diagnostic_overlay](weekend_fps_curvature_sweep_assets/curvature_025_combined_diagnostic_overlay.png), [transport_continuity](weekend_fps_curvature_sweep_assets/curvature_025_transport_continuity.png), [ownership_seams](weekend_fps_curvature_sweep_assets/curvature_025_ownership_seams.png) |
| 50 | 0.575 | 0.575 | on | 0.65 | 1681.06 | 38.2 | 24.6 | 100.000 | 0 | 684.46 | 701† | 31.1% | [png](weekend_fps_curvature_sweep_assets/curvature_050_screenshot.png) | [raw_visual](weekend_fps_curvature_sweep_assets/curvature_050_raw_visual.png), [geometry_explanation](weekend_fps_curvature_sweep_assets/curvature_050_geometry_explanation.png), [curvature_field_view](weekend_fps_curvature_sweep_assets/curvature_050_curvature_field_view.png), [cartesian_wireframe_overlay](weekend_fps_curvature_sweep_assets/curvature_050_cartesian_wireframe_overlay.png), [normal_overlay](weekend_fps_curvature_sweep_assets/curvature_050_normal_overlay.png), [hit_miss_map](weekend_fps_curvature_sweep_assets/curvature_050_hit_miss_map.png), [frame_coverage_map](weekend_fps_curvature_sweep_assets/curvature_050_frame_coverage_map.png), [traversal_step_heatmap](weekend_fps_curvature_sweep_assets/curvature_050_traversal_step_heatmap.png), [curvature_signature](weekend_fps_curvature_sweep_assets/curvature_050_curvature_signature.png), [budget_heatmap](weekend_fps_curvature_sweep_assets/curvature_050_budget_heatmap.png), [budget_overlay](weekend_fps_curvature_sweep_assets/curvature_050_budget_overlay.png), [diagnostic_contact_sheet](weekend_fps_curvature_sweep_assets/curvature_050_diagnostic_contact_sheet.png), [transport_ownership](weekend_fps_curvature_sweep_assets/curvature_050_transport_ownership.png), [combined_diagnostic_overlay](weekend_fps_curvature_sweep_assets/curvature_050_combined_diagnostic_overlay.png), [transport_continuity](weekend_fps_curvature_sweep_assets/curvature_050_transport_continuity.png), [ownership_seams](weekend_fps_curvature_sweep_assets/curvature_050_ownership_seams.png) |
| 75 | 0.8625 | 0.8625 | on | 0.61 | 1708.42 | 38.0 | 24.6 | 100.000 | 0 | 684.66 | 701† | 30.9% | [png](weekend_fps_curvature_sweep_assets/curvature_075_screenshot.png) | [raw_visual](weekend_fps_curvature_sweep_assets/curvature_075_raw_visual.png), [geometry_explanation](weekend_fps_curvature_sweep_assets/curvature_075_geometry_explanation.png), [curvature_field_view](weekend_fps_curvature_sweep_assets/curvature_075_curvature_field_view.png), [cartesian_wireframe_overlay](weekend_fps_curvature_sweep_assets/curvature_075_cartesian_wireframe_overlay.png), [normal_overlay](weekend_fps_curvature_sweep_assets/curvature_075_normal_overlay.png), [hit_miss_map](weekend_fps_curvature_sweep_assets/curvature_075_hit_miss_map.png), [frame_coverage_map](weekend_fps_curvature_sweep_assets/curvature_075_frame_coverage_map.png), [traversal_step_heatmap](weekend_fps_curvature_sweep_assets/curvature_075_traversal_step_heatmap.png), [curvature_signature](weekend_fps_curvature_sweep_assets/curvature_075_curvature_signature.png), [budget_heatmap](weekend_fps_curvature_sweep_assets/curvature_075_budget_heatmap.png), [budget_overlay](weekend_fps_curvature_sweep_assets/curvature_075_budget_overlay.png), [diagnostic_contact_sheet](weekend_fps_curvature_sweep_assets/curvature_075_diagnostic_contact_sheet.png), [transport_ownership](weekend_fps_curvature_sweep_assets/curvature_075_transport_ownership.png), [combined_diagnostic_overlay](weekend_fps_curvature_sweep_assets/curvature_075_combined_diagnostic_overlay.png), [transport_continuity](weekend_fps_curvature_sweep_assets/curvature_075_transport_continuity.png), [ownership_seams](weekend_fps_curvature_sweep_assets/curvature_075_ownership_seams.png) |
| 100 | 1.15 | 1.15 | on | 0.58 | 1801.37 | 38.2 | 24.6 | 100.000 | 0 | 685.21 | 701† | 31.1% | [png](weekend_fps_curvature_sweep_assets/curvature_100_screenshot.png) | [raw_visual](weekend_fps_curvature_sweep_assets/curvature_100_raw_visual.png), [geometry_explanation](weekend_fps_curvature_sweep_assets/curvature_100_geometry_explanation.png), [curvature_field_view](weekend_fps_curvature_sweep_assets/curvature_100_curvature_field_view.png), [cartesian_wireframe_overlay](weekend_fps_curvature_sweep_assets/curvature_100_cartesian_wireframe_overlay.png), [normal_overlay](weekend_fps_curvature_sweep_assets/curvature_100_normal_overlay.png), [hit_miss_map](weekend_fps_curvature_sweep_assets/curvature_100_hit_miss_map.png), [frame_coverage_map](weekend_fps_curvature_sweep_assets/curvature_100_frame_coverage_map.png), [traversal_step_heatmap](weekend_fps_curvature_sweep_assets/curvature_100_traversal_step_heatmap.png), [curvature_signature](weekend_fps_curvature_sweep_assets/curvature_100_curvature_signature.png), [budget_heatmap](weekend_fps_curvature_sweep_assets/curvature_100_budget_heatmap.png), [budget_overlay](weekend_fps_curvature_sweep_assets/curvature_100_budget_overlay.png), [diagnostic_contact_sheet](weekend_fps_curvature_sweep_assets/curvature_100_diagnostic_contact_sheet.png), [transport_ownership](weekend_fps_curvature_sweep_assets/curvature_100_transport_ownership.png), [combined_diagnostic_overlay](weekend_fps_curvature_sweep_assets/curvature_100_combined_diagnostic_overlay.png), [transport_continuity](weekend_fps_curvature_sweep_assets/curvature_100_transport_continuity.png), [ownership_seams](weekend_fps_curvature_sweep_assets/curvature_100_ownership_seams.png) |

> † overrun step — loop condition `s <= maxIntegrationSteps` allows one extra iteration past step budget. All overrun pixels found a hit; budget_exhausted_without_hit = 0.

## Visual Identity

- Non-identical artifact families: `budget_heatmap, budget_overlay, cartesian_wireframe_overlay, combined_diagnostic_overlay, curvature_field_view, curvature_signature, diagnostic_contact_sheet, frame_coverage_map, geometry_explanation, hit_miss_map, normal_overlay, ownership_seams, raw_visual, screenshot, transport_continuity, transport_ownership, traversal_step_heatmap`
- Identical artifact families: `none`
- Missing artifact families: `none`

## Artifact Health

- beauty_capture_health: `OK`
- diagnostic_artifact_health: `OK`
- visual_render_confirmation_required: `true` (mini preset)
- visual_render_confirmation_passed: `true`
- diagnostic_artifacts_valid: `true`
  - Note: `geometry_explanation` (cartesian_scene_geometry.png) varies because it embeds a field-activation glyph — absent at 0%, present at 25–100%. Room geometry is fixed; only the field-circle overlay changes. Expected behavior.

## Diagnostic Layers

The contact sheet is an Observatory Story: read left-to-right as a sequence of nine questions.
Selected layout: `storyboard` (9 columns x 1 rows). Reading order remains row-major: 1 -> 2 -> 3, then 4 -> 5 -> 6, then 7 -> 8 -> 9 in square mode.

1. **Raw visual** — Q: What did the camera actually see? Academic: final beauty/render output. Reported as `beauty_capture_health`.
2. **Scene geometry** — Q: What objects exist in the scene? Academic: Cartesian object/receiver geometry (sealed room bounds, surfaces, ray origin, field volume).
3. **Curvature field** — Q: What field is bending the rays? Academic: field-source volume and resolved amplitude; shows whether curved transport was enabled.
4. **Transport ownership** — Q: Where did each ray end up? Academic: receiver/domain ownership (territory map — which zone claimed each incoming ray).
5. **Hit/miss map** — Q: Did every ray find a target? Academic: hermetic closure validation (green = hit, orange = budget-exhausted hit, red = miss).
6. **Traversal steps** — Q: How hard was the trip? Academic: per-pixel integration/traversal cost (traffic/congestion map).
7. **Budget stress** — Q: Which rays nearly ran out of budget? Academic: max-step / overrun-step stress (fuel warning light).
8. **Combined diagnostic** — Q: What do all diagnostics look like together? Academic: composite diagnostic overlay (mission-control dashboard).
9. **Curvature signature** — Difference relative to 0% baseline. Q: What changed when curvature was activated? Academic: per-pixel traversal-step delta relative to 0% baseline; color encodes magnitude and sign of change. Analogy: weather-change map. 0% cell is the baseline reference.

Contact-sheet rule: title and caption bands are outside the image canvas; rendered/source pixels are not annotated by the sheet itself.

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
- Raw output root: `/home/bb/code/godot_xPRIMEray/output/curvature_fps_benchmark/20260607T185034Z`
- Curvature Signature Ladder: [curvature_signature_ladder.png](weekend_fps_curvature_sweep_assets/curvature_signature_ladder.png)
