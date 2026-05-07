# Transport Islands Image Manifest

All images in this directory are curated copies of specific `output/` artifacts.
They are repo-tracked so documentation can embed stable relative paths.
Original outputs are never deleted — this directory is a curation layer only.

Regeneration: re-run the corresponding `scripts/run_*.sh` script and re-copy the named file.

---

## Group 1 — Island Microscopy: Dense Patch Run
*Source run: `reference_transport_oracle_unresolved_island/20260506T035920Z` · patch x=36..44, y=31..37 · 17×17 sampling · oracle step 0.0015625*

| Docs asset | Source output path | Size |
|---|---|---|
| `island_diagnostic_contact_sheet.png` | `output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/diagnostic_overlay_contact_sheet.png` | 40 KB |
| `island_combined_diagnostic_overlay.png` | `output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/combined_diagnostic_overlay.png` | 8.7 KB |
| `island_convergence_ladder.png` | `output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/island_convergence_ladder.png` | 4.2 KB |
| `island_epsilon_stability_map.png` | `output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/epsilon_stability_map.png` | 4.1 KB |
| `island_first_stable_step_map.png` | `output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/first_stable_step_map.png` | 5.1 KB |
| `island_decision_risk_gradient.png` | `output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/decision_risk_gradient.png` | 5.7 KB |
| `island_ownership_transition_map.png` | `output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/ownership_transition_map.png` | 5.0 KB |
| `island_production_vs_oracle_diff.png` | `output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/production_vs_oracle_diff.png` | 3.1 KB |
| `island_precision_cost_curves.png` | `output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/precision_cost_curves.png` | 8.1 KB |
| `island_oracle_path_overlay.png` | `output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/oracle_path_overlay.png` | 3.2 KB |
| `island_parent_trajectory_contact_sheet.png` | `output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/parent_trajectory_contact_sheet.png` | 42 KB |
| `island_continuity_vectors.png` | `output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/layer5_transport_continuity_vectors.png` | 5.3 KB |
| `island_normal_angle_delta_map.png` | `output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/normal_angle_delta_map.png` | 4.9 KB |
| `island_path_length_delta_map.png` | `output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/path_length_delta_map.png` | 5.2 KB |

### Captions

**`island_diagnostic_contact_sheet.png`**
Six-layer diagnostic contact sheet for the upper-left corner island patch (x=36..44, y=31..37). Layers: beauty render · cartesian wireframe · transport ownership map · risk probe markers · spacetime transport diagram · transport continuity vectors. All 289 sampled pixels sealed at step 0.00625; 0 unresolved at 0.003125. Oracle replay failures: 0.

**`island_combined_diagnostic_overlay.png`**
Composite diagnostic overlay of all six Cathedral Probe layers over the island patch. Shows collider ownership boundaries, risk probe density at the edge midpoint, and transport continuity vector concentration at the seam.

**`island_convergence_ladder.png`**
Convergence ladder for the dense island patch. X-axis: production step length (0.02 → 0.01 → 0.00625 → 0.003125). Y-axis: fraction of pixels achieving `EpsilonStabilityClass.Stable`. All 289 pixels reach Stable by step 0.00625. The ladder shape shows the island's spatial gradient: border pixels stabilize at 0.014–0.015, interior pixels require 0.018–0.02.

**`island_epsilon_stability_map.png`**
Per-pixel `EpsilonStabilityClass` map at production step 0.00625 for the 17×17 patch. All pixels: Stable (green). No unresolved or multi-solution pixels remain at this step.

**`island_first_stable_step_map.png`**
Per-pixel map of the first production step at which each pixel achieves `EpsilonStabilityClass.Stable`. Color encodes step value: cooler = stabilizes at coarser step (0.014–0.016, island border); warmer = requires finer step (0.018–0.02, island interior). Distribution: 0.014(12px), 0.015(33px), 0.016(47px), 0.018(59px), 0.02(138px).

**`island_decision_risk_gradient.png`**
Per-pixel decision risk gradient across the island patch. Decision risk is a composite scalar from collider ownership, domain, normal angle, hit distance, path length, and boundary event deltas between production and oracle. Mean delta between step 0.00625 and step 0.003125: 0.000189. Max: 0.000691 — numerical noise, not structural instability.

**`island_ownership_transition_map.png`**
Transport ownership transition map showing collider and domain assignment changes across the island patch at successive step lengths. Ownership boundary is localized to the seam between the main collider and edge midpoint collider 25836914057. The transition boundary is sub-pixel in width at step 0.00625.

**`island_production_vs_oracle_diff.png`**
Pixel difference map: production step 0.02 vs oracle step 0.0015625. Differences concentrate at the transport ownership seam. Pixels matching oracle (collider, domain, normal, path) appear dark; seam pixels appear bright. The diff map is a spatial fingerprint of the topological transition.

**`island_precision_cost_curves.png`**
Precision cost curves: decision risk vs step length for island pixels. Each curve is one pixel; color encodes the pixel's first-stable step. Curves converge to the noise floor (decision risk < 0.001) by step 0.00625. The family of curves shows a smooth descent — no multi-solution pathology detected.

**`island_oracle_path_overlay.png`**
Oracle path polyline overlay for a representative island pixel. Shows the null-geodesic trajectory at oracle step 0.0015625 approaching the ownership seam. The approach geometry explains why production step 0.02 overshoots the seam and production step 0.00625 clears it cleanly. Oracle replay failures: 0 — trajectory is deterministic.

**`island_parent_trajectory_contact_sheet.png`**
Contact sheet of oracle parent trajectories for all 289 island samples. Each cell shows the null-geodesic polyline at oracle step, colored by termination reason (hit/escaped) and step count. The seam-adjacent pixels show the highest step counts and most curved paths.

**`island_continuity_vectors.png`**
Layer 5: Transport continuity vector field over the island patch. Vectors encode pixel-to-pixel disagreement across six transport dimensions at production step. Low-density here compared to the full-frame Cathedral Probe overlay — the island's instability is sub-pixel seam instability, not broad gradient noise.

**`island_normal_angle_delta_map.png`**
Per-pixel normal angle delta between production and oracle. Concentrated at the seam boundary. Pixels away from the seam show near-zero delta (oracle and production agree on surface normal). The normal angle delta is a secondary indicator; collider ownership delta is the primary signal.

**`island_path_length_delta_map.png`**
Per-pixel path length delta between production and oracle. Shows the same spatial pattern as normal angle delta: concentrated at the ownership seam, near-zero elsewhere. Path length delta at the seam: production ray slightly overshoots the correct geometry at coarse step, hitting a different surface.

---

## Group 2 — ROI Sweep: Broad Coverage Run
*Source run: `reference_transport_oracle_roi_sweep/20260505T034858Z` · 64 samples · row traversal · stride 1*

| Docs asset | Source output path | Size |
|---|---|---|
| `roi_sweep_convergence_ladder.png` | `output/reference_transport_oracle_roi_sweep/20260505T034858Z/cells/row_stride_1/convergence_ladder_contact_sheet.png` | 17 KB |
| `roi_sweep_epsilon_stability_map.png` | `output/reference_transport_oracle_roi_sweep/20260505T034858Z/cells/row_stride_1/epsilon_stability_map.png` | 4.1 KB |
| `roi_sweep_production_vs_oracle_diff.png` | `output/reference_transport_oracle_roi_sweep/20260505T034858Z/cells/row_stride_1/production_vs_oracle_diff.png` | 3.1 KB |
| `roi_sweep_oracle_path_overlay.png` | `output/reference_transport_oracle_roi_sweep/20260505T034858Z/cells/row_stride_1/oracle_path_overlay.png` | 3.3 KB |
| `roi_sweep_parent_trajectory_contact_sheet.png` | `output/reference_transport_oracle_roi_sweep/20260505T034858Z/cells/row_stride_1/parent_trajectory_contact_sheet.png` | 42 KB |

### Captions

**`roi_sweep_convergence_ladder.png`**
ROI sweep convergence ladder contact sheet: 64 sampled pixels across the domain resolver stress scene. Row traversal, stride 1. Of 320 total step comparisons: 266 Stable (83.1%), 54 Unresolved (16.9%), 0 ThresholdSnap, 0 MultiSolution. The 54 unresolved comparisons are concentrated in the upper-left corner patch. Oracle replay failures: 0.

**`roi_sweep_epsilon_stability_map.png`**
Full-frame `EpsilonStabilityClass` map from the ROI sweep at the finest production step. Green = Stable (266 pixels); red = Unresolved (54 pixels). The unresolved cluster is localized to x=36..44, y=31..37 — forming a compact bounded island, not a diffuse distribution.

**`roi_sweep_production_vs_oracle_diff.png`**
Full-frame production vs oracle difference map from the ROI sweep. Differences track the transport ownership boundaries identified by the Cathedral Probe continuity vectors, plus the localized upper-left island — which the Cathedral Probe continuity vectors at step 0.015 did NOT independently flag.

**`roi_sweep_oracle_path_overlay.png`**
Oracle path overlay for ROI sweep samples. Shows sparse sampling coverage across the scene. The upper-left region receives samples that reveal the unresolved island; the rest of the scene shows stable or already-resolved pixels.

**`roi_sweep_parent_trajectory_contact_sheet.png`**
Contact sheet of oracle parent trajectories for all 64 ROI sweep samples. Variety of ray paths: background pixels (escaped, short paths), mid-scene hits (moderate path lengths, low curvature), wormhole-adjacent pixels (high curvature, many boundary events). The unresolved pixels show paths ending at the seam between collider regions.

---

## Embedding Guide

From `Docs/Research/` pages (one directory deeper than `Docs/assets/`):
```markdown
![Description](../assets/transport_islands/filename.png)
```

From `Docs/index.md` or `Docs/README.md` (same level as `assets/`):
```markdown
![Description](assets/transport_islands/filename.png)
```
