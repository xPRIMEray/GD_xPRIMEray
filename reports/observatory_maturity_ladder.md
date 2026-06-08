# Observatory Maturity Ladder v1

The Observatory now tracks not only what exists, but how strongly each artifact should be trusted. Maturity is evidence strength, not physical truth.

## Ladder

| score | stage | meaning | minimum evidence |
|---:|---|---|---|
| 0 | **Proposed** | Named concept or intended observatory primitive; design exists but repeatable artifact evidence is not yet established. | Concept document, prompt, or architecture note. |
| 1 | **Experimental** | Artifact exists, but the contract, inputs, or interpretation are still unstable. | At least one generated artifact or report with caveats. |
| 2 | **Observed** | Artifact has been produced from real run data and can be inspected, but coverage or closure may be partial. | Source path plus visible artifact or report. |
| 3 | **Confirmed** | Artifact satisfies its local validation gate for at least one run or fixture. | PASS closure/verdict or equivalent local gate. |
| 4 | **Characterized** | Artifact has repeatable structure, documented interpretation, and known caveats. | Documented method, schema/tooling, or repeated comparable outputs. |
| 5 | **Canonical** | Artifact is part of the stable Observatory language and can anchor other interpretations. | Promoted visitor-facing artifact, stable fixture contract, and clear non-ground-truth caveats. |

## Anchor Assignments

| artifact | maturity | score | source | basis |
|---|---:|---:|---|---|
| **Hermetic Storyboard** | Canonical | 5 | `Docs/assets/observatory/hermetic_storyboard_v2.png` | Promoted Gallery anchor for the sealed-room fixture with closure, coverage, curvature signature, verdict, and explicit scene-contract caveats. |
| **Observer Storyboard Demo** | Characterized | 4 | `reports/observer_storyboard_demo.png` | Renderer-agnostic nine-panel framework has a schema, rendering tool, demo artifact, and explicit PASS/FAIL/PARTIAL/MISSING vocabulary. |
| **Query Observatory** | Observed | 2 | `reports/query_storyboard_v1.png` | A real storyboard artifact exists, but it has not yet been promoted into a stable catalog or canonical visitor-facing contract. |
| **Cost Basin** | Proposed | 0 | `reports/cost_basin_v1.md` | Concept architecture is documented; no dedicated Cost Basin artifact generation or validation gate exists yet. |

## Catalog Artifact Scores

| fixture | artifact | run | maturity | score | coverage | closure | verdict | source |
|---|---|---|---:|---:|---|---|---|---|
| `hermetic_curved_room` | `hermetic_storyboard_v2` | `published` | Canonical | 5 | PASS | PASS | PASS | `Docs/assets/observatory/hermetic_storyboard_v2.png` |
| `hermetic_curved_room` | `hermetic_storyboard_v2` | `published` | Canonical | 5 | PASS | PASS | PASS | `reports/hermetic_storyboard_v2.png` |
| `hermetic_curved_room` | `curvature_signature_ladder` | `20260607T191143Z` | Characterized | 4 | PASS | PASS | PASS | `output/curvature_fps_benchmark/20260607T191143Z/curvature_signature_ladder.png` |
| `hermetic_curved_room` | `curvature_signature_ladder` | `20260607T191820Z` | Characterized | 4 | PASS | PASS | PASS | `output/curvature_fps_benchmark/20260607T191820Z/curvature_signature_ladder.png` |
| `hermetic_curved_room` | `curvature_signature_ladder` | `20260607T221311Z` | Characterized | 4 | PASS | PASS | PASS | `output/curvature_fps_benchmark/20260607T221311Z/curvature_signature_ladder.png` |
| `observatory_reference` | `observatory_story_reference` | `published` | Characterized | 4 | MISSING | MISSING | PASS | `reports/observatory_story_reference.png` |
| `observer_storyboard_framework` | `observer_storyboard` | `published` | Characterized | 4 | PARTIAL | PARTIAL | PARTIAL | `reports/observer_storyboard_demo.png` |
| `renderer` | `renderer_storyboard_v1` | `published` | Characterized | 4 | PASS | PASS | PASS | `reports/renderer_storyboard_v1.png` |
| `hermetic_curved_room` | `curvature_signature_ladder` | `20260607T155158Z` | Confirmed | 3 | PARTIAL | PASS | PARTIAL | `output/curvature_fps_benchmark/20260607T155158Z/curvature_signature_ladder.png` |
| `hermetic_curved_room` | `curvature_signature_ladder` | `20260607T185034Z` | Confirmed | 3 | PARTIAL | PASS | PARTIAL | `output/curvature_fps_benchmark/20260607T185034Z/curvature_signature_ladder.png` |
| `hermetic_curved_room` | `curvature_signature_ladder` | `20260606T014236Z` | Observed | 2 | MISSING | PASS | MISSING | `output/curvature_fps_benchmark/20260606T014236Z/curvature_signature_ladder.png` |
| `hermetic_curved_room` | `curvature_signature_ladder` | `20260606T195525Z` | Observed | 2 | MISSING | PASS | MISSING | `output/curvature_fps_benchmark/20260606T195525Z/curvature_signature_ladder.png` |
| `hermetic_curved_room` | `curvature_signature_ladder` | `20260607T044625Z` | Observed | 2 | MISSING | PASS | MISSING | `output/curvature_fps_benchmark/20260607T044625Z/curvature_signature_ladder.png` |
| `hermetic_curved_room` | `curvature_signature_ladder` | `20260607T152708Z` | Observed | 2 | MISSING | PASS | MISSING | `output/curvature_fps_benchmark/20260607T152708Z/curvature_signature_ladder.png` |
| `hermetic_curved_room` | `curvature_signature_ladder` | `published` | Observed | 2 | MISSING | PASS | MISSING | `reports/curvature_signature_ladder.png` |
| `hermetic_curved_room` | `curvature_signature_ladder` | `published` | Observed | 2 | MISSING | PASS | MISSING | `reports/weekend_fps_curvature_sweep_assets/curvature_signature_ladder.png` |

## Reading Rule

A higher score means the artifact has stronger evidence, clearer caveats, and more stable interpretation. It does not mean the artifact proves physical correctness. Canonical means it can anchor Observatory interpretation within its declared scene contract.
