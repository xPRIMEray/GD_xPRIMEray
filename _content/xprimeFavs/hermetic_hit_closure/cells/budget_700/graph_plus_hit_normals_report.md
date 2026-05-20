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
| 0 | 40 | 10.690144 | 85.364537 | mixed |
| 1 | 140 | 9.0 | 90.0 | mixed |
| 2 | 40 | 10.690144 | 85.364537 | mixed |
| 3 | 0 |  |  | no-hit-or-insufficient-samples |

## Seam Alignment

- Edge count: 5
- Edges with normal deltas: 5
- Max normal-angle delta: 90.0
- Assessment: Some seam edges align with visible normal discontinuities.

## Unresolved Islands

- Unstable rows: 0
- Assessment: No unstable subgraph rows were present; no abnormal island normal behavior can be assessed.

## Merge/Split Regions

- Merge/split rows: 0
- Assessment: No merge/split rows were present in this graph packet.
