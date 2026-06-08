# Observer Storyboard Framework v1

Observer Storyboard v1 is a renderer-agnostic evidence layout. It takes any artifact set and renders the same nine questions in the same order, so a reader can understand what was observed, what assumptions were made, what changed between perspectives, and whether the fixture contract was satisfied.

This framework is intentionally not connected to the renderer yet. It only defines a manifest shape, panel semantics, status vocabulary, and a rendering tool.

## Panel Order

| slot | panel | question |
|---:|---|---|
| 1 | Observation | What was directly observed? |
| 2 | Assumptions | What must be true for this observation to mean anything? |
| 3 | Perspectives | Which viewpoints, modes, or reference frames are being compared? |
| 4 | Disagreements | Where do the perspectives diverge? |
| 5 | Closure Basin | Where did the evaluation reach terminal classification, and where did it not? |
| 6 | Lineage | How did the observation evolve, persist, split, or merge? |
| 7 | Coverage | How much of the artifact set was actually evaluated? |
| 8 | Sensitivity Signature | What changed when the tested condition was activated or varied? |
| 9 | Verdict | Did the fixture satisfy its stated contract? |

## Status Vocabulary

| status | meaning |
|---|---|
| `PASS` | The panel evidence is present and satisfies the local check. |
| `FAIL` | The panel evidence is present and contradicts the expected contract. |
| `PARTIAL` | Some evidence exists, but the panel is incomplete or not strong enough for a full pass. |
| `MISSING` | The artifact or metric is absent. Missing evidence should be visible, not hidden. |

## Manifest

The manifest schema lives at:

```text
docs/observer_storyboard/observer_storyboard_v1.schema.json
```

Minimum shape:

```json
{
  "schema": "xprimeray.observer_storyboard.v1",
  "title": "Demo Observer Storyboard",
  "subtitle": "Renderer-agnostic artifact-set summary",
  "panels": [
    {
      "slot": 1,
      "key": "observation",
      "title": "Observation",
      "question": "What was directly observed?",
      "status": "PASS",
      "caption": "Primary observed artifact."
    }
  ]
}
```

Real manifests must provide all nine panels. Image artifacts are optional; when an artifact is absent, the tool renders an explicit placeholder tile using the panel status.

## Tool

Render a manifest:

```bash
python3 tools/observer_storyboard.py \
  --manifest path/to/storyboard.json \
  --output reports/my_storyboard.png
```

Generate the built-in demo:

```bash
python3 tools/observer_storyboard.py --demo --output reports/observer_storyboard_demo.png
```

## Guardrails

- The framework does not infer physical correctness.
- A beautiful observation is not enough; coverage, closure, disagreement, and assumptions must be visible.
- Missing panels are rendered explicitly.
- `PASS` means the artifact-set contract passed, not that the underlying physical model is ground truth.
