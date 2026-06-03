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
| 0 | 0 |  |  | no-hit-or-insufficient-samples |

## Seam Alignment

- Edge count: 0
- Edges with normal deltas: 0
- Max normal-angle delta: 
- Assessment: No graph seam edges were present in this graph packet.

## Unresolved Islands

- Unstable rows: 1
- Assessment: Unstable subgraph rows exist; inspect unstable_subgraph_hit_normals.png for local normal behavior.

## Merge/Split Regions

- Merge/split rows: 0
- Assessment: No merge/split rows were present in this graph packet.
