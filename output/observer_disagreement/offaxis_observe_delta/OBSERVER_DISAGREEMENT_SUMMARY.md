# Observer Disagreement Summary

Packet:

```text
output/observer_disagreement/offaxis_observe_delta/
```

This packet is the first measured off-axis observer-disagreement artifact for matched straight/curved transport assumptions. It compares normalized terminal transport classifications exported from:

```text
res://test-straight-basic-visual-offaxis-observe.tscn
res://test-grin-basic-visual-offaxis-observe.tscn
```

It also includes paired resolved-film beauty frames from the same scene pair. The beauty frames are observatory context only: resolved-film context before reading where classification changed.

## Visual Summary

```text
output/observer_disagreement/offaxis_observe_delta/observability_cutsheet.png
output/observer_disagreement/offaxis_observe_delta/contact_sheet.png
```

The observability cutsheet contains:

- Straight beauty frame
- Curved GRIN beauty frame
- Straight classification
- Curved GRIN classification
- Delta mask
- Delta contours
- Metrics panel

## Key Metrics

| Metric | Value |
|---|---:|
| Resolution | 480x270 |
| Changed pixels | 30,839 |
| Changed ratio | 23.8% |
| Unresolved pixels | 51,232 |
| Unresolved ratio | 39.5% |

Top transitions:

| Transition | Count |
|---|---:|
| geom_hit -> escaped_no_hit | 27,619 |
| escaped_no_hit -> geom_hit | 3,220 |

## Interpretation

The measured classification changed between the matched straight/curved transport assumptions. The dominant transition is `geom_hit -> escaped_no_hit`, indicating that terminal evidence redistributed away from geometry-hit classification in the curved GRIN capture for those pixels.

The paired beauty frames provide context for the visible film output. They do not add disagreement evidence by themselves; they let the viewer connect resolved-film context with how terminal evidence redistributed.

This is an observability artifact. It shows where terminal classification labels differ between paired captures. It does not claim physical proof beyond the measured classification buffers.

## Guardrails

- Measured outputs only.
- No transport changes.
- No scheduler changes.
- No traversal-order changes.
- No hit-selection changes.
- No oracle changes.
- No synthetic beauty-frame effects.
- Hermetic observatory remains the export sanity gate.
