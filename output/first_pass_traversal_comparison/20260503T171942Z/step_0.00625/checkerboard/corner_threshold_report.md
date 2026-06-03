# Corner Transport Threshold Report

This is a passive corner/edge microscope pass. It does not alter beauty rendering, scheduling, hit selection, shading, resolver decisions, or precision stepping.

## ROI Summary

| roi | samples | required_precision | collider_flip_samples | ownership_change_samples | mean_max_risk | interpretation |
|---|---:|---:|---:|---:|---:|---|
| `geometry:25836914057:edge_midpoint:2` | 97 | 0.003125 | 42 | 42 | 4.054314 | hit_ownership_changes |
| `geometry:25836914057:edge_midpoint:4` | 97 | 0.003125 | 42 | 42 | 4.054314 | hit_ownership_changes |
| `geometry:25836914057:edge_midpoint:6` | 97 | 0.003125 | 42 | 42 | 4.054314 | hit_ownership_changes |
| `geometry:25836914057:edge_midpoint:7` | 97 | 0.003125 | 42 | 42 | 4.054314 | hit_ownership_changes |
| `manual_roi:280:145:manual_roi:280:35:geometry:25836914057:edge_midpoint:6` | 105 | 0.003125 | 101 | 101 | 2.443534 | hit_ownership_changes |
| `manual_roi:280:35:geometry:25836914057:edge_midpoint:6` | 105 | 0.003125 | 101 | 101 | 2.442675 | hit_ownership_changes |
| `manual_roi:40:145:manual_roi:40:35:geometry:25836914057:edge_midpoint:6` | 105 | 0.003125 | 101 | 101 | 2.442923 | hit_ownership_changes |
| `manual_roi:40:35:geometry:25836914057:edge_midpoint:6` | 105 | 0.003125 | 101 | 101 | 2.442093 | hit_ownership_changes |

## Global Notes

- Samples requiring reference/fine precision or ownership changes: 808 / 808
- Corner transitions are local in this probe unless corroborated by separate scheduler row-mod-stride DOE overlays.
