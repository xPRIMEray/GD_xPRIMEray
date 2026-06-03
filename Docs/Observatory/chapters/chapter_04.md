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
  ![Transport Coherence — radial risk profile](../../assets/observatory/transport-coherence-radial.png)
  <figcaption>Radial risk profile by node. Risk magnitude decays with distance from the instability band centers but never reaches zero. A step discontinuity in the decay curve is consistent with a topological feature in the transport field. Source: <code>output/transport_coherence_basin_smoke/20260503T001944Z/</code></figcaption>
</figure>

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

## Related Research

- [Transport Coherence Basin output README](https://github.com/AetherTopologist/GD_xPRIMEray/tree/main/output/transport_coherence_basin_smoke/README.md)
- [Reference Precision Null Geodesic Probe](../../Research/reference_precision_null_geodesic_probe.md)
- [Transport Island Microscopy](../../Research/transport_island_microscopy.md)
- [Observatory Atlas](../observatory_atlas.md)
