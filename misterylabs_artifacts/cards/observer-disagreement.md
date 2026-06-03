# Observer Disagreement — How Curved Transport Changes What You See

**Hook:** The same scene, the same camera, the same moment — but a curved-path observer and a straight-path observer classify 23.8% of pixels differently.

## Scientific Context

xPRIMEray can run two transport models simultaneously: a curved GRIN integration (physically motivated) and a straight reference ray (the conventional renderer assumption). This experiment asks: for a given scene and camera position, how many pixels would a curved observer classify differently from a straight one? And in which direction do they disagree?

## Observation

At 480×270 (129,600 total pixels), with an off-axis observer looking at the GRIN field scene:

| Transport | Budget Exhausted | Escaped No Hit | Geometry Hit |
|-----------|-----------------|----------------|--------------|
| Curved GRIN | 22,464 | 60,295 | 46,841 |
| Straight ref | 38,336 | 20,964 | 70,300 |

30,839 pixels (23.8%) changed classification. The dominant transition: **geom\_hit → escaped\_no\_hit** (27,619 pixels). Curved transport bends 27k rays *away* from geometry that straight rays would have hit — the lensing causes rays to escape the scene entirely instead of striking surfaces.

The reverse transition (escaped → geom\_hit) accounts for only 3,220 pixels. The effect is strongly asymmetric: GRIN deflection predominantly *redirects rays away from* surfaces rather than toward new ones.

## Why It Matters

This is the most direct measurable consequence of transport curvature at the pixel level. A straight renderer would report 70,300 geometry hits; xPRIMEray's curved transport reports only 46,841 — a 33% shortfall. The difference is not noise or aliasing: it is the physics of bent light deflecting rays past geometry. The contact sheet makes this observable without equations.

## Next Step

Run the disagreement analysis across multiple observer positions to map the *angular dependence* of the disagreement. Near-axis observers should disagree less; off-axis observers should disagree more. Build a disagreement heatmap over observer pose space.

---

*Source:* `output/observer_disagreement/offaxis_observe_delta/`  
*Key image:* `visuals/observer-disagreement-contact-sheet.png`  
*Tier:* 2 — Research Atlas
