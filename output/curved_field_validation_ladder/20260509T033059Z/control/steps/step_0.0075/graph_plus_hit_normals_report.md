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
| 0 | 12348 | 1.614372 | 66.030702 | mixed |
| 1 | 386 | 0.0 | 0.0 | coherent |
| 2 | 387 | 0.0 | 0.0 | coherent |
| 3 | 354 | 0.0 | 0.0 | coherent |
| 4 | 168 | 0.0 | 0.0 | coherent |
| 5 | 52 | 0.0 | 0.0 | coherent |
| 6 | 166 | 0.0 | 0.0 | coherent |
| 7 | 168 | 0.0 | 0.0 | coherent |
| 8 | 52 | 0.0 | 0.0 | coherent |
| 9 | 168 | 0.0 | 0.0 | coherent |
| 10 | 168 | 0.0 | 0.0 | coherent |
| 11 | 52 | 0.0 | 0.0 | coherent |
| 12 | 166 | 0.0 | 0.0 | coherent |
| 13 | 168 | 0.0 | 0.0 | coherent |
| 14 | 52 | 0.0 | 0.0 | coherent |
| 15 | 167 | 0.0 | 0.0 | coherent |
| 16 | 168 | 0.0 | 0.0 | coherent |
| 17 | 52 | 0.0 | 0.0 | coherent |
| 18 | 168 | 0.0 | 0.0 | coherent |
| 19 | 84 | 0.0 | 0.0 | coherent |
| 20 | 10 | 33.778059 | 61.776795 | incoherent |
| 21 | 74 | 0.0 | 0.0 | coherent |
| 22 | 52 | 0.0 | 0.0 | coherent |
| 23 | 167 | 0.0 | 0.0 | coherent |
| 24 | 168 | 0.0 | 0.0 | coherent |
| 25 | 52 | 0.0 | 0.0 | coherent |
| 26 | 167 | 0.0 | 0.0 | coherent |
| 27 | 168 | 0.0 | 0.0 | coherent |
| 28 | 52 | 0.0 | 0.0 | coherent |
| 29 | 168 | 0.0 | 0.0 | coherent |
| 30 | 168 | 0.0 | 0.0 | coherent |
| 31 | 52 | 0.0 | 0.0 | coherent |
| 32 | 167 | 0.0 | 0.0 | coherent |
| 33 | 168 | 0.0 | 0.0 | coherent |
| 34 | 52 | 0.0 | 0.0 | coherent |
| 35 | 167 | 0.0 | 0.0 | coherent |
| 36 | 168 | 0.0 | 0.0 | coherent |
| 37 | 52 | 0.0 | 0.0 | coherent |
| 38 | 167 | 0.0 | 0.0 | coherent |
| 39 | 168 | 0.0 | 0.0 | coherent |

## Seam Alignment

- Edge count: 144
- Edges with normal deltas: 144
- Max normal-angle delta: 65.244868
- Assessment: Some seam edges align with visible normal discontinuities.

## Unresolved Islands

- Unstable rows: 0
- Assessment: No unstable subgraph rows were present; no abnormal island normal behavior can be assessed.

## Merge/Split Regions

- Merge/split rows: 0
- Assessment: No merge/split rows were present in this graph packet.
