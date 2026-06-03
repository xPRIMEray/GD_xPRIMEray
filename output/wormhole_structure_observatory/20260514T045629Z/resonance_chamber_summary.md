# Resonance Chamber Overlay Summary

This is a first-pass post-process overlay for xPRIMEray wormhole fixture output. It visualizes chamber-like transport behavior inspired by Schrodinger wave-packet tunneling and double-barrier / double-slit pop-culture physics language, while staying grounded in renderer telemetry.

## Fixture

- Input directory: `/home/bb/code/godot_xPRIMEray/output/wormhole_structure_observatory/20260514T045629Z`
- Base image: `/home/bb/code/godot_xPRIMEray/output/wormhole_structure_observatory/20260514T045629Z/panels/clean_curved/clean_curved_film.png`

## Generated Artifacts

- Overlay: `/home/bb/code/godot_xPRIMEray/output/wormhole_structure_observatory/20260514T045629Z/resonance_chamber_overlay.png`
- Annotated overlay: `/home/bb/code/godot_xPRIMEray/output/wormhole_structure_observatory/20260514T045629Z/resonance_chamber_overlay_annotated.png`
- Metrics CSV: `/home/bb/code/godot_xPRIMEray/output/wormhole_structure_observatory/20260514T045629Z/resonance_chamber_metrics.csv`

## Region Interpretation

- RESONANCE CHAMBER (`chamber_wall_interaction`): coverage=33.57% mean_signal=51.38; source `/home/bb/code/godot_xPRIMEray/output/wormhole_structure_observatory/20260514T045629Z/panels/domain_diagnostics/domain_diagnostics_film.boundary_confidence.png`
- RESONANCE CHAMBER (`resonant_core`): coverage=21.03% mean_signal=47.72; source `/home/bb/code/godot_xPRIMEray/output/wormhole_structure_observatory/20260514T045629Z/panels/clean_curved/figures/figure_C_ring_density.png`
- PHASE BUILDUP (`phase_accumulation`): coverage=28.47% mean_signal=38.57; source `/home/bb/code/godot_xPRIMEray/output/wormhole_structure_observatory/20260514T045629Z/panels/clean_curved/wormhole_portal_sector_report.json; /home/bb/code/godot_xPRIMEray/output/wormhole_structure_observatory/20260514T045629Z/panels/step_budget_heatmap/step_budget_heatmap_film.png`
- OWNERSHIP TRANSITION (`ownership_transition`): coverage=31.49% mean_signal=39.64; source `/home/bb/code/godot_xPRIMEray/output/wormhole_structure_observatory/20260514T045629Z/panels/domain_diagnostics/domain_diagnostics_film.domain_id.png; /home/bb/code/godot_xPRIMEray/output/wormhole_structure_observatory/20260514T045629Z/panels/domain_diagnostics/domain_diagnostics_film.selection_flip.png`
- INBOUND PACKET (`inbound_packet`): coverage=21.66% mean_signal=21.24; source `fixture-space left-to-throat transport prior`
- TUNNEL EXIT (`tunnel_exit`): coverage=24.16% mean_signal=22.82; source `fixture-space throat-to-right plume prior`

## Missing Or Placeholder Inputs

- None detected for this pass.

## Notes

- Boundary confidence is treated as chamber wall interaction.
- Throat events or portal ring-density imagery are treated as the resonant chamber core.
- Sector report query density and step budget imagery are treated as phase accumulation.
- Domain-id edges and selection flips are treated as ownership transitions.
- Inbound and exit plume regions are deliberately soft spatial guides until direct transport direction maps are available.
