# Observer Disagreement World — Overlay Reference

## Primary Overlays

### `curved_ray` — Curved Ray (GRIN transport)
Full GRIN null-geodesic integration. This is the physically motivated render. Expected classification at the canonical off-axis pose: 46,841 geom hits, 60,295 escaped, 22,464 budget exhausted.

**Key visual:** fewer geometry hits than straight transport. Rays are being deflected away from surfaces by the GRIN field.

### `straight_ray` — Straight Ray (reference transport)
Conventional straight-line ray transport. No GRIN field curvature applied. Expected classification: 70,300 geom hits, 20,964 escaped, 38,336 budget exhausted.

**Key visual:** more geometry hits, fewer escapes. Without bending, rays find the surfaces they aimed at.

### `observer_disagreement` — Observer Disagreement Delta
Per-pixel classification delta between the curved and straight renders. Color encoding:

- **Bright blue** — pixel classified as `geom_hit` in straight transport but `escaped_no_hit` in curved. The GRIN field bent the ray away from a surface it would otherwise have hit. These are the 27,619 "lost geometry contact" pixels.
- **Faint cyan** — pixel classified as `escaped_no_hit` in straight but `geom_hit` in curved. GRIN bent the ray *toward* a surface it would have missed. These are the 3,220 "gained geometry contact" pixels — far fewer than the losses.
- **Neutral gray** — pixel is unresolved (budget exhausted in at least one transport) — cannot be compared.
- **Transparent** — pixel classified identically by both transports.

The strong asymmetry between blue and cyan (≈9:1 ratio) is the key finding: GRIN deflection predominantly redirects rays *away from* geometry, not toward it. The field is a lens that increases escape rate.

## Supporting Overlays

### `heatmap_normals` — Curvature Heat Map
Shows where the GRIN field is strongest. The disagreement clusters should align with the high-curvature zones in this overlay — they are the regions where ray deflection is large enough to change classification outcome.

**Useful combination:** `heatmap_normals` + `observer_disagreement` simultaneously. The curvature hot zones should spatially correspond to the disagreement blue pixels.

### `atlas_labels` — Field Source Labels
Labels the GRIN field source location and the geometric boundaries of the scene. Helps the visitor understand why disagreement is concentrated in specific image regions.

### `validation_hud` — Validation HUD
In disagreement mode, the HUD should show:
- Curved transport classification counts
- Straight transport classification counts
- Disagreement pixel count and percentage
- Top transition types (geom→escaped, escaped→geom)

This makes the static artifact numbers live.

## Overlay Sequence for First-Time Visitors

```
Start: curved_ray
  → "What do I see?"
  → toggle straight_ray
  → "What changed?"
  → toggle observer_disagreement
  → "Which pixels changed, and in which direction?"
  → toggle validation_hud
  → "How many? Does this match what the experiment measured?"
  → (optional) toggle heatmap_normals
  → "Where in the scene is the bending strong enough to cause disagreement?"
```

## What Each Mode Does NOT Show

- `observer_disagreement` does not show magnitude of change within a class — only the binary change of classification. A pixel that went from geom\_hit with depth=1.5m to geom\_hit with depth=2.0m is NOT marked as disagreeing, even though the transport changed.
- `straight_ray` does not run the full GRIN integrator — it uses straight paths. Results are deterministic and fast but physically incorrect in a curved-field scene.
- `heatmap_normals` shows curvature along ray paths, not geometric surface normals. The two are related but not identical.
