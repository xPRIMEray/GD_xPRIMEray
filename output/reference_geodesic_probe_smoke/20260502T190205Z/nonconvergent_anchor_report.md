# Nonconvergent Anchor Report

- Input rows: 104
- Anchor/radial-offset groups: 26
- Overall match rate versus reference: 0.250
- Epsilon: 0.05
- Monotonic decay failures: 18
- Persistent mismatches at `0.00625`: 26

## Objects Most Associated With Nonconvergence

| object_id | persistent_offset_count |
|---|---:|
| `geometry:25836914057` | 13 |
| `geometry:25937577361` | 13 |

## Tiles Most Associated With Nonconvergence

| projected_tile | persistent_offset_count |
|---:|---:|
| 2 | 13 |
| 1 | 12 |
| 0 | 1 |

## Anchors With Persistent Mismatch At 0.00625

| anchor_id | persistent_offset_count |
|---|---:|
| `geometry:25836914057:centroid` | 13 |
| `geometry:25937577361:centroid` | 13 |

## Monotonic Decay Failures

| anchor_id | object_id | tile | offset |
|---|---|---:|---:|
| `geometry:25836914057:centroid` | `geometry:25836914057` | 2 | (0,0) |
| `geometry:25836914057:centroid` | `geometry:25836914057` | 2 | (-1,0) |
| `geometry:25836914057:centroid` | `geometry:25836914057` | 2 | (0,1) |
| `geometry:25836914057:centroid` | `geometry:25836914057` | 2 | (0,-1) |
| `geometry:25836914057:centroid` | `geometry:25836914057` | 2 | (-2,0) |
| `geometry:25836914057:centroid` | `geometry:25836914057` | 2 | (0,2) |
| `geometry:25836914057:centroid` | `geometry:25836914057` | 2 | (0,-2) |
| `geometry:25836914057:centroid` | `geometry:25836914057` | 2 | (4,0) |
| `geometry:25836914057:centroid` | `geometry:25836914057` | 2 | (-4,0) |
| `geometry:25836914057:centroid` | `geometry:25836914057` | 2 | (0,4) |
| `geometry:25836914057:centroid` | `geometry:25836914057` | 2 | (0,-4) |
| `geometry:25937577361:centroid` | `geometry:25937577361` | 1 | (0,0) |
| `geometry:25937577361:centroid` | `geometry:25937577361` | 1 | (1,0) |
| `geometry:25937577361:centroid` | `geometry:25937577361` | 1 | (0,1) |
| `geometry:25937577361:centroid` | `geometry:25937577361` | 1 | (0,-1) |
| `geometry:25937577361:centroid` | `geometry:25937577361` | 1 | (2,0) |
| `geometry:25937577361:centroid` | `geometry:25937577361` | 1 | (-2,0) |
| `geometry:25937577361:centroid` | `geometry:25937577361` | 1 | (0,4) |

## Notes

This is analysis only. It does not alter renderer behavior, scheduler order, hit selection, or shading.
