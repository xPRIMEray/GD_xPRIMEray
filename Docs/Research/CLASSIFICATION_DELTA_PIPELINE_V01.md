# Classification Delta Pipeline V01

## Summary

The Classification Delta pipeline is the first capture-first artifact path for the
Observer Disagreement Observatory. It compares two already captured transport
classification images:

- straight transport reference
- curved GRIN transport

It produces a quiet measured-disagreement overlay and JSON summary. The pipeline is
presentation-only instrumentation. It does not run the renderer and does not change
transport behavior.

Implementation:

```text
tools/classification_delta_compare.py
```

Primary outputs:

- `classification_delta.png`
- `classification_delta_summary.json`
- `classification_delta_contours.png`

## Inputs

Required:

- `--straight`: straight-reference normalized classification PNG
- `--curved`: curved-GRIN normalized classification PNG
- `--out-dir`: output directory

Optional:

- `--straight-metadata`: straight capture metadata JSON
- `--curved-metadata`: curved capture metadata JSON
- `--straight-coverage`: straight coverage summary JSON
- `--curved-coverage`: curved coverage summary JSON
- `--require-metadata`: fail if metadata is missing or compared keys mismatch
- `--metadata-key`: explicit metadata key to compare; may be repeated
- `--treat-escaped-unresolved`: treat `escaped_no_hit` as unresolved evidence
- `--skip-contours`: skip `classification_delta_contours.png`

Example:

```bash
python3 tools/classification_delta_compare.py \
  --straight output/straight/transport_classification.png \
  --curved output/curved/transport_classification.png \
  --straight-coverage output/straight/coverage.json \
  --curved-coverage output/curved/coverage.json \
  --out-dir output/observer_disagreement/classification_delta
```

## Metadata Requirements

The comparator always requires image dimensions to match. Metadata is optional by
default because older capture packets do not consistently write the same schema.

When metadata is supplied, the tool compares common keys when present:

- `width`
- `height`
- `fixture`
- `fixture_id`
- `fixture_label`
- `camera_pose_key`
- `traversal_mode`
- `scheduler_mode`
- `render_test_traversal_pass1_pass2`

Use `--require-metadata` for stricter runs. Use repeated `--metadata-key` arguments
when a capture pipeline has a known schema:

```bash
python3 tools/classification_delta_compare.py \
  --straight straight_classification.png \
  --curved curved_classification.png \
  --straight-metadata straight_metadata.json \
  --curved-metadata curved_metadata.json \
  --metadata-key camera.pose_key \
  --metadata-key render.traversal_mode \
  --require-metadata \
  --out-dir output/delta
```

Matched metadata should establish:

- same resolution
- same camera pose
- same fixture pair or accepted straight/curved pair
- same traversal/scheduler capture settings
- explicit transport labels

## Comparison Semantics

Each pixel is classified using the renderer's fixture transport classification
palette:

- `geom_hit`
- `portal_hit`
- `throat_event`
- `throat_entry`
- `throat_exit`
- `throat_shell_transform`
- `throat_inner_absorb`
- `background_hit`
- `escaped_no_hit`
- `budget_exhausted`

Black classification pixels are normalized to `budget_exhausted`, matching
`GrinFilmCamera.NormalizeFixtureTransportClassificationPixel(...)`.

Per-pixel result:

| Result | Rule | Visual treatment |
| --- | --- | --- |
| Unchanged | straight class equals curved class | transparent |
| Changed | straight class differs from curved class | restrained blue-cyan tint |
| Unresolved | either side is unknown/unclassified/budget exhausted | neutral gray mark |

`escaped_no_hit` is treated as a measured class by default. For hermetic observatory
captures where escape should be considered missing evidence, pass
`--treat-escaped-unresolved`.

## Outputs

`classification_delta.png`

- transparent where classifications are unchanged
- low-opacity tint where classifications differ
- neutral gray where evidence is unresolved
- no glow, turbulence, particles, or synthetic field visuals

`classification_delta_contours.png`

- transparent background
- one-pixel contours around changed regions
- neutral contours around unresolved regions
- derived only from the measured delta mask

`classification_delta_summary.json`

Includes:

- input paths
- image dimensions
- unchanged pixel count
- changed pixel count
- changed ratio
- unresolved pixel count
- unresolved ratio
- class counts for straight and curved inputs
- top transition buckets such as `background_hit->geom_hit`
- optional metadata comparison results
- optional coverage summaries
- output paths
- guardrails

## Limitations

This pipeline compares fixture transport classifications, not full transport state.

It does not prove:

- physical correctness
- real spacetime topology
- geodesic validity
- ownership topology by itself
- collider identity changes unless collider-id artifacts are provided separately
- confidence unless confidence telemetry is provided separately

Classification equality is not equivalent to full transport equality. Two pixels may
share a classification while differing in collider id, hit point, normal, path
length, or oracle closure. Those require later comparison layers.

## Unsupported Claims

Do not use this artifact to claim:

- "the field changed space"
- "topology emerged" without connected owner/domain evidence
- "oracle verified the disagreement"
- "unchanged pixels have identical ray paths"
- "changed pixels are unstable"
- "unresolved pixels are stronger disagreement"

Acceptable language:

- "classification changed between matched transport assumptions"
- "visibility class redistributed under curved-GRIN transport"
- "this region requires deeper owner/domain/oracle inspection"

## Future Live-Overlay Path

The low-risk path is capture-first. A future live overlay can be added after this
artifact language is validated.

Future live path:

- synchronized straight and curved surfaces
- completed classification buffers from both surfaces
- presentation-only delta node
- optional confidence input from paired domain telemetry
- one dominant comparison concept per mode

The live path must still compare completed measured outputs only. It must not change
transport, scheduling, hit selection, resolver decisions, or oracle behavior.

## Validation

Expected validation checks:

- deterministic output for the same input images
- image dimensions must match
- optional metadata comparison reports or rejects mismatches
- unchanged regions remain transparent
- changed regions use restrained tint only
- unresolved regions use separate neutral marking
- transition buckets are counted from measured classification pixels
- no runtime semantic changes
- no scheduler modifications
- no transport modifications
- no synthetic overlays

When in doubt, keep the artifact quieter and report the limitation in JSON.
