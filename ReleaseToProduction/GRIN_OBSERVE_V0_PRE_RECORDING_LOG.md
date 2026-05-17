# v0.0-pre GRIN Observe Recording Log

## Take 001

Status: `FALLBACK MP4 PRODUCED; OBS OPERATOR CAPTURE STILL RECOMMENDED`

This take uses the smoke-verified play-mode path:

```bash
bash scripts/run_grin_observe_playmode_verify.sh
```

Current verification evidence:

- Report: `output/v0.0-pre/GRIN_OBSERVE_PLAYMODE_VERIFY.md`
- Straight/control still: `output/v0.0-pre/straight_control_verify.png`
- Curved GRIN still: `output/v0.0-pre/curved_grin_verify.png`
- Curved final smoke: `output/v0.0-pre/curved_grin_final_smoke.png`
- Straight diagnostics: `output/v0.0-pre/grin_observe_straight_control_diagnostics.json`
- Curved diagnostics: `output/v0.0-pre/grin_observe_curved_grin_transport_diagnostics.json`
- Fallback video: `output/v0.0-pre/video/xprimeray_grin_observe_v0_pre_take001_fallback.mp4`
- Manifest: `output/v0.0-pre/video/TAKE001_MANIFEST.md`

Recording command sequence:

```bash
bash scripts/run_grin_observe_playmode_verify.sh
./scripts/godot_local.sh --path . --scene res://test-straight-basic-visual-offaxis-observe.tscn
```

OBS operator path:

1. Start OBS with a 1920x1080, 30 FPS Godot window capture.
2. Start recording to MKV.
3. Open the straight/control scene first.
4. Press `F1` and show the cockpit control map.
5. Show `F3` rays, `F4` normals, `F5` grid, and `F6` comparison view only long enough to establish discoverability.
6. Use `F9` and `F10` to show screenshot and diagnostics export.
7. Use `F2` to switch to the matched Curved GRIN scene, or open it directly if scene switching is unstable on the recording machine.
8. Repeat only the essential overlay checks, then show the output folder/report.
9. Stop recording and remux MKV to MP4.

Narration constraint:

- This recording validates coherent transport instrumentation and usability.
- Do not claim exotic physics proof.
- Observable comparison language is acceptable: path bending, changed hit pattern, matched control/curved fixture.

Release gate:

- The play-mode smoke verifier may be used for Take 001.
- Before tagging release, run the full-pixel automation on faster GPU hardware:

```bash
bash scripts/run_grin_observe_v0_pre_full_pixel.sh
```

The release tag should not be cut until the full-pixel automation confirms complete render coverage for both canonical scenes.

Environment note:

- This shell environment does not currently expose OBS or system ffmpeg. A project-local Python fallback stack produced an MP4 from verified still artifacts. This is a valid repeatable payload, but it is not a live OBS walkthrough.
