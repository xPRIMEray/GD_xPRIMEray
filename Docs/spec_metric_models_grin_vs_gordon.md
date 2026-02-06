# Specification — Metric Models (GRIN vs Gordon Metric / Gravity Mode)

## Purpose

This document defines the renderer’s **MetricModel** abstraction: how different “field meanings” (optical GRIN vs gravitational Gordon metric style) coexist under the same Field System architecture.

The goal is not to fully encode causality or exact GR theory. The goal is to provide a unified, fast, controllable “gravity mode” that behaves like a curved-path generator.

We define two MetricModels:

* **GRIN (IOR framework)** — optical gradient-index style
* **GordonMetric (Gravity mode)** — GR-inspired curvature control for gravitational objects

Both share the same FieldEntity contract:

* shape
* transform
* rInner/rOuter
* curveType and params
* amp
* flags

They differ only in how they interpret these parameters to produce acceleration/curvature.

---

## Why “MetricModel” Exists

We want:

* One architecture for snapshot, broadphase, scheduling
* Multiple physics interpretations
* Minimal user-facing controls
* A stable ABI for passing parameters in/out

MetricModel ensures we can swap physical meaning without rewriting the engine.

---

## Common Evaluation Pipeline

Field selection (FTLAS) is identical.

Per candidate field entity:

1. pLocal = localFromWorld * pWorld
2. Compute r and u normalized via rInner/rOuter
3. Evaluate scalar curve f(u) from curveType
4. Convert f(u) into a contribution vector `a`
5. Sum contributions deterministically

Only Step 4 differs by MetricModel.

---

## MetricModel Enum

```
enum MetricModel {
    GRIN = 0,
    GordonMetric = 1
}
```

---

## GRIN MetricModel (Optical / IOR-style)

### Intent

Approximate optical bending via a controllable “index gradient” influence.

### Minimal Behavior (v1)

* magnitude = amp * f(u)
* direction = radial (in local space), sign controlled by flags

```
a = radialDir * (amp * f(u))
```

### Notes

This is a control field that drives curved motion.
Later we can reinterpret `amp` as d(ior)/dr or similar without changing the architecture.

---

## GordonMetric MetricModel (Gravity Mode)

### Intent

Provide a GR-inspired gravitational curvature controller with minimal parameters, suitable for simulating “gravitational objects” bending rays/paths.

We explicitly ignore deep causality minutia in v1.
We focus on spatial curvature-like bending behavior.

### Minimal Behavior (v1)

* direction: toward center (attractive) by default
* magnitude: amp * f(u) scaled by optional “mass-like” convention

```
dir = -normalize(pLocal)   // inward
a = dir * (amp * f(u))
```

### Optional Enhancements (v1.1+)

Add a “1/r^2-like” scaling by u or r:

```
a = dir * (amp * f(u) / max(epsilon, r*r))
```

This is controlled via modeFlags, not a new parameter.

---

## Relationship to Gordon Metric (Conceptual)

The Gordon metric is historically used to represent light propagation in a medium as an effective metric.

We borrow the **conceptual stance**:

* “field modifies the geometry of paths”
* implement as a controllable curvature/acceleration field

This is not an exact GR solver.
It is a practical “gravity mode” knob that shares architecture with GRIN.

---

## User-Facing Controls (Minimal Set)

Both models expose the same minimal UI:

* MetricModel: GRIN or GordonMetric
* ShapeType: SphereRadial / BoxVolume / …
* CurveType: Power/Linear/Polynomial/…
* rInner / rOuter
* amp
* curveA/curveB/curveC
* flags (invert, clamp, 1/r^2, etc.)

This ensures:

* easy pass in/out
* unified serialization
* consistent snapshot packing

---

## Mode Flags (Shared)

We reserve flags for cross-model behaviors:

* INVERT_SIGN (repel vs attract)
* CLAMP_01 (clamp f(u))
* USE_INV_R2 (apply 1/r^2 scaling)
* USE_INV_R (apply 1/r scaling)
* FLAT_INNER (zero influence for r < rInner)
* PLATEAU_INNER (constant influence inside rInner)

Flags avoid adding new parameters.

---

## Determinism Rules

* Field contributions must sum in stable FieldEntityId order
* Within a field, computations must use stable math paths
* Avoid randomization in gravity mode unless explicitly enabled

---

## Debug & Validation

Required visualizations:

* Field influence magnitude heatmap
* Direction vectors (sampled grid)
* Compare GRIN vs Gordon outputs for same parameters

---

## Success Criteria

MetricModel system is complete when:

* SceneSnapshot can represent both GRIN and GordonMetric fields identically
* FieldSystem can evaluate both using the shared pipeline
* User can swap metric models without reauthoring the scene
* rInner/rOuter remain universal influence bounds
* Future refinements do not require architectural changes

This spec establishes a unified field architecture capable of both optical and gravity-style curvature simulation.
