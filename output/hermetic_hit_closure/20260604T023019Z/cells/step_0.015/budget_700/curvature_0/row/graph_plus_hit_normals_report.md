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
| 0 | 294 | 0.0 | 0.0 | coherent |
| 1 | 1238 | 0.0 | 0.0 | coherent |
| 2 | 196 | 0.0 | 0.0 | coherent |
| 3 | 448 | 28.486264 | 74.357754 | incoherent |
| 4 | 202 | 0.727774 | 89.714949 | mixed |
| 5 | 880 | 0.204545 | 90.0 | mixed |
| 6 | 106 | 1.384419 | 89.454342 | mixed |
| 7 | 544 | 25.308773 | 102.362492 | incoherent |

## Seam Alignment

- Edge count: 13
- Edges with normal deltas: 13
- Max normal-angle delta: 90.0
- Assessment: Some seam edges align with visible normal discontinuities.

## Unresolved Islands

- Unstable rows: 0
- Assessment: No unstable subgraph rows were present; no abnormal island normal behavior can be assessed.

## Merge/Split Regions

- Merge/split rows: 0
- Assessment: No merge/split rows were present in this graph packet.
