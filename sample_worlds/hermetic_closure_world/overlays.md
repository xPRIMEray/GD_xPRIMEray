# Hermetic Closure World — Overlay Reference

## Primary Overlays

### `hermetic_closure` — Hermetic Closure Overlay
Per-pixel classification coloring over the rendered film:

- **Green** — `geom_hit`: ray found and classified geometry before exhausting budget.
- **Blue** — `escaped_no_hit`: ray exited the scene bounds without hitting geometry. This is a valid classification — not a failure.
- **Red** — `budget_exhausted`: ray ran out of steps before reaching a definitive classification. This is a failure. At budget=32, 100% of pixels are red.
- **Yellow** — portal event: ray entered a portal or wormhole transition. Not expected in the hermetic curved room scene.

The ratio of red to (green + blue) is the inverse of the closure percentage. At 100% closure, no red pixels appear.

### `validation_hud` — Validation HUD (required in this world)
The HUD is the primary correctness signal for this world. It should display:

- **Closure %** — fraction of rays classified before budget exhaustion. At budget=700: 100.0%. At budget=32: 0.0%.
- **Budget pressure** — how close the average ray came to budget exhaustion. Low pressure = comfortable budget. High pressure = near the cliff edge.
- **Classification breakdown** — exact counts for geom\_hit, escaped\_no\_hit, budget\_exhausted.
- **Step length** — current integration step size. Default for this world: 0.015.
- **Active budget** — current maximum steps per ray.

Without the validation HUD, the visitor cannot distinguish a correct render (100% closure) from a silent failure (0% closure) by visual inspection alone. The HUD is not optional in this world.

## Supporting Overlays

### `heatmap_normals` — Curvature Heat Map
Shows where rays bend most. In the hermetic curved room, high-curvature zones correspond exactly to the regions where budget=32 fails — because those are the regions where rays need more steps to find their endpoint.

**Key observation:** failure is not uniform. It is concentrated where curvature is high. This makes `heatmap_normals` the best visual explanation for *why* the failure pattern in `hermetic_closure` looks the way it does.

**Useful combination:** `hermetic_closure` + `heatmap_normals` simultaneously. Red pixels (failures) should overlay the hot zones in the curvature map.

### `ray_traces` — Ray Path Traces (sparse)
For failing pixels (budget\_exhausted, shown red), render the actual integration path as a line in 3D scene space. Failing rays will be visibly spiraling or wandering near high-curvature boundaries, showing *why* they exhaust their budget: they can't find a clean exit from the field transition zone.

**Recommended:** sparse mode only (stride 16+). Dense ray traces in this scene will obscure the closure overlay.

### `cathedral_probe` — Cathedral Probe Composite
Full six-layer diagnostic. Most useful for developers investigating a specific failure cluster. The coherence vector layer will show high-density vectors at the failure boundary, confirming the topological nature of the instability.

**Not recommended for first-time visitors.** Enable after the visitor has understood the basic budget cliff story.

## Overlay Sequence for First-Time Visitors

```
Start: budget=700, curved_ray only
  → toggle hermetic_closure
  → "All green/blue — every pixel resolved. This is a correct render."
  → toggle validation_hud
  → "Confirm: 100% closure."
  → reduce budget to 32 (via toggle or slider)
  → "Watch: image still looks similar. But HUD shows 0% closure."
  → "Every red pixel is a budget-exhausted failure — unresolved noise."
  → toggle heatmap_normals
  → "Red pixels cluster where curvature is high. This is why."
  → restore budget=700
  → toggle ray_traces (sparse)
  → "See how failing rays spiral near boundaries vs. resolving rays that terminate cleanly."
```

## The Silent Failure Warning

> This overlay makes something important visible: a renderer can *look* correct and be completely wrong. At budget=32, the hermetic curved room produces an image that passes casual visual inspection but contains zero correctly classified pixels.
>
> The Validation HUD is the only way to know. This is why transport validation is not optional in xPRIMEray.
