# v0.0-pre GRIN Observe Demo

## Canonical Demo Spine

Use the matched GRIN Basic Visual Off-Axis Observe pair:

- Straight/control: `res://test-straight-basic-visual-offaxis-observe.tscn`
- Curved GRIN: `res://test-grin-basic-visual-offaxis-observe.tscn`

Do not use wormhole or overspace scenes as primary evidence for this validation pass.

## Demo HUD

Both canonical scenes include `GrinObserveDemoHud.cs`.

The HUD shows:

- `xPRIMEray v0.0-pre`
- active mode: `Straight Control` or `Curved GRIN Transport`
- fixture label: `GRIN Basic Visual Off-Axis Observe`
- `FilmOverlay2D` state: rays, normals, grid, sampled rays
- current field/transport readout
- diagnostics/export command hint
- scene-switch comparison hint

Function-key cockpit controls during the recording:

- `F1`: help / control overlay
- `F2`: switch matched scene, Straight Control vs Curved GRIN Transport
- `F3`: toggle film rays
- `F4`: toggle hit normals
- `F5`: toggle grid and reference crosshair
- `F6`: toggle comparison view
- `F7`: freeze / unfreeze camera pose
- `F8`: reset camera to canonical pose
- `F9`: capture viewport still packet
- `F10`: export cockpit diagnostics JSON
- `F11`: toggle clean presentation mode
- `F12`: toggle minimal reticle

Movement controls remain reserved for observer/camera navigation. Object and field state stay locked for v0.0-pre.

Hit normals are off by default to keep the recording readable. Rays and a subtle comparison grid/crosshair are available by default.

## Launch Commands

Straight/control:

```bash
./scripts/godot_local.sh --path . --scene res://test-straight-basic-visual-offaxis-observe.tscn
```

Curved GRIN:

```bash
./scripts/godot_local.sh --path . --scene res://test-grin-basic-visual-offaxis-observe.tscn
```

Scene switching is the accepted v0.0-pre fallback for mode comparison.

## Diagnostics / Export Commands

Play-mode cockpit verification:

```bash
bash scripts/run_grin_observe_playmode_verify.sh
```

This launches both canonical observe scenes through Godot scene playback, verifies the active HUD and F1-F12 cockpit map, captures:

- `output/v0.0-pre/straight_control_verify.png`
- `output/v0.0-pre/curved_grin_verify.png`
- `output/v0.0-pre/curved_grin_final_smoke.png`

and writes:

- `output/v0.0-pre/GRIN_OBSERVE_PLAYMODE_VERIFY.md`

Visible Godot playback is the default because this verifier is meant to exercise the same render-mode path used by pressing Play. `--headless` is available for automation environments that can still produce nonblank render targets.

Straight/control still:

```bash
./scripts/godot_local.sh --path . --scene res://test-straight-basic-visual-offaxis-observe.tscn -- \
  --grin-basic-capture=output/v0.0-pre/straight_control.png \
  --grin-basic-min-processed-rows=270 \
  --grin-basic-exit-after-capture=1
```

Curved GRIN still:

```bash
./scripts/godot_local.sh --path . --scene res://test-grin-basic-visual-offaxis-observe.tscn -- \
  --grin-basic-capture=output/v0.0-pre/curved_grin.png \
  --grin-basic-min-processed-rows=270 \
  --grin-basic-exit-after-capture=1
```

The automated observe comparison script also defaults to full film coverage:

```bash
bash scripts/run_grin_observe_v0_pre_full_pixel.sh
```

For the canonical observe scenes, full coverage means `processedRows >= 270`, `filmRowsRendered == filmHeight`, and `unrenderedImageBounds` height is `0`.

Existing reference stills also live under:

- `screenshots/basic_visual_offaxis_observe/2026-03-15/straight_offaxis_observe_reference.png`
- `screenshots/basic_visual_offaxis_observe/2026-03-15/grin_stronger_offaxis_observe.png`

## Walkthrough Script

Target length: 3-6 minutes.

1. Intro, 20-30 seconds  
   "This is xPRIMEray v0.0-pre. This demo validates the instrument workflow: open a scene, compare straight and curved transport, inspect overlays, and surface diagnostics."

2. Open Straight Control, 45-60 seconds  
   Open `test-straight-basic-visual-offaxis-observe.tscn`. Identify it as the control fixture. Show the stable render and readable overlay.

3. Open Curved GRIN, 60-90 seconds  
   Open `test-grin-basic-visual-offaxis-observe.tscn`. Point out that the scene is matched, but transport is now GRIN/curved. Toggle or show `FilmOverlay2D`.

4. Compare, 60-90 seconds  
   Switch back and forth or show captured stills side by side. Explain only observable differences: path bending and changed hit pattern relative to control.

5. Diagnostics/Export, 30-60 seconds  
   Show the capture/export path or output folder. Frame it as diagnostic workflow visibility, not physics proof.

6. Close, 10-20 seconds  
   "v0.0-pre passes if the viewer can identify mode, inspect overlays, compare straight vs curved outputs, and find diagnostics."

## OBS Settings

- Canvas/output: `1920x1080`
- FPS: `30`
- Format: `MKV`, remux to `MP4`
- Encoder: hardware H.264 if available, otherwise x264
- Quality: CQP/CQ `18-22` or CBR `12000-20000 Kbps`
- Capture: Godot window capture preferred
- Audio: 48 kHz voice, light noise suppression only if clean
- Keep Godot at fixed 16:9; avoid resizing during a take

## Take 001 Recording Path

Use the smoke-verified play-mode path immediately before recording:

```bash
bash scripts/run_grin_observe_playmode_verify.sh
```

Then record the walkthrough in OBS using the canonical scene sequence:

1. Start with `res://test-straight-basic-visual-offaxis-observe.tscn`.
2. Press `F1` to show the control overlay.
3. Use `F3-F6` to show the readable overlay stack.
4. Use `F9-F10` to show screenshot and diagnostics export.
5. Use `F2` to switch to the matched curved scene, or open `res://test-grin-basic-visual-offaxis-observe.tscn` directly if the recording machine needs the v0.0-pre scene-switch fallback.
6. Show `output/v0.0-pre/GRIN_OBSERVE_PLAYMODE_VERIFY.md` and the generated PNG artifacts.

Recording log:

- [`GRIN_OBSERVE_V0_PRE_RECORDING_LOG.md`](GRIN_OBSERVE_V0_PRE_RECORDING_LOG.md)
- [`GRIN_OBSERVE_CAPTURE_ENVIRONMENT.md`](GRIN_OBSERVE_CAPTURE_ENVIRONMENT.md)

Semi-automated capture payload:

```bash
bash scripts/setup_grin_observe_capture_tools.sh
bash scripts/record_grin_observe_v0_pre_take001.sh
```

If live capture tools are unavailable, the recording script falls back to verified-frame MP4 assembly:

```bash
bash scripts/build_grin_observe_demo_from_frames.sh
```

Full-pixel automation is still a pre-tag release gate. Run it on faster GPU hardware before tagging:

```bash
bash scripts/run_grin_observe_v0_pre_full_pixel.sh
```

Take 001 may use the smoke-verified play-mode report, but the release tag should wait for full-pixel automation over both canonical scenes.

## Acceptance Criteria

Passes when:

- both canonical scenes load cleanly
- straight/control and curved GRIN modes are visibly labeled
- `FilmOverlay2D` is readable in the recording
- play-mode verification produces `GRIN_OBSERVE_PLAYMODE_VERIFY.md`
- F1-F12 controls report `PASS` or an explicit non-pass state; missing controls do not silently pass
- automated still packets cover the full film buffer, not a partial row slice
- exported PNG artifacts receive a full-pixel coverage scan with dimensions, total pixels, non-background pixels, traced/marked pixels when available, coverage percentage, checksum, and pass/fail threshold
- straight vs curved outputs can be compared without reading source code
- the viewer can explain what changed in observable transport terms
- diagnostics/export path is shown
- video is 3-6 minutes long
- no wormhole/overspace scene is used as primary evidence
- no exotic physics claim is made

Fails when:

- mode identity is ambiguous
- overlays obscure the render
- curved/control scenes are not matched enough for comparison
- diagnostics/export are only mentioned, not shown
- narration implies proof of new physics
