# Band Boundary Analysis

Primitive/shape id is not available on the current stored-hit export path, so `primitive_or_shape_id` remains `-1` in the CSV and collider id is used as the object-level switch signal.

| Checkpoint | Pixels | Hit Class | Collider Change | Normal Delta | Distance Delta | Segment Delta | Left Normal | Right Normal |
| --- | --- | --- | --- | ---: | ---: | ---: | --- | --- |
| mouth | (268,132) -> (269,132) | background -> background | True | 1.414214 | 0.000222 | 0 | (0,0,-1) | (-1,0,0) |
| post_throat_backstep_01 | (454,37) -> (455,37) | background -> background | False | 1.414214 | 0.006328 | 0 | (-1,0,0) | (0,-1,0) |
| post_throat_backstep_01 | (440,107) -> (441,107) | background -> background | True | 2.000000 | 1.337458 | 12 | (0,0,1) | (0,0,-1) |

## Verdict

- Near-side mouth boundary: object-level nearest-hit switching and a matching stored-normal flip happen with almost no distance or segment-count change.
- Hard-leg boundary A: same-collider stored-normal flip alone is enough to create a strong band edge.
- Hard-leg boundary B: collider switch, normal reversal, hit-distance jump, and segment-count jump all coincide, indicating a deeper nearest-hit surface change rather than a pure coloring artifact.
