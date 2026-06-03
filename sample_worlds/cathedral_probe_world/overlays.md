# Cathedral Probe World — Overlay Reference

This world's primary content *is* the overlays. Each of the six Cathedral Probe layers is a distinct diagnostic mode; they can be used individually or composed into the full six-layer stack.

## Cathedral Probe Layers (Six)

### Layer 1: Coherence Vectors
Per-pixel transport continuity vectors. Each pixel receives a vector magnitude and direction representing how much the transport solution changes from its neighbors. High-magnitude vectors (bright pixels) are at transport ownership boundaries.

**Code name:** `cathedral_coherence_vectors`  
**What to look for:** Dense clusters of bright vectors. These are the instability zones — where a small change in ray direction produces a large change in endpoint. The 289 UNSEALED regions in the coherence basin experiment live here.

### Layer 2: Normal Discontinuity Map
Pixel-level change in surface normal across neighboring samples. Where the rendered normal jumps sharply, a domain ownership transition is occurring — the same geometry is being "owned" by different transport domains in adjacent pixels.

**Code name:** `cathedral_normal_discontinuity`  
**What to look for:** Sharp linear features. These are the transport seam edges. In the domain-resolver-stress scene they appear as arcs corresponding to geometric boundaries.

### Layer 3: Curvature Accumulation
Cumulative absolute ray-turn-angle per pixel, normalized to [0,1]. This is the same metric used in `heatmap_normals` in other worlds, but here it is one layer in the composite rather than the primary visualization.

**Code name:** `cathedral_curvature`  
**Relationship to other overlays:** The curvature accumulation map should spatially correlate with the coherence vector map (Layer 1) — high curvature predicts high instability. Where they diverge, the instability has a cause other than field strength (topology, ownership ambiguity, numerical precision floor).

### Layer 4: Domain Ownership
Per-pixel color coding of which transport domain the resolver assigned ownership of each ray's hit. Domain boundaries between adjacent ownership regions are the transport fault lines where first-hit instability lives.

**Code name:** `cathedral_domain_ownership`  
**What to look for:** Thin transition strips between ownership regions. These strips are where the `transport_seam` (Layer 5) overlay will fire. Pixels in the strip could plausibly be owned by either adjacent domain — this is the instability source.

### Layer 5: Transport Seam Detection
Highlights pixels in the ownership ambiguity zone — where the first-hit resolver could assign ownership to multiple competing domains. These are the seam pixels that contribute disproportionately to the banding artifact when amplified by row traversal.

**Code name:** `cathedral_transport_seams`  
**Relationship to DOE findings:** The seam pixels at stride=1 are the same pixels that produce the banding artifact at stride=4. The seam density predicts the band susceptibility.

### Layer 6: Step Budget Allocation
How many integration steps each pixel consumed, normalized to [0, max\_steps]. Expensive pixels (near max\_steps) are in high-curvature, high-ownership-ambiguity zones. The step budget map can reveal scheduler resonance directly: at stride=4, the allocation map shows periodic horizontal stripes rather than smooth curvature-correlated gradients.

**Code name:** `cathedral_step_budget`  
**The resonance signature:** At stride=1, the budget allocation map is smooth. At stride=4, horizontal stripes appear in the budget allocation — this is the scheduler resonance being visible in per-pixel compute cost before it appears as visual banding.

## Composite Mode

### `cathedral_probe` — Full Six-Layer Composite
All six layers composited with configurable per-layer opacity and blend mode. The reference composite (`Docs/assets/cathedral_probe/cathedral_probe_overlay_row_0015.png`) uses:
- Layer 1 (coherence): additive blend, 60% opacity
- Layer 2 (normals): multiply blend, 40% opacity
- Layer 3 (curvature): additive blend, 30% opacity
- Layer 4 (ownership): hue overlay, 50% opacity
- Layer 5 (seams): additive blend, 80% opacity
- Layer 6 (budget): additive blend, 40% opacity

**Target:** The composite should reproduce the published reference overlay for identical scene, step, and stride parameters.

## Supporting Overlays

### `validation_hud`
In this world, the HUD should show:
- Active Cathedral Probe layers (checkboxes)
- Current traversal mode and stride
- Band pixel percentage (compared to expected from the DOE CSV)
- Horizontal band score
- Seam pixel count

### `atlas_labels`
Labels domain ownership boundary regions with IDs corresponding to the transport\_ownership\_graph.json. Links individual seam segments to their domain pair.

## Overlay Sequence for Technical Visitors

```
Start: raw render, no overlays
  → Layer 6 (step budget): "where is integration expensive?"
  → Layer 1 (coherence vectors): "where is transport unstable?"
  → Layer 5 (seams): "where are the ownership ambiguity zones?"
  → Switch stride: 1 → 4 → "watch the budget map develop stripes"
  → composite: all layers → "this is the full Cathedral Probe view"
```
