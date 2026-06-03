# Graph Plus Hit Normals Report

Post-process diagnostic report only. This does not modify renderer behavior and does not claim physical truth.

## Outputs

- full_frame_hit_normals: `full_frame_hit_normals.png`
- roi_hit_normals: not generated
- unstable_subgraph_hit_normals: `unstable_subgraph_hit_normals.png`
- merge_split_hit_normals: not generated

## Normal Coherence Inside Ownership Nodes

| node | hit samples | mean angle | max angle | label |
|---:|---:|---:|---:|---|
| 0 | 18959 | 1.601715 | 78.782118 | mixed |
| 1 | 194 | 0.0 | 0.0 | coherent |
| 2 | 582 | 0.0 | 0.0 | coherent |
| 3 | 582 | 0.0 | 0.0 | coherent |
| 4 | 1165 | 1.080925 | 7.417806 | coherent |
| 5 | 7 | 0.0 | 0.0 | coherent |
| 6 | 78 | 0.0 | 0.0 | coherent |
| 7 | 252 | 0.0 | 0.0 | coherent |
| 8 | 252 | 0.0 | 0.0 | coherent |
| 9 | 78 | 0.0 | 0.0 | coherent |
| 10 | 252 | 0.0 | 0.0 | coherent |
| 11 | 252 | 0.0 | 0.0 | coherent |
| 12 | 78 | 0.0 | 0.0 | coherent |
| 13 | 252 | 0.0 | 0.0 | coherent |
| 14 | 252 | 0.0 | 0.0 | coherent |
| 15 | 78 | 0.0 | 0.0 | coherent |
| 16 | 252 | 0.0 | 0.0 | coherent |
| 17 | 252 | 0.0 | 0.0 | coherent |
| 18 | 78 | 0.0 | 0.0 | coherent |
| 19 | 252 | 0.0 | 0.0 | coherent |
| 20 | 126 | 0.0 | 0.0 | coherent |
| 21 | 15 | 37.403489 | 67.04551 | incoherent |
| 22 | 111 | 0.0 | 0.0 | coherent |
| 23 | 78 | 0.0 | 0.0 | coherent |
| 24 | 252 | 0.0 | 0.0 | coherent |
| 25 | 252 | 0.0 | 0.0 | coherent |
| 26 | 78 | 0.0 | 0.0 | coherent |
| 27 | 252 | 0.0 | 0.0 | coherent |
| 28 | 252 | 0.0 | 0.0 | coherent |
| 29 | 78 | 0.0 | 0.0 | coherent |
| 30 | 252 | 0.0 | 0.0 | coherent |
| 31 | 2404 | 0.0 | 0.0 | coherent |
| 32 | 78 | 0.0 | 0.0 | coherent |
| 33 | 252 | 0.0 | 0.0 | coherent |
| 34 | 78 | 0.0 | 0.0 | coherent |
| 35 | 252 | 0.0 | 0.0 | coherent |
| 36 | 78 | 0.0 | 0.0 | coherent |
| 37 | 252 | 0.0 | 0.0 | coherent |
| 38 | 78 | 0.0 | 0.0 | coherent |
| 39 | 252 | 0.0 | 0.0 | coherent |

## Seam Alignment

- Edge count: 136
- Edges with normal deltas: 136
- Max normal-angle delta: 79.361135
- Assessment: Some seam edges align with visible normal discontinuities.

## Unresolved Islands

- Unstable rows: 0
- Assessment: No unstable subgraph rows were present; no abnormal island normal behavior can be assessed.

## Merge/Split Regions

- Merge/split rows: 0
- Assessment: No merge/split rows were present in this graph packet.
