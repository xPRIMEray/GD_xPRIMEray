# Hermetic Fixture Rule

Overspace validation fixtures are renderer-validation scenes, not presentation demos.

## Canon

"A scene promoted to an overspace validation fixture should be hermetically enclosed and should produce 100% classified pixel outcomes under GrinFilmCamera transport, within the configured transport budget."

This is a validation-harness rule for enclosed overspace scenes. It is not a claim about universal physics truth.

## Interpretation

Do not frame the rule as "every pixel must hit solid geometry."

Frame it as:

- every pixel must return a classified transport result

For a sealed overspace room, the target is:

- `classified_coverage_ratio = 1.0`
- `escaped_no_hit = 0`
- `budget_exhausted = 0`
- `unclassified = 0`

## Classified Pixel Outcomes

- `geom_hit`: the ray resolved against ordinary scene geometry
- `portal_hit`: the ray resolved against explicit portal-frame or portal-surface geometry
- `throat_event`: aggregate bucket for throat-facing outcomes; in Phase B.0 this is the sum of the throat subcategories below
- `throat_entry`: the ray crossed a boundary-layer throat shell and continued, but the final resolved hit was not a post-remap transform hit
- `throat_exit`: the ray accumulated repeated throat remaps (`BoundaryRemapCount >= 2`), indicating a deeper linked-shell exit-style traversal path
- `throat_shell_transform`: the final resolved hit occurred after a `BoundaryLayerVolume` `SceneTransform` remap
- `throat_inner_absorb`: the ray terminated inside the throat-facing absorbing inner-radius region without a normal geometry hit
- `background_hit`: only valid when the fixture intentionally allows a background class
- `escaped_no_hit`: the ray returned no classified result before leaving the transport solve; should be near zero in hermetic fixtures
- `budget_exhausted`: the renderer stopped on transport budget rather than on a classified scene result; should be near zero in hermetic fixtures

## Phase B.0 Note

The current throat taxonomy is intentionally narrow-seam and additive:

- it refines the old `throat_event` bucket
- it does not replace the current Phase A portal transport
- it remains aligned to the existing `BoundaryLayerVolume` seam

`throat_shell_bias` is not emitted yet because per-pixel `DirectionBias` attribution is not currently surfaced through the active renderer classification path. That can be added later without changing the Hermetic Fixture Rule itself.

## Validation Reading Guide

For hermetic overspace fixture outputs:

- `coverage.json` is the machine-readable truth source
- `coverage.txt` is the quick human scan
- `summary.json` should mirror the same counts plus run metadata

If classified coverage drops below 100%, treat that as a renderer or harness gap to expose, not something to hide.
