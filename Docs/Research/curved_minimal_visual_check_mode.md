# Curved Minimal Visual Check Mode

Date: 2026-03-28

## Purpose

Keep the existing curved-minimal-family scheduler-fast render-test baseline unchanged, while adding a separate explicit mode for clearer visual inspection and capture comparisons.

## Runtime Status

The mode token and profile wiring now exist, and the corrected local verification packet was captured on 2026-03-28 under:

- `/home/bb/code/godot_xPRIMEray/output/render_test_visual_compare/corrected_visual_check_2026-03-28`

The original false visual-check runs from:

- `/home/bb/code/godot_xPRIMEray/output/render_test_visual_compare/combined_evidence_2026-03-28`

showed the root cause:

- the runner logged `profile=curved_minimal_visual_check`
- but `PrepareRun()` then reapplied the per-run `TestRunConfig`
- that restored the legacy barebones render-test values before measurement and capture
- so the labeled visual-check packet still ran at `resScale=0.25` and `stride=2`

That overwrite is now fixed by reapplying the curved-minimal visual-check override during run preparation and refreshing the trust/capture targets from the final runtime film settings.

## Mode Split

### Scheduler-fast mode

This remains the default render-test path.

Characteristics:

- legacy `bbNew` render-test baseline
- `FilmResolutionScale = 0.25`
- `PixelStride = 2`
- effective curved-minimal-family render-test grid: `80 x 45`

Use this for:

- scheduler experiments
- historical apples-to-apples comparisons
- low-cost instrumentation runs

### Visual-check mode

This is opt-in via:

```text
--render-test-profile=curved_minimal_visual_check
```

Intended characteristics:

- only applies to `curved_minimal` and `curved_minimal_backdrop`
- keeps the same fixture and scheduler behavior
- should raise sampling to:
  - `FilmResolutionScale = 0.5`
  - `PixelStride = 1`
- intended effective grid:
  - `160 x 90`

Observed characteristics after the fix:

- `scheduler-fast` still logs:
  - `scaledFilm=80x45`
  - `resScale=0.25`
  - `stride=2`
- corrected `visual-check` now logs:
  - `scaledFilm=160x90`
  - `resScale=0.5`
  - `stride=1`
  - `rowsPerStep=90`
- corrected capture filenames now also carry `stride-1`

## Why This Is Safe

- the default scheduler-fast mode is unchanged
- the fix stays localized to `RenderTestRunner` run preparation
- logs now show the final effective runtime values after the last overwrite point
- capture workflows still use `--render-test-capture-mode=visual-check`

## Exact Local Commands

Scheduler-fast curved minimal:

```bash
./scripts/godot_local.sh --path . --scene res://test-curved-minimal.tscn -- \
  --render-test --render-test-fixture=curved_minimal --lifecycle-stress=0 --smartscale=0 \
  --render-test-capture=1 --render-test-capture-dir=output/render_test_visual_compare \
  --render-test-capture-mode=scheduler-fast
```

Visual-check curved minimal:

```bash
./scripts/godot_local.sh --path . --scene res://test-curved-minimal.tscn -- \
  --render-test --render-test-fixture=curved_minimal --lifecycle-stress=0 --smartscale=0 \
  --render-test-profile=curved_minimal_visual_check \
  --render-test-capture=1 --render-test-capture-dir=output/render_test_visual_compare \
  --render-test-capture-mode=visual-check
```

Scheduler-fast curved minimal backdrop:

```bash
./scripts/godot_local.sh --path . --scene res://test-curved-minimal-backdrop.tscn -- \
  --render-test --render-test-fixture=curved_minimal_backdrop --lifecycle-stress=0 --smartscale=0 \
  --render-test-capture=1 --render-test-capture-dir=output/render_test_visual_compare \
  --render-test-capture-mode=scheduler-fast
```

Visual-check curved minimal backdrop:

```bash
./scripts/godot_local.sh --path . --scene res://test-curved-minimal-backdrop.tscn -- \
  --render-test --render-test-fixture=curved_minimal_backdrop --lifecycle-stress=0 --smartscale=0 \
  --render-test-profile=curved_minimal_visual_check \
  --render-test-capture=1 --render-test-capture-dir=output/render_test_visual_compare \
  --render-test-capture-mode=visual-check
```

## Verification Highlights

Corrected runtime verification lines:

- `curved_minimal`:
  - `[RenderTestRunner][EffectiveProfile] run=baseline_prune_off profile=curved_minimal_visual_check resScale=0.5 stride=1 scaledFilm=160x90 rowsPerStep=90`
- `curved_minimal_backdrop`:
  - `[RenderTestRunner][EffectiveProfile] run=baseline_prune_off profile=curved_minimal_visual_check resScale=0.5 stride=1 scaledFilm=160x90 rowsPerStep=90`

Corrected capture examples:

- `/home/bb/code/godot_xPRIMEray/output/render_test_visual_compare/corrected_visual_check_2026-03-28/images/curved_minimal__visual-check__baseline_prune_off__scheduler-baseline-observe-only__subtile-8__targetms-1000__stride-1__runid-1.png`
- `/home/bb/code/godot_xPRIMEray/output/render_test_visual_compare/corrected_visual_check_2026-03-28/images/curved_minimal_backdrop__visual-check__baseline_prune_off__scheduler-baseline-observe-only__subtile-8__targetms-1000__stride-1__runid-1.png`

## Recommendation

Future scheduler comparisons should continue using only scheduler-fast mode.

Visual packs, screenshots, and inspection-oriented captures can now use visual-check mode as the higher-resolution companion path for curved-minimal-family evidence packets.
