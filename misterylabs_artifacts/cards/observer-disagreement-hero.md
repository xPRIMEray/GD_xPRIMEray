# Observer Disagreement Hero — 23.8% of the Frame Classifies Differently

**Hook:** Same scene. Same camera. Two transport models. 30,839 pixels disagree. Look at the color — almost all of it is blue, not cyan. The GRIN field is a defocusing lens.

## What You See

Three panels, same observer pose:

| Panel | Transport | Geometry Hits | Escaped |
|-------|-----------|--------------|---------|
| Left  | Curved GRIN | 46,841 | 60,295 |
| Center | Straight reference | 70,300 | 20,964 |
| Right  | Disagreement delta | — | 30,839 pixels differ |

**Blue pixels (right panel):** geometry hits under straight transport that became escapes under curved — the GRIN field bent rays *away* from surfaces they would have intersected. Count: 27,619.

**Cyan pixels (right panel):** escapes under straight that became hits under curved — the GRIN field bent rays *toward* surfaces. Count: 3,220.

**Ratio: 8.6:1** (blue to cyan). The field is defocusing at this configuration. Curved rays escape geometry that straight rays hit. This is a real physical effect, not a visual style choice.

## Why It Matters

A renderer that uses straight-line ray paths for this scene would report 70,300 geometry hits. xPRIMEray's curved transport reports 46,841 — a 33% shortfall. The difference is not aliasing, not noise, not a rendering artifact. It is the physics of geodesic curvature deflecting rays past surfaces they would otherwise have hit.

This measurement makes the transport model choice consequential. If you need an accurate surface-contact count — for radiosity, for collision, for spectral analysis — the 33% discrepancy matters. "Approximately right" is not right when the approximation systematically removes a third of the geometry contacts.

## What Would Make It Stronger

A second camera pose at a different off-axis angle. If the disagreement fraction changes with pose, the asymmetry is pose-dependent, not an intrinsic property of the GRIN field. The angular dependence sweep is the next experiment.

## Technical Context

- Scene: `test-grin-basic-visual-offaxis-observe.tscn` (curved) + `test-grin-basic-visual-straight-offaxis-observe.tscn` (straight)
- Camera pose: `basic_visual_offaxis_observe_v0_pre`
- Film: 480×270 source renders composited into 1464×330 hero panel
- Classification delta computed pixel-by-pixel across both transports

---

*Source:* `output/observer_disagreement/offaxis_observe_delta/`  
*Hero panel:* `visuals/observer-disagreement-hero.png` (1464×330)  
*Web crop:* `visuals/observer-disagreement-hero-web.png` (1200×270)  
*Presentation:* `visuals/observer-disagreement-hero-pres.png` (1920×540)  
*Also promoted:* `visuals/observer-disagreement-contact-sheet.png` (1600×1300) — original full-detail exhibit  
*Tier:* 1 — Minimum Viable Observatory  
