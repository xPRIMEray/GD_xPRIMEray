# TAKE001 Manifest

- Timestamp UTC: `2026-05-18T00:29:39Z`
- Status: `PASS`
- Capture method used: `fallback frames`
- Video artifact path: `output/v0.0-pre/video/xprimeray_grin_observe_v0_pre_take001_fallback.mp4`
- Resolution/FPS/codec: `1920x1080 @ 30 FPS, H.264 MP4`
- Runtime: `50.0s`
- Video SHA-256: `9126462242946a9f08cd1dd86e38327d823fe675d69d1658ca55d448a1830b83`
- Verification report path: `output/v0.0-pre/GRIN_OBSERVE_PLAYMODE_VERIFY.md`

## Section Timing

| Start | End | Section | Type | Artifact / Note |
| ---: | ---: | --- | --- | --- |
| `00:00.0` | `00:06.0` | Title | title card | `Opening title and scope.` |
| `00:06.0` | `00:09.5` | Straight Control | section card | `Section card.` |
| `00:09.5` | `00:17.5` | Straight Control Artifact | verified still | `output/v0.0-pre/straight_control_verify.png` |
| `00:17.5` | `00:21.0` | Curved GRIN Transport | section card | `Section card.` |
| `00:21.0` | `00:29.0` | Curved GRIN Artifact | verified still | `output/v0.0-pre/curved_grin_verify.png` |
| `00:29.0` | `00:32.5` | Overlay / Diagnostics | section card | `Section card.` |
| `00:32.5` | `00:40.5` | Diagnostics Artifact | verified still | `output/v0.0-pre/curved_grin_final_smoke.png` |
| `00:40.5` | `00:44.0` | Release Gate | section card | `Section card.` |
| `00:44.0` | `00:50.0` | Manifest | manifest card | `output/v0.0-pre/video/TAKE001_MANIFEST.md` |

## Screenshot Artifacts

- `output/v0.0-pre/straight_control_verify.png` - Straight Control Artifact
- `output/v0.0-pre/curved_grin_verify.png` - Curved GRIN Artifact
- `output/v0.0-pre/curved_grin_final_smoke.png` - Diagnostics Artifact

## Known Limitations

- This fallback MP4 is assembled from verified still artifacts, not live OBS window capture.
- It communicates the v0.0-pre workflow and evidence packet but does not replace the operator OBS walkthrough.
- Full-pixel release automation remains a pre-tag gate on faster GPU hardware.
- Captions intentionally avoid unsupported physics claims.
