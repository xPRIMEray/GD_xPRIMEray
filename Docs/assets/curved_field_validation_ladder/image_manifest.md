# Curved-Field Validation Ladder Image Manifest

All images in this directory are curated copies of specific `output/` artifacts.
They are repo-tracked so documentation can embed stable relative paths.
Original outputs are never deleted — this directory is a curation layer only.

Run timestamp: `20260509T033059Z`  
Source root: `output/curved_field_validation_ladder/20260509T033059Z/`

---

## Group 1 — Curved Oracle: Cross-Section Quad Panel
*Source: `curved/oracle/cross_section_quad_panel/` · oracle step 0.0015625 · curved_minimal_backdrop scene*

| Docs asset | Source output path | Size |
|---|---|---|
| `curved_field_validation_quad_panel.png` | `curved/oracle/cross_section_quad_panel/diagnostic_quad_panel.png` | 37 KB |
| `curved_field_validation_frame_with_minimap.png` | `curved/oracle/cross_section_quad_panel/diagnostic_frame_with_minimap.png` | 8.3 KB |
| `curved_field_validation_cross_section_minimap.png` | `curved/oracle/cross_section_quad_panel/camera_cross_section_minimap.png` | 5.7 KB |

### Captions

**`curved_field_validation_quad_panel.png`** — *Primary hero image.*
Four-panel diagnostic layout for the curved transport oracle run. Panels: (1) rendered frame with curved transport active, (2) hit-normal vector overlay showing surface normal directions at each hit pixel, (3) camera cross-section minimap showing the transport geometry in the vertical camera plane with field structure and hit-normal sticks, (4) transport/field overlay (ownership seams and oracle comparison context). This is the clearest single public artifact from the curved-field validation ladder: it makes curved transport behavior visible rather than leaving it inside logs and CSV files. Oracle replay failures: 0. All 64 sampled pixels sealed at step 0.02.

**`curved_field_validation_frame_with_minimap.png`**
Rendered frame with the cross-section minimap inset in the top-right corner. Compact two-panel view useful for side-by-side comparison contexts. Minimap size: 140×140 pixels; shows camera frustum, field bounds, and projected hit positions in the vertical camera plane.

**`curved_field_validation_cross_section_minimap.png`**
Camera cross-section minimap in isolation. Vertical cross-section through the camera plane showing: scene geometry bounds, camera frustum, GRIN field influence region, and hit-normal sticks projected into the camera's xz plane. 20 vertical hit sticks drawn. Axis range: z = –20.9 to +5.2, axis = –5.2 to +6.8. This is the first public-facing cross-section minimap from a curved-transport run.

---

## Group 2 — Curved Oracle: Diagnostic Overlays
*Source: `curved/oracle/` · oracle run · curved_minimal_backdrop scene*

| Docs asset | Source output path | Size |
|---|---|---|
| `curved_field_validation_overlay.png` | `curved/oracle/combined_diagnostic_overlay.png` | 14 KB |
| `curved_field_validation_hit_normals.png` | `curved/oracle/hit_normal_vector_overlay.png` | 12 KB |
| `curved_field_validation_convergence_ladder.png` | `curved/oracle/convergence_ladder_contact_sheet.png` | 32 KB |

### Captions

**`curved_field_validation_overlay.png`**
Six-layer Cathedral Probe composite overlay for the curved oracle run. Same layer stack as Cathedral Probe (beauty · wireframe · transport ownership · risk probe markers · transport diagram · continuity vectors), applied to the curved transport scene. Serves as evidence that the full Cathedral Probe diagnostic stack operates correctly on curved-transport captures.

**`curved_field_validation_hit_normals.png`**
Full-frame hit-normal vector overlay for the curved oracle run. Cyan dots: sampled hit pixels. Green arrows: screen-projected surface normal vectors (stride 8, scale 12). 70 vectors drawn from 920 sampled pixels. Normal x-range: –0.997 to +0.997; normal z mean: 0.025 (near-forward surfaces dominant). Post-process inspection only — does not modify renderer behavior.

**`curved_field_validation_convergence_ladder.png`**
Oracle convergence ladder contact sheet for the curved oracle cell. Shows per-pixel `EpsilonStabilityClass` across the step range 0.02 → 0.003125. All 64 sampled pixels achieve `Stable` at step 0.02. Mean decision-risk delta (0.00625 vs 0.003125): 0.000090. Max: 0.000090. Oracle replay failures: 0.

---

## Group 3 — Curved vs Control Storyboard
*Source: root of run · cross-scene comparison*

| Docs asset | Source output path | Size |
|---|---|---|
| `curved_vs_control_storyboard.png` | `curved_vs_control_storyboard.png` | 66 KB |

### Caption

**`curved_vs_control_storyboard.png`**
Top-level cross-scene comparison storyboard. Side-by-side panels: control scene render, curved-transport render, curved hit normals, ownership seams, unresolved island overlay, and graph lineage. Comparability status: `warning` — control uses `domain_resolver_stress` scene while curved uses `curved_minimal_backdrop`; scenes differ by camera pose key (matched-pose control was not available at run time). Resolution, frames, stride, traversal, scheduler mode, and resolver flags are matched. The storyboard is evidence that the ladder infrastructure is wiring correctly even under the scene-control constraint.

---

## Embedding Guide

From `Docs/index.md` or `Docs/Research/` pages at the same level as `assets/`:
```markdown
![Description](assets/curved_field_validation_ladder/filename.png)
```

From `Docs/Research/` pages (one level deeper):
```markdown
![Description](../assets/curved_field_validation_ladder/filename.png)
```
