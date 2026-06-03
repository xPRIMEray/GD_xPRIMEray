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
| 0 | 147 | 0.0 | 0.0 | coherent |
| 1 | 666 | 0.0 | 0.0 | coherent |
| 2 | 147 | 0.0 | 0.0 | coherent |
| 3 | 0 |  |  | no-hit-or-insufficient-samples |
| 4 | 153 | 0.0 | 0.0 | coherent |
| 5 | 609 | 0.0 | 0.0 | coherent |
| 6 | 102 | 0.0 | 0.0 | coherent |
| 7 | 224 | 29.437045 | 73.574605 | incoherent |
| 8 | 156 | 0.941829 | 89.630355 | mixed |
| 9 | 577 | 0.567064 | 90.099818 | mixed |
| 10 | 104 | 1.41094 | 89.443748 | mixed |
| 11 | 192 | 33.714199 | 69.623565 | incoherent |
| 12 | 156 | 0.0 | 0.0 | coherent |
| 13 | 572 | 0.0 | 0.0 | coherent |
| 14 | 104 | 0.0 | 0.0 | coherent |
| 15 | 192 | 33.714199 | 69.623565 | incoherent |

## Seam Alignment

- Edge count: 29
- Edges with normal deltas: 29
- Max normal-angle delta: 90.0
- Assessment: Some seam edges align with visible normal discontinuities.

## Unresolved Islands

- Unstable rows: 0
- Assessment: No unstable subgraph rows were present; no abnormal island normal behavior can be assessed.

## Merge/Split Regions

- Merge/split rows: 0
- Assessment: No merge/split rows were present in this graph packet.
