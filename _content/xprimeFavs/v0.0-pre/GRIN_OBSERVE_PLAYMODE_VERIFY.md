# GRIN Observe Play-Mode Verify

- Timestamp UTC: `2026-05-19T01:23:59Z`
- Summary: `PASS`
- Demo spine: `GRIN Basic Visual Off-Axis Observe`
- Primary scenes:
  - `test-straight-basic-visual-offaxis-observe.tscn`
  - `test-grin-basic-visual-offaxis-observe.tscn`
- Scope: v0.0-pre coherent transport instrumentation, not exotic physics proof

## Active Keymap

| Key | v0.0-pre control |
| --- | --- |
| F1 | Help / Control overlay |
| F2 | Toggle Straight Control vs Curved Transport by matched scene switch |
| F3 | Toggle Film Rays |
| F4 | Toggle Hit Normals |
| F5 | Toggle Grid / Reference Crosshair |
| F6 | Toggle Difference / Comparison View |
| F7 | Freeze / Unfreeze Camera |
| F8 | Reset Camera to Canonical Pose |
| F9 | Capture Screenshot / Still Packet |
| F10 | Export Diagnostics |
| F11 | Toggle Clean Presentation Mode |
| F12 | Toggle Crosshair / Minimal Reticle |

## Scene Results

| Role | Scene | Runner | Return | Detail | Log | JSON |
| --- | --- | --- | --- | --- | --- | --- |
| straight_control | `res://test-straight-basic-visual-offaxis-observe.tscn` | `PASS` | `0` | Godot return=0; verifier summary=PASS | `output/v0.0-pre/playmode_verify_straight_control.log` | `output/v0.0-pre/playmode_verify_straight_control.json` |
| curved_grin | `res://test-grin-basic-visual-offaxis-observe.tscn` | `PASS` | `0` | Godot return=0; verifier summary=PASS | `output/v0.0-pre/playmode_verify_curved_grin.log` | `output/v0.0-pre/playmode_verify_curved_grin.json` |

## Control Checks

| Role | Area | Check | Status | Detail |
| --- | --- | --- | --- | --- |
| straight_control | scene boot | canonical scene loaded | `PASS` | res://test-straight-basic-visual-offaxis-observe.tscn |
| straight_control | scene boot | GrinObserveDemoHud active | `PASS` | ../CanvasLayer/DemoHud |
| straight_control | scene boot | FilmOverlay2D active | `PASS` | ../CanvasLayer/FilmOverlay2D |
| straight_control | scene boot | GrinFilmCamera active | `PASS` | ../GrinFilmCamera |
| straight_control | scene boot | paired comparison scene reachable | `PASS` | res://test-grin-basic-visual-offaxis-observe.tscn |
| straight_control | scene boot | no fatal runtime errors during first frames | `PASS` | verifier reached frame 20 |
| straight_control | user behavior | objects locked for v0.0-pre | `PASS` | verification does not expose object manipulation controls |
| straight_control | key conflict | renderer F6-F10 macro hotkeys disabled | `PASS` | GrinFilmCamera.RuntimeMacroHotkeysEnabled=false |
| straight_control | key conflict | RayBeamRenderer debug hotkeys disabled | `PASS` | no active RayBeamRenderer debug hotkeys found |
| straight_control | controls | F1 | `PASS` | help overlay visible=off |
| straight_control | controls | F2 | `PASS` | paired scene reachable: res://test-grin-basic-visual-offaxis-observe.tscn; verification does not switch scenes |
| straight_control | controls | F3 | `PASS` | film rays=off |
| straight_control | controls | F4 | `PASS` | hit normals=on |
| straight_control | controls | F5 | `PASS` | grid=off reticle=off |
| straight_control | controls | F6 | `PASS` | comparison view=on |
| straight_control | controls | F7 | `PASS` | camera frozen=on |
| straight_control | controls | F8 | `PASS` | camera reset to canonical pose |
| straight_control | controls | F9 | `PASS` | output/v0.0-pre/grin_observe_straight_control_still.png |
| straight_control | controls | F10 | `PASS` | output/v0.0-pre/grin_observe_straight_control_diagnostics.json |
| straight_control | controls | F11 | `PASS` | clean presentation=on |
| straight_control | controls | F12 | `PASS` | minimal reticle=off |
| curved_grin | scene boot | canonical scene loaded | `PASS` | res://test-grin-basic-visual-offaxis-observe.tscn |
| curved_grin | scene boot | GrinObserveDemoHud active | `PASS` | ../CanvasLayer/DemoHud |
| curved_grin | scene boot | FilmOverlay2D active | `PASS` | ../CanvasLayer/FilmOverlay2D |
| curved_grin | scene boot | GrinFilmCamera active | `PASS` | ../GrinFilmCamera |
| curved_grin | scene boot | paired comparison scene reachable | `PASS` | res://test-straight-basic-visual-offaxis-observe.tscn |
| curved_grin | scene boot | no fatal runtime errors during first frames | `PASS` | verifier reached frame 20 |
| curved_grin | user behavior | objects locked for v0.0-pre | `PASS` | verification does not expose object manipulation controls |
| curved_grin | key conflict | renderer F6-F10 macro hotkeys disabled | `PASS` | GrinFilmCamera.RuntimeMacroHotkeysEnabled=false |
| curved_grin | key conflict | RayBeamRenderer debug hotkeys disabled | `PASS` | no active RayBeamRenderer debug hotkeys found |
| curved_grin | controls | F1 | `PASS` | help overlay visible=off |
| curved_grin | controls | F2 | `PASS` | paired scene reachable: res://test-straight-basic-visual-offaxis-observe.tscn; verification does not switch scenes |
| curved_grin | controls | F3 | `PASS` | film rays=off |
| curved_grin | controls | F4 | `PASS` | hit normals=on |
| curved_grin | controls | F5 | `PASS` | grid=off reticle=off |
| curved_grin | controls | F6 | `PASS` | comparison view=on |
| curved_grin | controls | F7 | `PASS` | camera frozen=on |
| curved_grin | controls | F8 | `PASS` | camera reset to canonical pose |
| curved_grin | controls | F9 | `PASS` | output/v0.0-pre/grin_observe_curved_grin_transport_still.png |
| curved_grin | controls | F10 | `PASS` | output/v0.0-pre/grin_observe_curved_grin_transport_diagnostics.json |
| curved_grin | controls | F11 | `PASS` | clean presentation=on |
| curved_grin | controls | F12 | `PASS` | minimal reticle=off |

## Pixel Coverage

| Role | Artifact | Width | Height | Total Pixels | Non-background | Traced/Marked | Coverage | SHA-256 | Status |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |
| straight_control | `output/v0.0-pre/straight_control_verify.png` | 480 | 270 | 129600 | 125498 | 16064 | 96.834877% | `03115ea7271014edd3866f77378bacee888ad142929ad45ba55ad5c70c4efcda` | `PASS` |
| curved_grin | `output/v0.0-pre/curved_grin_verify.png` | 480 | 270 | 129600 | 122562 | 16640 | 94.569444% | `d50738a197ad0fa3bd07682fc7d8e1a1af71bfc074970d3758318209953781eb` | `PASS` |
| curved_grin | `output/v0.0-pre/curved_grin_final_smoke.png` | 480 | 270 | 129600 | 122562 | 16640 | 94.569444% | `d50738a197ad0fa3bd07682fc7d8e1a1af71bfc074970d3758318209953781eb` | `PASS` |

## Acceptance Summary

- Both canonical scenes load cleanly: `PASS`
- HUD is present and active: `PASS`
- F1-F12 cockpit controls are visible and verified: `PASS`
- Straight vs curved scene-switch comparison path is reachable: `PASS`
- Diagnostics/export path is shown and exercised: `PASS`
- Full-pixel artifact pass completed: `PASS`
- Wormhole/overspace scenes used as primary evidence: `NO`
- Exotic physics claim made: `NO`

## Known Limitations

- Automated F2 verification checks matched-scene reachability; in normal Godot Play mode F2 performs the scene switch.
- Pixel coverage scans every exported viewport pixel and records renderer traced-pixel stats when available; it is not an advanced physics validation.
- Movement controls remain Godot/player camera navigation controls. v0.0-pre intentionally does not expose object manipulation.
