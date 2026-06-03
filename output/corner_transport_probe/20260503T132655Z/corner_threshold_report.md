# Corner Transport Threshold Report

This is a passive corner/edge microscope pass. It does not alter beauty rendering, scheduling, hit selection, shading, resolver decisions, or precision stepping.

## ROI Summary

| roi | samples | required_precision | collider_flip_samples | ownership_change_samples | mean_max_risk | interpretation |
|---|---:|---:|---:|---:|---:|---|
| `geometry:25836914057:edge_midpoint:6` | 89 | 0.003125 | 39 | 39 | 4.038819 | hit_ownership_changes |

## Global Notes

- Samples requiring reference/fine precision or ownership changes: 89 / 89
- Corner transitions are local in this probe unless corroborated by separate scheduler row-mod-stride DOE overlays.
