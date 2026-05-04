# Cathedral Probe Image Manifest

All images in this directory are curated copies of specific `output/` artifacts.
They are repo-tracked so documentation can embed stable relative paths.
Original outputs are never deleted — this directory is a curation layer only.

Regeneration: re-run the corresponding `scripts/run_*.sh` script and re-copy the named file.

---

## Group 1 — Six-Layer Diagnostic Overlay Stack
*Source run: `tile_commit_traversal_comparison/20260503T231337Z` · step_length=0.015 · row traversal*

| Docs asset | Source output path | Size |
|---|---|---|
| `cathedral_probe_overlay_row_0015.png` | `output/tile_commit_traversal_comparison/20260503T231337Z/beauty/step_0.015/row/combined_diagnostic_overlay.png` | 15 KB |
| `cathedral_probe_contact_sheet_row_0015.png` | `output/tile_commit_traversal_comparison/20260503T231337Z/beauty/step_0.015/row/diagnostic_overlay_contact_sheet.png` | 61 KB |
| `continuity_vectors_row_0015.png` | `output/tile_commit_traversal_comparison/20260503T231337Z/beauty/step_0.015/row/layer5_transport_continuity_vectors.png` | 5 KB |
| `transport_shape_regions_row_0015.png` | `output/tile_commit_traversal_comparison/20260503T231337Z/beauty/step_0.015/row/transport_shape_regions_overlay.png` | 4 KB |

### Captions

**`cathedral_probe_overlay_row_0015.png`**
Six-layer Cathedral Probe composite overlay. Domain resolver stress scene, step_length=0.015, row traversal. Layers: beauty render · cartesian wireframe · transport ownership map · risk probe markers · spacetime transport diagram · transport continuity vectors. Collider 25836914057 ownership boundary visible as high-density vector cluster.

**`cathedral_probe_contact_sheet_row_0015.png`**
Contact sheet of all six Cathedral Probe diagnostic layers rendered individually. Shows the progressive layering from bare beauty image through geometric structure, transport ownership, probe risk markers, and continuity vector overlay.

**`continuity_vectors_row_0015.png`**
Layer 5: Transport continuity vector field. Each vector encodes pixel-to-pixel disagreement across six transport dimensions: collider ownership, domain, hit distance, normal angle, path length, and boundary event. 6,619 high-discontinuity vectors (score ≥ 1.0). All six identified shape regions confirmed `boundary_aligns_with_high_vector_density = true`.

**`transport_shape_regions_row_0015.png`**
Transport shape region overlay identifying collider ownership contours in screen space. All regions align with high-density continuity vector clusters.

---

## Group 2 — Four-Mode Traversal Comparison
*Source run: `tile_commit_traversal_comparison/20260503T231337Z` · step_length=0.015 · row / column / tile / checkerboard*

| Docs asset | Source output path | Size |
|---|---|---|
| `traversal_contact_sheet_4mode_0015.png` | `output/tile_commit_traversal_comparison/20260503T231337Z/traversal_contact_sheet.png` | 59 KB |
| `row_vs_tile_diff_0015.png` | `output/tile_commit_traversal_comparison/20260503T231337Z/row_vs_tile_diff.png` | 2 KB |
| `row_vs_checkerboard_diff_0015.png` | `output/tile_commit_traversal_comparison/20260503T231337Z/row_vs_checkerboard_diff.png` | 2 KB |
| `band_support_by_mode_0015.png` | `output/tile_commit_traversal_comparison/20260503T231337Z/band_support_by_mode.png` | 5 KB |

### Captions

**`traversal_contact_sheet_4mode_0015.png`**
Four-mode traversal comparison contact sheet at step_length=0.015. Row (band% = 20.2%), column (band% = 10.8%), tile (band% = 10.1%), checkerboard (band% = 7.9%). Scheduler decorrelation reduces horizontal banding across all non-row modes. Corner ROI instability (precision 0.003125, 468 ownership-change samples) persists unchanged across all modes.

**`row_vs_tile_diff_0015.png`**
Pixel difference map: row traversal vs tile traversal at step_length=0.015. Differences are concentrated at transport ownership boundaries rather than distributed uniformly.

**`row_vs_checkerboard_diff_0015.png`**
Pixel difference map: row traversal vs checkerboard traversal at step_length=0.015. Shows broader spatial decorrelation than row vs tile.

**`band_support_by_mode_0015.png`**
Band support area per traversal mode at step_length=0.015. Quantifies the reduction in horizontal band coverage from row-major through increasingly decorrelated traversal patterns.

---

## Group 3 — Scheduler Resonance DOE
*Source run: `doe_scheduler_resonance/20260502T155725Z` · 56 cells · step range 0.00625–0.025 · strides 1/2/4/8*

| Docs asset | Source output path | Size |
|---|---|---|
| `scheduler_resonance_stride_plot.png` | `output/doe_scheduler_resonance/20260502T155725Z/scheduler_stride_plot.png` | 87 KB |
| `scheduler_resonance_band_score_plot.png` | `output/doe_scheduler_resonance/20260502T155725Z/horizontal_band_score_plot.png` | 86 KB |
| `scheduler_resonance_stride_heatmap.png` | `output/doe_scheduler_resonance/20260502T155725Z/band_by_row_mod_stride_heatmap.png` | 110 KB |

### Captions

**`scheduler_resonance_stride_plot.png`**
Scheduler stride sweep: band percentage vs stride at multiple step lengths. Stride 1: 19–32% across all step lengths. Stride 4: 0.2–0.7%. Stride 8: ≤0.19%. Band coverage is step-length independent at stride 1, confirming that traversal cadence — not physics precision — is the primary amplifier of row-global artifacts. 56-cell DOE, domain resolver stress scene, 320×180.

**`scheduler_resonance_band_score_plot.png`**
Horizontal band score vs step length, coloured by stride. The stride-1 line is flat across the full step range. Stride-2 shows non-monotonic sensitivity. Strides 4 and 8 collapse to noise levels regardless of step length.

**`scheduler_resonance_stride_heatmap.png`**
Band pixel count by row-modulo-stride class. At stride 1, band pixels distribute uniformly across all row-mod-stride classes, confirming global resonance. At stride 4, band pixels concentrate in a small number of residual classes, indicating localized rather than global artifact support.

---

## Group 4 — Step-Length Sensitivity DOE
*Source run: `doe_overnight/20260502T060652Z` · step range 0.00625–0.04 · default stride*

| Docs asset | Source output path | Size |
|---|---|---|
| `doe_step_sensitivity_band_plot.png` | `output/doe_overnight/20260502T060652Z/DOE_overnight_band_plot.png` | 51 KB |

### Caption

**`doe_step_sensitivity_band_plot.png`**
Band percentage vs step length at default stride (stride 1 equivalent). Banding increases from 0.3% at step 0.025 to 26.1% at step 0.00625. Non-monotonic: banding is lower at coarser step 0.015 (3.3%) than at finer step 0.00625 (26.1%). Finer step lengths expose more transport boundary structure, creating more pixels vulnerable to scheduler resonance. Refining step length alone does not reduce banding.

---

## Group 5 — Corner Transport Probe
*Source run: `corner_transport_probe/20260503T132655Z` · ROI: geometry:25836914057:edge_midpoint:6*

| Docs asset | Source output path | Size |
|---|---|---|
| `corner_collider_flip_map.png` | `output/corner_transport_probe/20260503T132655Z/corner_collider_flip_map.png` | 0.4 KB |
| `corner_required_precision_map.png` | `output/corner_transport_probe/20260503T132655Z/corner_required_precision_map.png` | 0.4 KB |
| `corner_convergence_profile.png` | `output/corner_transport_probe/20260503T132655Z/corner_convergence_profile.png` | 8 KB |

### Captions

**`corner_collider_flip_map.png`**
Corner probe: collider ownership flip map at edge ROI. Red pixels indicate locations where changing step length from 0.00625 to 0.003125 changes the recorded hit owner. 39 flips in 89 samples (44%). Local transport ambiguity is genuine and independent of traversal mode.

**`corner_required_precision_map.png`**
Corner probe: required precision map at edge ROI. All 89 sampled points require reference precision (step 0.003125) for stable transport decisions. This instability persists unchanged across row and column traversal modes.

**`corner_convergence_profile.png`**
Corner probe: convergence profile showing decision risk vs step size across the edge ROI samples. Mean maximum decision risk: 4.038819. The risk does not converge to zero at any tested step size within the ROI.

---

## Group 6 — First-Pass Traversal Comparison
*Source run: `first_pass_traversal_comparison/20260503T171942Z` · row vs column · step_length=0.015*

| Docs asset | Source output path | Size |
|---|---|---|
| `first_pass_traversal_contact_sheet.png` | `output/first_pass_traversal_comparison/20260503T171942Z/traversal_mode_contact_sheet.png` | 37 KB |
| `row_vs_column_diff.png` | `output/first_pass_traversal_comparison/20260503T171942Z/row_vs_column_diff.png` | 0.8 KB |

### Captions

**`first_pass_traversal_contact_sheet.png`**
Row vs column first-pass traversal comparison at step_length=0.015. Row: band% = 0.059%. Column: band% = 0.118%. Column traversal changed 448 pixels versus row. Traversal order affects output, but column does not suppress banding — it demonstrates that output is order-dependent without yet showing improvement.

**`row_vs_column_diff.png`**
Pixel difference map: row vs column first-pass traversal at step_length=0.015. 448 pixels differ. Differences cluster near transport ownership boundaries rather than distributing uniformly.

---

## Embedding Guide

From `Docs/index.md` or `Docs/README.md` (same directory level as `assets/`):
```markdown
![Description](assets/cathedral_probe/filename.png)
```

From `Docs/Research/` pages (one directory deeper):
```markdown
![Description](../assets/cathedral_probe/filename.png)
```

From root `README.md` (if created; `Docs/` is a subdirectory):
```markdown
![Description](Docs/assets/cathedral_probe/filename.png)
```
