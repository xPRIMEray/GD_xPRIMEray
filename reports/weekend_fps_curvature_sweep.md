# Weekend FPS Curvature Sweep

Hermetic closure validates transport completion within a known scene contract. It does not establish physical correctness.

## Executive Summary

- Did it run? yes
- Did Godot exit cleanly? yes
- Did all five curvature levels complete? yes
- Did sealed-scene hit validation pass? yes
- Did FPS reach 30? no
- Did FPS reach 60? no
- Biggest bottleneck observed: pass2_phys_ms averaged 1984.615 ms across available cells.

## Detailed Benchmark Table

| curvature % | amplitude | mean FPS | p95 frame ms | hit % | miss count | avg traversal steps | max traversal steps | screenshot | visual metrics available |
|---:|---:|---:|---:|---:|---:|---:|---:|---|---|
| 0 | 0 | 3.21 | 367.62 | 100.000 | 0 | 677.27 | 701 | [png](weekend_fps_curvature_sweep_assets/curvature_000_screenshot.png) | [normal_overlay](weekend_fps_curvature_sweep_assets/curvature_000_normal_overlay.png), [budget_heatmap](weekend_fps_curvature_sweep_assets/curvature_000_budget_heatmap.png), [budget_overlay](weekend_fps_curvature_sweep_assets/curvature_000_budget_overlay.png), [diagnostic_contact_sheet](weekend_fps_curvature_sweep_assets/curvature_000_diagnostic_contact_sheet.png), [combined_diagnostic_overlay](weekend_fps_curvature_sweep_assets/curvature_000_combined_diagnostic_overlay.png), [transport_continuity](weekend_fps_curvature_sweep_assets/curvature_000_transport_continuity.png), [ownership_seams](weekend_fps_curvature_sweep_assets/curvature_000_ownership_seams.png), [hit_miss_map](weekend_fps_curvature_sweep_assets/curvature_000_hit_miss_map.png), [traversal_step_heatmap](weekend_fps_curvature_sweep_assets/curvature_000_traversal_step_heatmap.png) |
| 25 | 0.2875 | 3.23 | 355.94 | 100.000 | 0 | 677.27 | 701 | [png](weekend_fps_curvature_sweep_assets/curvature_025_screenshot.png) | [normal_overlay](weekend_fps_curvature_sweep_assets/curvature_025_normal_overlay.png), [budget_heatmap](weekend_fps_curvature_sweep_assets/curvature_025_budget_heatmap.png), [budget_overlay](weekend_fps_curvature_sweep_assets/curvature_025_budget_overlay.png), [diagnostic_contact_sheet](weekend_fps_curvature_sweep_assets/curvature_025_diagnostic_contact_sheet.png), [combined_diagnostic_overlay](weekend_fps_curvature_sweep_assets/curvature_025_combined_diagnostic_overlay.png), [transport_continuity](weekend_fps_curvature_sweep_assets/curvature_025_transport_continuity.png), [ownership_seams](weekend_fps_curvature_sweep_assets/curvature_025_ownership_seams.png), [hit_miss_map](weekend_fps_curvature_sweep_assets/curvature_025_hit_miss_map.png), [traversal_step_heatmap](weekend_fps_curvature_sweep_assets/curvature_025_traversal_step_heatmap.png) |
| 50 | 0.575 | 3.26 | 359.76 | 100.000 | 0 | 677.27 | 701 | [png](weekend_fps_curvature_sweep_assets/curvature_050_screenshot.png) | [normal_overlay](weekend_fps_curvature_sweep_assets/curvature_050_normal_overlay.png), [budget_heatmap](weekend_fps_curvature_sweep_assets/curvature_050_budget_heatmap.png), [budget_overlay](weekend_fps_curvature_sweep_assets/curvature_050_budget_overlay.png), [diagnostic_contact_sheet](weekend_fps_curvature_sweep_assets/curvature_050_diagnostic_contact_sheet.png), [combined_diagnostic_overlay](weekend_fps_curvature_sweep_assets/curvature_050_combined_diagnostic_overlay.png), [transport_continuity](weekend_fps_curvature_sweep_assets/curvature_050_transport_continuity.png), [ownership_seams](weekend_fps_curvature_sweep_assets/curvature_050_ownership_seams.png), [hit_miss_map](weekend_fps_curvature_sweep_assets/curvature_050_hit_miss_map.png), [traversal_step_heatmap](weekend_fps_curvature_sweep_assets/curvature_050_traversal_step_heatmap.png) |
| 75 | 0.8625 | 3.25 | 372.34 | 100.000 | 0 | 677.27 | 701 | [png](weekend_fps_curvature_sweep_assets/curvature_075_screenshot.png) | [normal_overlay](weekend_fps_curvature_sweep_assets/curvature_075_normal_overlay.png), [budget_heatmap](weekend_fps_curvature_sweep_assets/curvature_075_budget_heatmap.png), [budget_overlay](weekend_fps_curvature_sweep_assets/curvature_075_budget_overlay.png), [diagnostic_contact_sheet](weekend_fps_curvature_sweep_assets/curvature_075_diagnostic_contact_sheet.png), [combined_diagnostic_overlay](weekend_fps_curvature_sweep_assets/curvature_075_combined_diagnostic_overlay.png), [transport_continuity](weekend_fps_curvature_sweep_assets/curvature_075_transport_continuity.png), [ownership_seams](weekend_fps_curvature_sweep_assets/curvature_075_ownership_seams.png), [hit_miss_map](weekend_fps_curvature_sweep_assets/curvature_075_hit_miss_map.png), [traversal_step_heatmap](weekend_fps_curvature_sweep_assets/curvature_075_traversal_step_heatmap.png) |
| 100 | 1.15 | 3.30 | 345.23 | 100.000 | 0 | 677.27 | 701 | [png](weekend_fps_curvature_sweep_assets/curvature_100_screenshot.png) | [normal_overlay](weekend_fps_curvature_sweep_assets/curvature_100_normal_overlay.png), [budget_heatmap](weekend_fps_curvature_sweep_assets/curvature_100_budget_heatmap.png), [budget_overlay](weekend_fps_curvature_sweep_assets/curvature_100_budget_overlay.png), [diagnostic_contact_sheet](weekend_fps_curvature_sweep_assets/curvature_100_diagnostic_contact_sheet.png), [combined_diagnostic_overlay](weekend_fps_curvature_sweep_assets/curvature_100_combined_diagnostic_overlay.png), [transport_continuity](weekend_fps_curvature_sweep_assets/curvature_100_transport_continuity.png), [ownership_seams](weekend_fps_curvature_sweep_assets/curvature_100_ownership_seams.png), [hit_miss_map](weekend_fps_curvature_sweep_assets/curvature_100_hit_miss_map.png), [traversal_step_heatmap](weekend_fps_curvature_sweep_assets/curvature_100_traversal_step_heatmap.png) |

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
- Raw output root: `/home/bb/code/godot_xPRIMEray/output/curvature_fps_benchmark/20260605T192555Z`
