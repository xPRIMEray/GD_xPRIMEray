# Corner Transport Threshold Report

This is a passive corner/edge microscope pass. It does not alter beauty rendering, scheduling, hit selection, shading, resolver decisions, or precision stepping.

## ROI Summary

| roi | samples | required_precision | collider_flip_samples | ownership_change_samples | mean_max_risk | interpretation |
|---|---:|---:|---:|---:|---:|---|
| `geometry:25836914057:edge_midpoint:2` | 81 | 0.003125 | 36 | 36 | 4.594262 | hit_ownership_changes |
| `geometry:25836914057:edge_midpoint:4` | 81 | 0.003125 | 36 | 36 | 4.594262 | hit_ownership_changes |
| `geometry:25836914057:edge_midpoint:6` | 81 | 0.003125 | 36 | 36 | 4.594262 | hit_ownership_changes |
| `geometry:25836914057:edge_midpoint:7` | 81 | 0.003125 | 36 | 36 | 4.594262 | hit_ownership_changes |
| `manual_roi:280:145:manual_roi:280:35:geometry:25836914057:edge_midpoint:6` | 81 | 0.003125 | 81 | 81 | 3.652527 | hit_ownership_changes |
| `manual_roi:280:35:geometry:25836914057:edge_midpoint:6` | 81 | 0.003125 | 81 | 81 | 3.650629 | hit_ownership_changes |
| `manual_roi:40:145:manual_roi:40:35:geometry:25836914057:edge_midpoint:6` | 81 | 0.003125 | 81 | 81 | 3.651177 | hit_ownership_changes |
| `manual_roi:40:35:geometry:25836914057:edge_midpoint:6` | 81 | 0.003125 | 81 | 81 | 3.64928 | hit_ownership_changes |

## Global Notes

- Samples requiring reference/fine precision or ownership changes: 648 / 648
- Corner transitions are local in this probe unless corroborated by separate scheduler row-mod-stride DOE overlays.
