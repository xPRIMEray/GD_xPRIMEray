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
| 0 | 21443 | 1.60781 | 79.937732 | mixed |
| 1 | 21443 | 1.60781 | 79.937732 | mixed |
| 2 | 7 | 0.0 | 0.0 | coherent |
| 3 | 78 | 0.0 | 0.0 | coherent |
| 4 | 252 | 0.0 | 0.0 | coherent |
| 5 | 78 | 0.0 | 0.0 | coherent |
| 6 | 252 | 0.0 | 0.0 | coherent |
| 7 | 78 | 0.0 | 0.0 | coherent |
| 8 | 252 | 0.0 | 0.0 | coherent |
| 9 | 78 | 0.0 | 0.0 | coherent |
| 10 | 252 | 0.0 | 0.0 | coherent |
| 11 | 78 | 0.0 | 0.0 | coherent |
| 12 | 252 | 0.0 | 0.0 | coherent |
| 13 | 25 | 39.335724 | 75.297483 | incoherent |
| 14 | 260 | 0.0 | 0.0 | coherent |
| 15 | 252 | 0.0 | 0.0 | coherent |
| 16 | 252 | 0.0 | 0.0 | coherent |
| 17 | 252 | 0.0 | 0.0 | coherent |
| 18 | 546 | 0.0 | 0.0 | coherent |
| 19 | 252 | 0.0 | 0.0 | coherent |
| 20 | 252 | 0.0 | 0.0 | coherent |
| 21 | 523 | 0.0 | 0.0 | coherent |
| 22 | 252 | 0.0 | 0.0 | coherent |
| 23 | 252 | 0.0 | 0.0 | coherent |
| 24 | 78 | 0.0 | 0.0 | coherent |
| 25 | 252 | 0.0 | 0.0 | coherent |
| 26 | 78 | 0.0 | 0.0 | coherent |
| 27 | 252 | 0.0 | 0.0 | coherent |
| 28 | 78 | 0.0 | 0.0 | coherent |
| 29 | 252 | 0.0 | 0.0 | coherent |
| 30 | 858 | 0.0 | 0.0 | coherent |
| 31 | 252 | 0.0 | 0.0 | coherent |
| 32 | 826 | 1.01035 | 79.377324 | mixed |
| 33 | 4 | 23.595506 | 35.758432 | incoherent |
| 34 | 15 | 33.1985 | 58.822534 | incoherent |
| 35 | 129 | 1.604993 | 64.556308 | mixed |
| 36 | 523 | 0.0 | 0.0 | coherent |
| 37 | 523 | 0.0 | 0.0 | coherent |
| 38 | 252 | 0.0 | 0.0 | coherent |

## Seam Alignment

- Edge count: 74
- Edges with normal deltas: 74
- Max normal-angle delta: 79.362304
- Assessment: Some seam edges align with visible normal discontinuities.

## Unresolved Islands

- Unstable rows: 0
- Assessment: No unstable subgraph rows were present; no abnormal island normal behavior can be assessed.

## Merge/Split Regions

- Merge/split rows: 0
- Assessment: No merge/split rows were present in this graph packet.
