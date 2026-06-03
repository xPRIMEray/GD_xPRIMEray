# Sample Worlds

Sample Worlds are the interactive layer of xPRIMEray's public presence — the bridge between static promoted artifacts (in `misterylabs_artifacts/`) and navigable community demonstrations.

Each world is a self-contained scene configuration that a visitor can run, observe, and toggle. Worlds are not tutorials and not stress tests. They are *places* — designed so that a new visitor with no prior knowledge of xPRIMEray can arrive, look around, read the HUD, and understand one specific optical transport phenomenon.

## Design Principles

1. **One question per world.** Each world answers a single, clearly stated question. Complexity is managed through toggles, not by cramming multiple phenomena into one scene.

2. **Observable, not explained.** The physics should be directly visible, not described in a paragraph. The visitor should be able to turn an overlay on and off and see the effect.

3. **Grounded in promoted artifacts.** Every world is backed by a real experiment in `output/`. The static artifact is the evidence; the world is the demonstration.

4. **Honest about limits.** Each world specifies a Validation Question — something the visitor can verify themselves — and acknowledges what is not yet implemented or what requires further work.

## Worlds

| World | Status | Scene | Tier | Question |
|-------|--------|-------|------|----------|
| [dual_reality_view_world](dual_reality_view_world/) | Design proposal | `test-overspace-wormhole-witness-fixture.tscn` | 1 | What does a wormhole look like, and how do we know the bending is real? |
| [observer_disagreement_world](observer_disagreement_world/) | Design proposal | `test-grin-basic-visual-minimal-offaxis-observe.tscn` | 1 | How much does curved transport change what you see vs. straight? |
| [hermetic_closure_world](hermetic_closure_world/) | Design proposal | `test-hermetic-curved-room.tscn` | 1 | When does the integrator fail silently, and what does failure look like? |
| [cathedral_probe_world](cathedral_probe_world/) | Design proposal | `test-domain-resolver-stress.tscn` | 2 | How do you diagnose a renderer that doesn't give you a crash, just wrong pixels? |
| [transport_coherence_basin_world](transport_coherence_basin_world/) | Design proposal | `test-domain-resolver-stress.tscn` | 2 | Where in the scene does transport refuse to converge, and why there? |

## Status Lifecycle

```
Design proposal → Scaffold → Prototype → Review → Published
```

All worlds in this folder are at **Design proposal** stage. No runtime code has been written. Each folder contains:

- `world.md` — purpose, narrative, required overlays, validation question
- `config.schema.json` — placeholder schema for future runtime configuration
- `overlays.md` — which overlay modes apply and what each one reveals

## Relationship to misterylabs_artifacts/

Static artifacts provide the *evidence* that each world demonstrates. The artifact was measured first; the world makes it interactive. If the artifact does not exist or is not promoted, the world should not be built yet.

| World | Backing Artifact |
|-------|-----------------|
| dual_reality_view_world | `wormhole-dual-reality-story`, `wormhole-structure-observatory` |
| observer_disagreement_world | `observer-disagreement` |
| hermetic_closure_world | `hermetic-hit-closure` |
| cathedral_probe_world | `doe-scheduler-resonance`, `transport-coherence-basin` |
| transport_coherence_basin_world | `transport-coherence-basin` |

## Overlay Reference

All worlds draw from a shared overlay vocabulary. See individual `overlays.md` for which modes apply to each world.

| Overlay | Code Name | What It Shows |
|---------|-----------|---------------|
| Straight Ray | `straight_ray` | Reference transport — no curvature, conventional renderer |
| Curved Ray | `curved_ray` | GRIN null-geodesic integration — the physically motivated path |
| Dual Reality View | `dual_reality` | Split-screen or inset: curved main + straight reference side-by-side |
| Observer Disagreement | `observer_disagreement` | Per-pixel delta: where curved and straight transport classify differently |
| Hermetic Closure | `hermetic_closure` | Classification overlay: hit / escape / budget-exhausted per pixel |
| Transport Coherence | `transport_coherence` | Instability heatmap: regions where the oracle fails to converge |
| Heatmap Normals | `heatmap_normals` | Normal vector field — sharp edges reveal domain ownership boundaries |
| Ray Path Traces | `ray_traces` | Explicit ray trajectory lines — visualizes bending geometry directly |
| Cathedral Probe | `cathedral_probe` | Six-layer composite: coherence, normals, curvature, ownership, seams, budget |
| Atlas Labels | `atlas_labels` | Semantic geometry annotations — portal glyphs, field markers, region IDs |
| Validation HUD | `validation_hud` | Live stats: closure %, budget pressure, render health, frame telemetry |
