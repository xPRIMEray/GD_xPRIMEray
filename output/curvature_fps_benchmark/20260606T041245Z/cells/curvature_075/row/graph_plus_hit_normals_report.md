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
| 0 | 108 | 7.992409 | 86.633539 | mixed |
| 1 | 1125 | 0.450884 | 90.051157 | mixed |
| 2 | 96 | 8.962315 | 86.185925 | mixed |
| 3 | 32 | 41.490939 | 59.036243 | incoherent |
| 4 | 540 | 12.080208 | 84.680106 | mixed |
| 5 | 4896 | 2.279412 | 90.0 | mixed |
| 6 | 540 | 12.080208 | 84.680106 | mixed |
| 7 | 0 |  |  | no-hit-or-insufficient-samples |

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
