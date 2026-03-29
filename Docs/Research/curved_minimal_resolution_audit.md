# Curved Minimal Resolution Audit

Date: 2026-03-28

## Question

Why do `curved_minimal` and `curved_minimal_backdrop` often look visibly more blocky than other fixtures?

## Short Answer

The blockiness is real, not just perceptual.

It is caused primarily by a much smaller effective film grid on the curved-minimal render-test path, and in render-test mode that coarse grid is pushed even lower by an intentional legacy `bbNew` baseline override.

Perception does add to it:

- curved-minimal-family uses a film overlay path that can emphasize coarse pixel blocks
- the scene was originally tuned for fast scheduler experiments rather than visual presentation

## Effective Resolution Pipeline

### Authored curved-minimal family scene settings

Both scenes define:

- base film size: `320 x 180`
- `FilmResolutionScale = 0.5`
- default `PixelStride = 1` unless changed later

That gives a scene-authored film buffer of:

- `160 x 90`
- `14,400` film pixels

Files:

- [test-curved-minimal.tscn](/home/bb/code/godot_xPRIMEray/test-curved-minimal.tscn)
- [test-curved-minimal-backdrop.tscn](/home/bb/code/godot_xPRIMEray/test-curved-minimal-backdrop.tscn)

### Render-test baseline override

When the `RenderTestRunner` starts in render-test mode, it applies the legacy `bbNew` baseline profile to the film:

- quality mode forced to `Barebones`
- `FilmResolutionScale = 0.25`
- `Width = 320`
- `Height = 180`
- `PixelStride = 2`

That produces a render-test baseline film of:

- `80 x 45`
- `3,600` sampled film pixels before stride fill

Source:

- [RenderTestRunner.cs](/home/bb/code/godot_xPRIMEray/RendererCore/Testing/RenderTestRunner.cs#L737)
- [GrinFilmCamera.cs](/home/bb/code/godot_xPRIMEray/GrinFilmCamera.cs#L12845)

Confirmed in existing curved-minimal direct log:

- `scaledFilm=80x45`
- `resScale=0.25`
- `stride=2`

Source:

- [curved_minimal_direct.log](/home/bb/code/godot_xPRIMEray/output/render_test_scheduler_compare/direct_probe/curved_minimal_direct.log)

## Comparison Against Less Blocky Fixtures

Two cleaner-looking comparison fixtures:

- [test-grin-basic-visual-minimal.tscn](/home/bb/code/godot_xPRIMEray/test-grin-basic-visual-minimal.tscn)
- [test-metric-basic-visual-minimal.tscn](/home/bb/code/godot_xPRIMEray/test-metric-basic-visual-minimal.tscn)

They use:

- base film size: `640 x 360`
- `FilmResolutionScale = 0.75`
- default `PixelStride = 1`

That yields:

- `480 x 270`
- `129,600` film pixels

Comparison:

- curved-minimal scene-authored path: `160 x 90` = `14,400` pixels
- curved-minimal render-test baseline path: `80 x 45` = `3,600` pixels
- basic-visual comparison path: `480 x 270` = `129,600` pixels

So:

- scene-authored curved-minimal is already `9x` lower pixel count than the basic-visual comparison fixtures
- render-test curved-minimal baseline is `36x` lower pixel count than those comparison fixtures

## Display / Perceptual Contributors

The main issue is real sampling density, but a few display choices can make it look even coarser:

- curved-minimal-family uses `DrawFilmGradientNormals = true`
- the render-test baseline sets `FilmOpacity = 0.8`
- the HUD/runtime reports `FILM_ACCUM=ON`

Those do not create the low resolution by themselves, but they can make block edges and coarse accumulation more obvious.

## Is This Fixture-Specific?

Partly.

The authored curved-minimal-family scenes are lower resolution than the cleaner basic-visual fixtures.

But the extra drop to `80 x 45` is not unique to curved-minimal. It comes from the shared render-test baseline path, and existing blackhole/einstein render-test logs show the same `scaledFilm=80x45` baseline.

So the curved-minimal family inherits:

1. a genuinely smaller authored film grid than the basic-visual fixtures
2. the shared legacy render-test coarse baseline used for speed and repeatability

## Most Likely Reason

The most likely reason the curved-minimal family looks more pixelated is:

- intentional legacy tuning for fast render-test and scheduler characterization

That tuning is visible in two places:

1. the scene itself starts relatively small at `320x180` with `0.5` scale
2. render-test mode then applies the `bbNew` `Barebones` baseline, lowering the effective scale to `0.25` and using `PixelStride = 2`

This matches earlier scheduler work, where the docs already treated the curved-minimal path as effectively `80px` wide for subtile experiments.

## Recommendation

Safest next adjustment:

- keep the current coarse setting for scheduler experiments
- add a separate higher-resolution comparison mode for visual checks

Recommended visual-check mode:

- same scheduler behavior
- same fixture geometry
- increase only visual sampling settings, for example:
  - keep `PixelStride = 1`
  - use at least scene-authored `FilmResolutionScale = 0.5`
  - optionally compare `0.75` if runtime cost is acceptable

Why this is safest:

- it avoids invalidating current scheduler characterization history
- it preserves the existing low-cost experimental baseline
- it gives a clean A/B path for judging whether scheduler results still hold when the image is less coarse

## Final Recommendation Choice

Best choice from the requested options:

2. add a higher-resolution comparison mode

Why not `1` alone:

- the current coarse mode is appropriate for scheduler experiments, but it is too easy to misread visually

Why not `3` yet:

- promoting a new default would shift the established render-test baseline before the higher-resolution path has been validated alongside the scheduler experiments
