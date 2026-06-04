---
title: "Ch 4 — Coherence Basin"
description: Where does transport refuse to converge regardless of precision — and what does the spatial pattern reveal?
---

# Chapter 4 — Coherence Basin

**Act III — Understanding** · Requires Ch 2 + Ch 3 context · Instability chapter

---

## Core Question

*Where in the transport field does convergence fail regardless of precision — and does the spatial pattern of those failures reveal their cause?*

---

<figure markdown>
  ![Coherence Basin Hero — 960×540 raw render + risk region overlay](../../assets/observatory/coherence-basin-hero.png)
  <figcaption><strong>First 960×540 coherence basin map.</strong> Left: raw transport render at full resolution. Right: risk region overlay — 276 threshold_snap nodes all at precision=0.003125, clustered in two symmetric horizontal bands at the GRIN field boundary annulus. Source: <code>output/transport_coherence_basin_smoke/20260604T023051Z_960x540/</code></figcaption>
</figure>

<figure markdown>
  ![Transport Coherence — radial risk profile](../../assets/observatory/transport-coherence-radial.png)
  <figcaption>Radial risk profile by node. Risk magnitude decays with distance from the instability band centers but never reaches zero. The step discontinuity in the decay curve is the quantitative signature of a topological feature at the GRIN field boundary. Source: <code>output/transport_coherence_basin_smoke/20260503T001944Z/</code></figcaption>
</figure>

---

!!! tip "What to look at"
    **Inspect:** The radial risk profile chart. Notice the step discontinuity in the decay curve — the risk does not taper smoothly to zero. That non-smooth decay is the quantitative signature of a topological feature at the GRIN field boundary.

    **Contradiction:** If instability were purely numerical, you would expect a distribution of required precision floors across the 289 regions. Instead, every single region shares the same floor (0.003125). Uniformity at the finest level implies the instability is structural, not stochastic.

    **What would make it stronger:** A beauty render at ≥960×540 resolution where the 289 individual instability regions appear as distinguishable spatial features rather than merged symmetric bands. At the current 480×270 resolution the regions overlap. See [capture recipe](#capture-recipe-coherence-basin-beauty-render) below.

---

## What the Visitor Sees

**The coherence basin probe** runs the transport oracle at progressively finer precision until it either converges or exhausts the finest available level (0.003125). Regions that never converge are classified as `UNSEALED_NONCONVERGENT`.

**Result:** 289 `UNSEALED_NONCONVERGENT` regions. All 289 require the same precision floor. Zero regions sealed at any coarser precision.

**The spatial pattern:** The 289 regions are not randomly distributed. They cluster symmetrically around two horizontal bands:

- Band 1: centered at pixel row ~58
- Band 2: centered at pixel row ~122
- Both symmetric around the scene's vertical midline (~row 135 for 270-row film)

The symmetric distribution corresponds to the GRIN field's outer boundary annulus — the same location Chapter 1 identified as the curvature hot zone.

**The uniformity finding:** All 289 regions share the same precision floor (0.003125). If the instability were a smooth continuous property of the field, we would expect a distribution of required precisions. The uniform floor is inconsistent with smooth degradation — it suggests a topological discontinuity at the GRIN field boundary.

**The radial risk profile** shows risk magnitude decaying with distance from the band centers but never reaching zero. The decay has a step discontinuity, not a smooth gradient. This is the quantitative signature of a topological feature.

---

## Artifacts

| Artifact | File | Notes |
|----------|------|-------|
| Radial risk profile | `misterylabs_artifacts/visuals/transport-coherence-radial.png` | Primary chart |
| Risk vs. step by anchor | `misterylabs_artifacts/visuals/transport-coherence-risk-vs-step.png` | Step-length independence |
| Risk node dataset | `misterylabs_artifacts/datasets/transport-coherence-risk-nodes.csv` | 289 rows, one per region |
| Validation report | `misterylabs_artifacts/validation/transport-coherence-basin.md` | Risk region classification |
| Card | `misterylabs_artifacts/cards/transport-coherence-basin.md` | Ready for MisterY Labs |

!!! note "Missing visual"
    A beauty render at sufficient resolution to distinguish the 289 individual instability regions as separate spatial features — rather than merged symmetric bands — is the chapter's primary pending artifact. At 480×270 (current resolution), the 289 regions overlap into two continuous stripes.

---

## The Risk Node Dataset

The `transport-coherence-risk-nodes.csv` dataset has 289 rows. Every row:

```
node_id  center_x  center_y  high_radius  stable_radius  precision  max_risk  sealed
1        128       122       43.84        -              0.003125   2.27      no
2        128        58       43.84        -              0.003125   2.27      no
...
289      ...
```

All 289 rows: `sealed = no`, `precision = 0.003125`. The machine-readable uniformity confirms what the profile shows visually.

---

## Sample World

**`transport_coherence_basin_world`** — [design proposal](https://github.com/AetherTopologist/GD_xPRIMEray/tree/main/sample_worlds/transport_coherence_basin_world/world.md)

Scene: `test-domain-resolver-stress.tscn`

The world places the visitor in the domain-resolver-stress scene with the `transport_coherence` overlay active — a heatmap showing convergence status per pixel. The 289 instability regions appear as two bright symmetric bands.

The critical experiment: adjust the IOR gradient at the GRIN outer shell and observe whether the instability regions move, shrink, or disappear. This is the "topological vs. parameterization" test.

Build priority: **5** (most technically specialized of the five worlds).

---

## Validation Question

*At epsilon=0.05 and finest precision floor 0.003125: how many UNSEALED_NONCONVERGENT regions should the oracle find?*

Expected: **289**.

*Structural check:* Are the instability bands symmetric around the scene's vertical midline? Expected: yes (bands at rows ~58 and ~122). Asymmetry would indicate field parameterization change or numerical artifact.

---

## Key Insight

**Some transport failures are topological, not numerical. They cannot be solved by adding budget — the field geometry itself creates a precision floor that the oracle cannot cross.**

---

## Next Chapter

[Chapter 5 — Cathedral Probe →](chapter_05.md): Chapter 4 identified *where* the transport field is unstable using the oracle. Chapter 5 provides the diagnostic toolkit for finding the same instability zones from the rendered output alone — without running the oracle explicitly.

*Bridge:* "We know where the field is unstable. But we can't run the oracle on every frame. How do we find the same zones from the render itself?"

---

## Capture Recipe — Coherence Basin Beauty Render

**Target:** A beauty render of `test-domain-resolver-stress.tscn` at ≥960×540 resolution with the `transport_coherence` overlay active, so the 289 individual instability regions appear as distinguishable spatial features rather than merged bands.

**Steps:**

1. Open `test-domain-resolver-stress.tscn` in the Godot editor.
2. Set film resolution to **960×540** (or 1280×720 for higher fidelity). Parameter: `GrinFilmCamera.FilmWidth = 960`, `FilmHeight = 540`.
3. Enable `transport_coherence` overlay (`DualRealityOverlayMode` → coherence mode, or the specific coherence overlay flag). The 289 instability regions should appear as two symmetric horizontal bands of high-risk pixels.
4. Run a full traversal pass. Screenshot the film output. Save as `coherence-basin-beauty-960.png`.
5. Copy to `Docs/assets/observatory/coherence-basin-beauty-960.png`.
6. Add a second `<figure>` block in this chapter using the new image.

**Expected output:** Two symmetric bright bands at pixel rows ~58 and ~122 (scaled for 540 rows → rows ~115 and ~243). The bands should be visually separable, not merged into a single wide stripe.

**Promotion path:** Copy to `misterylabs_artifacts/visuals/coherence-basin-beauty-960.png` and update `manifest.json` artifact `a05`.

---

## Related Research

- [Transport Coherence Basin output README](https://github.com/AetherTopologist/GD_xPRIMEray/tree/main/output/transport_coherence_basin_smoke/README.md)
- [Reference Precision Null Geodesic Probe](../../Research/reference_precision_null_geodesic_probe.md)
- [Transport Island Microscopy](../../Research/transport_island_microscopy.md)
- [Observatory Atlas](../observatory_atlas.md)
