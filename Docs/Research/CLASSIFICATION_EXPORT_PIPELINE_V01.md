# Classification Export Pipeline V01

## Scope

This pipeline exports normalized transport classification buffers from the existing hermetic observatory fixtures. It is export-only instrumentation.

It does not change transport semantics, scheduler behavior, traversal order, hit selection, resolver decisions, or oracle behavior. The export reuses the existing `GrinBasicVisualController` analysis capture mode `transport_classification`, which copies the measured `GrinFilmCamera` fixture classification buffer after the fixture has rendered.

## Export Lifecycle

Hermetic observatory export remains the sanity gate for the plumbing. It is
useful for deterministic validation, metadata checks, and palette checks, but
the sealed chamber can produce mostly flat `background_hit` images and should
not be treated as the primary observer-disagreement visual artifact.

Run the hermetic observatory capture runner with classification export enabled:

```bash
bash scripts/run_hermetic_observatory_full_pixel.sh --export-classification
```

The runner performs the normal hermetic observatory captures first, then re-runs each selected hermetic fixture with:

```text
--grin-basic-analysis-capture-mode=transport_classification
```

The second pass is a capture pass only. It asks the runtime to save the normalized classification buffer instead of the resolved film image. The same fixture scene, camera, row minimum, settle frames, and exit-after-capture path are used.

For the visually meaningful paired observe packet, run:

```bash
GODOT_EXE=./scripts/godot_local.sh python3 tools/observer_disagreement_offaxis_export.py
```

That runner captures only:

```text
res://test-straight-basic-visual-offaxis-observe.tscn
res://test-grin-basic-visual-offaxis-observe.tscn
```

It writes measured straight/curved classification buffers and immediately runs
`tools/classification_delta_compare.py` against them.

## Artifact Naming

Default output root:

```text
output/v0.0-pre/
```

Full hermetic pair artifacts:

```text
output/v0.0-pre/hermetic_straight_transport_classification.png
output/v0.0-pre/hermetic_grin_transport_classification.png
output/v0.0-pre/hermetic_straight_transport_classification_metadata.json
output/v0.0-pre/hermetic_grin_transport_classification_metadata.json
output/v0.0-pre/hermetic_straight_transport_classification_coverage.json
output/v0.0-pre/hermetic_grin_transport_classification_coverage.json
```

When both classification PNGs validate, the runner also invokes `tools/classification_delta_compare.py` and writes the first disagreement packet here:

```text
output/v0.0-pre/classification_delta/classification_delta.png
output/v0.0-pre/classification_delta/classification_delta_contours.png
output/v0.0-pre/classification_delta/classification_delta_summary.json
```

Use `--skip-classification-delta` to export the straight/GRIN buffers without producing the comparison packet.

Off-axis observe disagreement packet:

```text
output/observer_disagreement/offaxis_observe_delta/straight_offaxis_observe_transport_classification.png
output/observer_disagreement/offaxis_observe_delta/grin_offaxis_observe_transport_classification.png
output/observer_disagreement/offaxis_observe_delta/straight_offaxis_observe_transport_classification_metadata.json
output/observer_disagreement/offaxis_observe_delta/grin_offaxis_observe_transport_classification_metadata.json
output/observer_disagreement/offaxis_observe_delta/straight_offaxis_observe_transport_classification_coverage.json
output/observer_disagreement/offaxis_observe_delta/grin_offaxis_observe_transport_classification_coverage.json
output/observer_disagreement/offaxis_observe_delta/classification_delta.png
output/observer_disagreement/offaxis_observe_delta/classification_delta_contours.png
output/observer_disagreement/offaxis_observe_delta/classification_delta_summary.json
output/observer_disagreement/offaxis_observe_delta/packet_manifest.json
```

## Palette Normalization

The exported PNG palette is produced by `GrinFilmCamera.TryCopyTransportClassificationFilmImageForTesting(...)`.

That path normalizes the fixture transport classification image before saving. The expected labels and colors match `tools/classification_delta_compare.py`:

| Class | RGB |
|---|---:|
| `geom_hit` | `41,184,66` |
| `portal_hit` | `46,209,235` |
| `throat_event` | `242,199,46` |
| `throat_entry` | `245,209,41` |
| `throat_exit` | `245,107,36` |
| `throat_shell_transform` | `184,82,235` |
| `throat_inner_absorb` | `117,41,36` |
| `background_hit` | `82,112,219` |
| `escaped_no_hit` | `140,43,43` |
| `budget_exhausted` | `242,46,46` |

Black classification pixels are normalized to `budget_exhausted` by the runtime export path. The comparator mirrors that normalization so older or partially initialized captures do not create an invented class.

## Metadata Expectations

Each metadata JSON records:

- fixture id, label, scene, and transport assumption
- capture mode and classification PNG path
- film dimensions and analysis image dimensions
- scheduler/traversal mode from existing log telemetry
- traversal rows completed and requested minimum rows
- whether the transport classification buffer was written
- validation status and hermetic failures, if any

The runner fails the classification export if:

- `analysisCaptureMode` is not `transport_classification`
- `transportClassificationWritten` is not `1`
- analysis image dimensions are missing
- analysis image dimensions do not match final film dimensions

The coverage JSON is a structured sidecar for the existing `[GrinBasicVisual][Coverage]` log line. It does not add new measurements.

## Determinism

The export files are deterministic with respect to the fixture run and renderer state. Metadata sidecars intentionally avoid wall-clock timestamps. The normal validation report still includes a generated timestamp because it predates this packet format and is not used as comparator metadata.

## Limitations

The exported buffers are classification captures, not proof of transport correctness. They show the terminal class assigned by the current fixture instrumentation under the selected transport assumption.

The pipeline does not claim:

- physical uncertainty estimation
- topology proof
- oracle authority
- semantic correctness beyond the measured classification buffer
- live disagreement state

Unknown or unresolved pixels remain evidence limitations, not visual opportunities. They should be marked quietly by downstream comparison tools.

## Future Disagreement Overlay Path

The current path is capture-first:

1. export straight classification PNG and sidecars
2. export curved GRIN classification PNG and sidecars
3. compare the measured buffers with `classification_delta_compare.py`
4. display changed regions quietly, with unchanged regions transparent or nearly silent

A future live overlay can reuse the same normalized palette and summary schema, but it must still be presentation-only. Live mode should synchronize paired captures or paired observatory surfaces without changing scheduler order, transport semantics, hit resolution, or oracle behavior.

## Guardrails

Do not add glow effects, decorative particles, turbulence, synthetic field energy, rainbow palettes, or invented contours. A classification delta is only valid where the exported straight and curved buffers contain measured, comparable classification values.

The observatory frame should remain calm: one dominant comparison concept at a time, sparse annotation, and no dashboard flattening.
