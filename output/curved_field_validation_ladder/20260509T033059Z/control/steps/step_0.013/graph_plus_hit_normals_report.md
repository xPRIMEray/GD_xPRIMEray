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
| 0 | 16225 | 1.696527 | 66.124776 | mixed |
| 1 | 65 | 0.0 | 0.0 | coherent |
| 2 | 485 | 0.0 | 0.0 | coherent |
| 3 | 485 | 0.0 | 0.0 | coherent |
| 4 | 485 | 0.0 | 0.0 | coherent |
| 5 | 252 | 0.0 | 0.0 | coherent |
| 6 | 78 | 0.0 | 0.0 | coherent |
| 7 | 187 | 0.0 | 0.0 | coherent |
| 8 | 252 | 0.0 | 0.0 | coherent |
| 9 | 78 | 0.0 | 0.0 | coherent |
| 10 | 219 | 0.0 | 0.0 | coherent |
| 11 | 252 | 0.0 | 0.0 | coherent |
| 12 | 78 | 0.0 | 0.0 | coherent |
| 13 | 187 | 0.0 | 0.0 | coherent |
| 14 | 252 | 0.0 | 0.0 | coherent |
| 15 | 78 | 0.0 | 0.0 | coherent |
| 16 | 187 | 0.0 | 0.0 | coherent |
| 17 | 252 | 0.0 | 0.0 | coherent |
| 18 | 78 | 0.0 | 0.0 | coherent |
| 19 | 187 | 0.0 | 0.0 | coherent |
| 20 | 126 | 0.0 | 0.0 | coherent |
| 21 | 15 | 37.406874 | 67.068946 | incoherent |
| 22 | 111 | 0.0 | 0.0 | coherent |
| 23 | 65 | 0.0 | 0.0 | coherent |
| 24 | 168 | 0.0 | 0.0 | coherent |
| 25 | 252 | 0.0 | 0.0 | coherent |
| 26 | 78 | 0.0 | 0.0 | coherent |
| 27 | 219 | 0.0 | 0.0 | coherent |
| 28 | 252 | 0.0 | 0.0 | coherent |
| 29 | 65 | 0.0 | 0.0 | coherent |
| 30 | 168 | 0.0 | 0.0 | coherent |
| 31 | 252 | 0.0 | 0.0 | coherent |
| 32 | 78 | 0.0 | 0.0 | coherent |
| 33 | 187 | 0.0 | 0.0 | coherent |
| 34 | 252 | 0.0 | 0.0 | coherent |
| 35 | 65 | 0.0 | 0.0 | coherent |
| 36 | 168 | 0.0 | 0.0 | coherent |
| 37 | 252 | 0.0 | 0.0 | coherent |
| 38 | 78 | 0.0 | 0.0 | coherent |
| 39 | 219 | 0.0 | 0.0 | coherent |

## Seam Alignment

- Edge count: 145
- Edges with normal deltas: 145
- Max normal-angle delta: 65.294014
- Assessment: Some seam edges align with visible normal discontinuities.

## Unresolved Islands

- Unstable rows: 0
- Assessment: No unstable subgraph rows were present; no abnormal island normal behavior can be assessed.

## Merge/Split Regions

- Merge/split rows: 0
- Assessment: No merge/split rows were present in this graph packet.
