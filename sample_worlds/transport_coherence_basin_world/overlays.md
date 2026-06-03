# Transport Coherence Basin World — Overlay Reference

## Primary Overlays

### `transport_coherence` — Transport Coherence Instability Map
The primary diagnostic for this world. Maps per-pixel oracle convergence status as a spatial heatmap:

- **Bright / hot** — UNSEALED_NONCONVERGENT: oracle ran out of precision budget without convergence. These are the 289 instability regions.
- **Medium** — converged but at fine precision (0.003125 or 0.00625): oracle reached a solution but only at the finest available precision floor.
- **Cool / dark** — converged at coarse precision: stable transport, oracle converges quickly.
- **Black** — not probed: outside the oracle's sampling domain for this run.

**What to look for:** Two symmetric horizontal bands — the GRIN field boundary annulus at pixel rows ~58 and ~122. The spatial symmetry is itself a finding: the instability is geometrically determined by the field's radial structure, not by transport order or numerical noise.

### `validation_hud` — Validation HUD
For this world, the HUD should display:
- Risk region count: expected 289 (UNSEALED_NONCONVERGENT)
- Required precision floor: expected 0.003125 (all 289 regions at the same floor)
- Sealed regions: expected 0
- Epsilon (convergence threshold): 0.05 (the value used in the promoted artifact)
- Step length and traversal mode

Any deviation from the expected risk region count is a regression signal.

## Supporting Overlays

### `heatmap_normals` — Curvature Heat Map
The curvature accumulation map should spatially co-locate with the instability regions from `transport_coherence`. Where curvature is high, precision requirements are high, and the oracle is most likely to fail.

**Critical check:** The curvature hot zones and the instability regions should be in the same spatial locations. If they diverge significantly, the instability may have a cause other than field strength — possible ownership topology issue.

### `atlas_labels` — Field Source and Region Labels
Labels the GRIN field source, the outer shell boundary (the geometric feature associated with the instability annulus), and individual risk region IDs from the transport\_risk\_nodes.csv dataset.

**Useful for:** Linking specific instability regions (by ID) to the upstream dataset for research reference.

### `cathedral_probe` — Cathedral Probe Composite
The full six-layer Cathedral Probe diagnostic. In this world, the most relevant layers are:
- **Layer 1 (coherence vectors):** should show dense clusters at the instability annulus
- **Layer 4 (domain ownership):** the instability regions should correspond to thin ownership transition strips
- **Layer 5 (transport seams):** seam pixels should be concentrated in the instability bands

## What the Overlays Cannot Show

- **Why** the oracle cannot converge (only that it cannot). The convergence decay curve and the IOR gradient experiment are needed to distinguish topological from parameterization causes.
- **Individual ray paths** for specific risk nodes (ray traces would need to be pin-pointed to specific risk node coordinates, not the full film).
- **The oracle's precision refinement sequence** — the convergence replay mode does not yet exist.
