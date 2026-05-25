# Observer Disagreement Off-Axis Packet V01

## Scope

This document records the first visually meaningful measured off-axis observer-disagreement packet. Hermetic observatory classification export remains the plumbing sanity gate; this packet is the visual observer-disagreement artifact path.

The packet compares normalized terminal transport classification buffers from matched straight/curved transport assumptions. It also includes paired resolved-film beauty frames as observatory context, so viewers can connect resolved-film context with where classification changed. It does not modify transport semantics, scheduler behavior, traversal order, hit selection, resolver decisions, or oracle logic.

## Command Used

```bash
GODOT_EXE=./scripts/godot_local.sh python3 tools/observer_disagreement_offaxis_export.py --output-dir output/observer_disagreement/offaxis_observe_delta
GODOT_EXE=./scripts/godot_local.sh python3 tools/observer_disagreement_offaxis_beauty_export.py --output-dir output/observer_disagreement/offaxis_observe_delta
python3 tools/build_observer_disagreement_contact_sheet.py --packet-dir output/observer_disagreement/offaxis_observe_delta
```

## Input Scenes

```text
res://test-straight-basic-visual-offaxis-observe.tscn
res://test-grin-basic-visual-offaxis-observe.tscn
```

## Artifact List

```text
output/observer_disagreement/offaxis_observe_delta/straight_offaxis_observe_beauty.png
output/observer_disagreement/offaxis_observe_delta/grin_offaxis_observe_beauty.png
output/observer_disagreement/offaxis_observe_delta/straight_offaxis_observe_transport_classification.png
output/observer_disagreement/offaxis_observe_delta/grin_offaxis_observe_transport_classification.png
output/observer_disagreement/offaxis_observe_delta/classification_delta.png
output/observer_disagreement/offaxis_observe_delta/classification_delta_contours.png
output/observer_disagreement/offaxis_observe_delta/classification_delta_summary.json
output/observer_disagreement/offaxis_observe_delta/packet_manifest.json
output/observer_disagreement/offaxis_observe_delta/observability_cutsheet.png
output/observer_disagreement/offaxis_observe_delta/contact_sheet.png
output/observer_disagreement/offaxis_observe_delta/OBSERVER_DISAGREEMENT_SUMMARY.md
```

Sidecars:

```text
output/observer_disagreement/offaxis_observe_delta/straight_offaxis_observe_transport_classification_metadata.json
output/observer_disagreement/offaxis_observe_delta/grin_offaxis_observe_transport_classification_metadata.json
output/observer_disagreement/offaxis_observe_delta/straight_offaxis_observe_transport_classification_coverage.json
output/observer_disagreement/offaxis_observe_delta/grin_offaxis_observe_transport_classification_coverage.json
output/observer_disagreement/offaxis_observe_delta/straight_offaxis_observe_beauty_metadata.json
output/observer_disagreement/offaxis_observe_delta/grin_offaxis_observe_beauty_metadata.json
output/observer_disagreement/offaxis_observe_delta/logs/
```

## Key Metrics

| Metric | Value |
|---|---:|
| Resolution | 480x270 |
| Total pixels | 129,600 |
| Changed pixels | 30,839 |
| Changed ratio | 23.8% |
| Unresolved pixels | 51,232 |
| Unresolved ratio | 39.5% |
| Resolved compared pixels | 78,368 |

Top transitions:

| Transition | Count |
|---|---:|
| geom_hit -> escaped_no_hit | 27,619 |
| escaped_no_hit -> geom_hit | 3,220 |

Classification counts:

| Class | Straight | Curved GRIN |
|---|---:|---:|
| geom_hit | 70,300 | 46,841 |
| escaped_no_hit | 20,964 | 60,295 |
| budget_exhausted | 38,336 | 22,464 |

## Interpretation

The observability cutsheet summarizes the resolved-film context and where classification changed between matched straight/curved transport assumptions. The dominant observed transition is `geom_hit -> escaped_no_hit`, so the packet shows terminal evidence redistributed across the off-axis film plane.

The delta mask and contour panels are comparison artifacts over already-exported classification buffers. They do not introduce new renderer behavior. They make disagreement legible by showing where the terminal classification labels differ.

The beauty frames are context panels only. They are resolved-film captures from the same fixture pair and traversal configuration, without synthetic glow, turbulence, particles, or cinematic effects.

## Limitations

- This packet is measured observability, not proof of correctness.
- Unresolved pixels are evidence limitations and are treated separately by `classification_delta_compare.py`.
- The classification buffer reports terminal categories, not continuous path geometry.
- The beauty frames help connect visible film output to classification redistribution, but they are not disagreement evidence by themselves.
- The observability cutsheet is a presentation artifact; it does not participate in rendering, scheduling, hit selection, traversal, or oracle logic.
- Hermetic observatory output remains useful for export sanity checks but is visually flat for this disagreement use case.

## Next Steps

- Add a quiet disagreement overlay over the resolved film frame, using the same summary schema.
- Add packet-to-packet repeatability checks for the off-axis observe classification export.
- Preserve the same restrained observability visual language for future observer-disagreement packets.
