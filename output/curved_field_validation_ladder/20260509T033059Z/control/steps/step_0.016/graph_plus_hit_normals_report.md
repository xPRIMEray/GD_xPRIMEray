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
| 0 | 18613 | 1.627991 | 78.766133 | mixed |
| 1 | 161 | 0.0 | 0.0 | coherent |
| 2 | 582 | 0.0 | 0.0 | coherent |
| 3 | 582 | 0.0 | 0.0 | coherent |
| 4 | 581 | 0.190154 | 7.903911 | coherent |
| 5 | 7 | 0.0 | 0.0 | coherent |
| 6 | 252 | 0.0 | 0.0 | coherent |
| 7 | 78 | 0.0 | 0.0 | coherent |
| 8 | 252 | 0.0 | 0.0 | coherent |
| 9 | 252 | 0.0 | 0.0 | coherent |
| 10 | 78 | 0.0 | 0.0 | coherent |
| 11 | 252 | 0.0 | 0.0 | coherent |
| 12 | 252 | 0.0 | 0.0 | coherent |
| 13 | 78 | 0.0 | 0.0 | coherent |
| 14 | 187 | 0.0 | 0.0 | coherent |
| 15 | 252 | 0.0 | 0.0 | coherent |
| 16 | 78 | 0.0 | 0.0 | coherent |
| 17 | 219 | 0.0 | 0.0 | coherent |
| 18 | 252 | 0.0 | 0.0 | coherent |
| 19 | 78 | 0.0 | 0.0 | coherent |
| 20 | 251 | 0.0 | 0.0 | coherent |
| 21 | 126 | 0.0 | 0.0 | coherent |
| 22 | 15 | 37.404921 | 67.058492 | incoherent |
| 23 | 111 | 0.0 | 0.0 | coherent |
| 24 | 78 | 0.0 | 0.0 | coherent |
| 25 | 252 | 0.0 | 0.0 | coherent |
| 26 | 252 | 0.0 | 0.0 | coherent |
| 27 | 78 | 0.0 | 0.0 | coherent |
| 28 | 219 | 0.0 | 0.0 | coherent |
| 29 | 252 | 0.0 | 0.0 | coherent |
| 30 | 78 | 0.0 | 0.0 | coherent |
| 31 | 219 | 0.0 | 0.0 | coherent |
| 32 | 252 | 0.0 | 0.0 | coherent |
| 33 | 78 | 0.0 | 0.0 | coherent |
| 34 | 251 | 0.0 | 0.0 | coherent |
| 35 | 252 | 0.0 | 0.0 | coherent |
| 36 | 78 | 0.0 | 0.0 | coherent |
| 37 | 251 | 0.0 | 0.0 | coherent |
| 38 | 252 | 0.0 | 0.0 | coherent |
| 39 | 78 | 0.0 | 0.0 | coherent |

## Seam Alignment

- Edge count: 149
- Edges with normal deltas: 149
- Max normal-angle delta: 79.355955
- Assessment: Some seam edges align with visible normal discontinuities.

## Unresolved Islands

- Unstable rows: 0
- Assessment: No unstable subgraph rows were present; no abnormal island normal behavior can be assessed.

## Merge/Split Regions

- Merge/split rows: 0
- Assessment: No merge/split rows were present in this graph packet.
