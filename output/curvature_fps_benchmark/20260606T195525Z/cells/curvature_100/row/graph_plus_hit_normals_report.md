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
| 0 | 2240 | 27.552605 | 89.968697 | incoherent |
| 1 | 15180 | 6.166008 | 90.0 | mixed |
| 2 | 2240 | 27.552605 | 89.968697 | incoherent |
| 3 | 244 | 0.737705 | 90.0 | mixed |

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
