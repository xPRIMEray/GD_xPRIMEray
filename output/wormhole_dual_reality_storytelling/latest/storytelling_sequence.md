# Wormhole DualRealityTransport Storytelling Sequence

This ladder is a small, repeatable storytelling sequence for explaining the wormhole-side `DualRealityTransport` stack from perception to interpretation.

## Sequence

### 01_clean_curved — Clean curved render

- What it shows: Observer-facing curved film render with no storytelling overlays.
- What it communicates: Baseline perception. This is the curved wormhole view before interpretive scaffolding is added.
- Image: `images/01_clean_curved.png`

### 02_reference_reality — Reference Reality / straight transport view

- What it shows: Curved main render with the straight-path Reference Reality inset frozen for side-by-side comparison.
- What it communicates: Perceptual contrast. It shows what the scene would look like under straight transport and exposes the wormhole distortion as a difference against the main view.
- Image: `images/02_reference_reality.png`

### 03_curvature_map — Curvature heat map only

- What it shows: Fullscreen pass-1 curvature heat map using cumulative absolute turn angle, without semantic or collision overlays.
- What it communicates: Primary transport interpretation. It highlights where rays accumulate bending, without additional annotation competing for attention.
- Image: `images/03_curvature_map.png`

### 04_curvature_plus_semantic — Curvature plus semantic glyphs

- What it shows: Curvature heat map combined with semantic field / BLV / portal glyph overlays.
- What it communicates: Structural interpretation. It helps relate annular high-curvature regions to portal-local geometry and semantic landmarks.
- Image: `images/04_curvature_plus_semantic.png`

### 05_curvature_plus_collision — Curvature plus collision radar

- What it shows: Curvature heat map combined with collision radar labels for visible collision objects.
- What it communicates: Geometry-query interpretation. It shows where strong bending and visible collision structure do or do not line up.
- Image: `images/05_curvature_plus_collision.png`

### 06_full_stack — Full stack

- What it shows: Reference Reality inset plus curvature, semantic glyphs, and collision radar overlays in one composed storytelling frame.
- What it communicates: Integrated interpretation. This is the most information-dense frame and closes the ladder by stacking perception, transport, and structure together.
- Image: `images/06_full_stack.png`

## Artifacts

- Contact sheet: `wormhole_dual_reality_storytelling_contact_sheet.png`
- Contact sheet summary: `contact_sheet_summary.json`

## Ordering Intent

- `01` starts with perception alone.
- `02` introduces the straight-transport comparison layer.
- `03` isolates the curvature metric as the main interpretive diagnostic.
- `04` and `05` connect curvature to semantic and collision structure separately.
- `06` closes with the full interpretive stack.
