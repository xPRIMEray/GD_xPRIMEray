# Dual Reality View World — Overlay Reference

## Active Overlays

### `curved_ray` — Curved Ray (default)
The primary transport mode. All rays follow null geodesics of the GRIN field. This is the "honest" render — what the physics says the observer should see. Start here with no additional overlays to let the visitor form their own impression before comparison.

**What to look for:** The annular compression around the portal ring, the asymmetric distortion of the background behind the throat, and the dark region corresponding to rays that were bent so far they escaped without hitting geometry.

### `dual_reality` — Dual Reality View
Activates the straight-ray Reference Reality inset — a frozen render of the same scene under conventional straight transport. In the wormhole fixture this appears as a Picture-in-Picture or a half-screen split.

**What to look for:** Background geometry that appears in a different position in the two views. Features that are visible in straight transport but missing in curved (because curved rays escaped instead of hitting). The inset boundary is the sharpest possible before/after comparison.

### `heatmap_normals` — Curvature Heat Map
Maps cumulative absolute ray-turn-angle per pixel using a heat scale (cool = low bending, hot = high). The curvature is integrated along the full ray path from camera to endpoint.

**What to look for:** The bright annular ring at the portal boundary — this is consistently the highest-curvature region, *not* the throat. The throat interior is surprisingly low-curvature because rays that pass through it have already bent at the shell. This surprises most visitors and is worth calling out explicitly.

### `atlas_labels` — Atlas Labels / Portal Glyphs
Renders semantic geometry annotations: BLV (Boundary Layer Volume) portal glyph markers, field source indicators, and region ID labels for the wormhole's structural zones (mouth, throat, exit).

**What to look for:** Whether the glyph positions align with the high-curvature ring from `heatmap_normals`. They should — but the overlap is not perfect, which reveals that optical curvature and geometric extent are not the same thing.

### `validation_hud` — Validation HUD
Live overlay of render health statistics: classification counts (geomHit, escapedNoHit, budgetExhausted), per-frame closure percentage, budget pressure indicator, and step count histogram.

**What to look for:** Budget exhausted pixel count. In the wormhole scene at default settings, budget exhaustion is low (the scene is well-parameterized). If it rises above 5%, the transport settings need tuning.

## Advanced / Optional Overlays

### `ray_traces` — Ray Path Traces
Renders explicit ray trajectory lines for a sampled subset of rays. Lines are colored by classification outcome (green=hit, blue=escape, red=budget exhausted) and drawn in 3D scene space over the film render.

**Caution:** Dense at full resolution. Recommended for sparse sample (stride 8–16) or for a specific sub-region of interest (e.g., the portal boundary ring only).

### `cathedral_probe` — Cathedral Probe Composite
Six-layer diagnostic overlay assembled from coherence vectors, normal discontinuity map, curvature accumulation, domain ownership boundaries, transport seam detection, and step budget allocation. Intended for developers and researchers.

**When to enable:** After the visitor has seen the basic dual-reality story and wants to understand the full diagnostic picture. This is the "everything" mode — information-dense but explains itself if the visitor follows the cathedral_probe architecture document.

## Overlay Sequence for First-Time Visitors

```
Start: curved_ray only
  → toggle dual_reality: "see what straight transport would show"
  → toggle heatmap_normals: "see where the bending is concentrated"
  → toggle atlas_labels: "see what geometry the bending corresponds to"
  → toggle validation_hud: "confirm the render is correct"
  → (optional) toggle ray_traces: "see individual ray paths"
  → (optional) toggle cathedral_probe: "see the full diagnostic picture"
```

## Overlay Conflicts / Combinations to Avoid

- `ray_traces` + `cathedral_probe` simultaneously: too visually noisy for a new visitor.
- `heatmap_normals` + `dual_reality` at full opacity: the inset and heatmap compete for the same visual real estate. Reduce inset opacity to 50% or use a split-screen mode.
