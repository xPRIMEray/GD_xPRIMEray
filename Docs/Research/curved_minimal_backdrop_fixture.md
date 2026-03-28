# Curved Minimal Backdrop Fixture

## Purpose

`curved_minimal_backdrop` preserves the existing `curved_minimal` fixture and adds one farther rectangular detector plane behind the original sphere target. The goal is to keep the field setup simple while exposing a second downstream hit class for later scheduler experiments.

## Scene Composition

- Same camera pose as `curved_minimal`
- Same central curved-field source
- Same primary sphere target at the origin
- One rear detector / backdrop plane at `z = -18`

The new plane is a broad flat receiver intended to catch rays that miss the primary object after curved transport.

## Expected Hit Classes

- Primary object hits: `fixture_target`
- Detector / backdrop hits: `detector_backdrop`
- Miss / sky: unchanged from the baseline harness

For future analysis, the fixture marks:

- the primary sphere with `fixture_source`
- the detector plane with `fixture_background`
- both intercept surfaces with `fixture_geometry` / `raytrace_geometry`

That lets the renderer keep its legacy source-vs-background fallback for older fixtures while this scene opts into explicit primary-vs-detector classification.

## Difference From `curved_minimal`

`curved_minimal_backdrop` is intentionally a superset of the original fixture:

- same field parameters
- same camera
- same sphere target
- one additional downstream receiver plane only

This makes it useful for comparing front-object hits against farther detector hits without invalidating the existing minimal curved-field baseline.

## Use In Later Scheduler Experiments

This fixture is a good follow-on for tile and subtile experiments because it can separate:

- productive subtiles dominated by primary-object hits
- productive subtiles dominated by downstream detector hits
- empty or low-yield subtiles

In the current render-test harness, this variant now emits explicit class-separated detail:

- `sourceHits` maps to the primary sphere
- `backgroundHits` maps to the rear detector plane through `fixture_background`
- `unclassifiedHits` should stay near zero for this controlled fixture; if it rises, some hits are landing on geometry outside the tagged primary/detector surfaces

That means later scheduler experiments can compare primary vs detector capture directly in existing logs before any larger hit-taxonomy work.

The safest first use is still reorder-only analysis. Once hit classes are visible in telemetry, later experiments can ask whether a subtile ranking that helps primary hits also helps or harms downstream detector capture.
