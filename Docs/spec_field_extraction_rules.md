# Specification — Field Entity Extraction Rules (Godot → SceneSnapshot)

## Purpose

This document defines the rules for extracting field nodes (e.g., GrinField, GravityField/GordonMetricField) from the Godot scene into renderer-native FieldEntities within SceneSnapshot.

Canonical `FieldSource3D` resolver behavior is specified in:
`spec_fieldsource3d_canonical_params_1.md`.

Goals:

* Deterministic, stable snapshot extraction
* Fast runtime field evaluation
* Conservative bounds for broadphase (FTLAS)
* Minimal, uniform parameter packing
* Support for multiple metric models (GRIN, GordonMetric/Gravity)

This spec is concerned with *data extraction and mapping*, not the math details of field evaluation (defined in Field System / Metric specs).

---

## Node Categories

The snapshot builder recognizes the following categories:

* **Field Nodes**: nodes that define a spatial metric / field contribution

  * GRIN field nodes (IOR framework)
  * Gordon Metric field nodes (gravity framework)
* **Geometry Nodes**: mesh instances for intersection/shading
* **Camera Node**: render camera reference

This spec focuses on Field Nodes.

---

## Field Node Interface Contract (Godot Side)

A field node must supply:

### Required Common Properties

* Enabled flag
* Transform (world)
* Shape type (SphereRadial default)
* `rInner`, `rOuter`
* `amp`
* Metric model selector (GRIN vs GordonMetric)
* CurveType selector (Linear/Power/Polynomial/Exponential/etc.)
* Curve params (A,B,C)

### Optional Properties

* debug color / label
* priority override (rare; default ordering used)
* local shape dimensions (Box extents, Cylinder radius/height)
* “falloff mode” flags (clamp/invert/etc.)

If a property is missing, defaults must be applied deterministically.

---

## Extraction Pipeline Overview

On each render step:

1. Enumerate all active field nodes
2. Build a deterministic ordering
3. For each node:

   * Identify MetricModel + ShapeType + CurveType
   * Extract transform + compute world bounds
   * Pack params into the FieldParamBuffer
   * Emit FieldEntity entry into FieldEntitySOA
4. Build Field TLAS (FTLAS) over world bounds

SceneSnapshot then becomes read-only.

---

## Deterministic Ordering Rules

Because Godot node enumeration order may not be stable:

Field nodes must be sorted using a stable key:

1. NodePath string (primary)
2. Node instance ID (secondary)
3. Field type enum (tertiary)

This ensures:

* Stable FieldEntityId assignment per snapshot
* Stable summation order during AccelAt

---

## Shape Extraction Rules

### ShapeType: SphereRadial (default)

Local-space domain:

* center at node origin
* radius controlled by rOuter

Local bounds:

* AABB = [-rOuter, +rOuter] in X/Y/Z

World bounds:

* Transform local bounds conservatively into world AABB

### ShapeType: BoxVolume

Local-space extents:

* `boxExtents` from node property (Vector3 half-extents)
* MUST be present or default to [rOuter,rOuter,rOuter]

Local bounds:

* AABB = [-extents, +extents]

World bounds:

* Transform into conservative world AABB

### ShapeType: Cylinder (optional v1.1)

Local:

* radius, halfHeight

World bounds:

* conservative AABB from transformed cylinder bounds

---

## Bounds Conservatism Requirements

World bounds MUST fully contain influence region.

Rules:

* rOuter is a hard cutoff; bounds must include it
* If shape scaling exists, bounds must expand accordingly
* Non-uniform scales must be handled conservatively:

  * Use max scale axis for radial bounds if needed

If in doubt: overestimate bounds.

Underestimation is forbidden (can cause missed field contributions → incorrect rays).

---

## Parameter Packing Rules

All fields must pack into the common parameter block layout.

### Float Block Layout (v1)

```
[0] rInner
[1] rOuter
[2] amp
[3] curveA
[4] curveB
[5] curveC
[6] reserved0
[7] reserved1
```

### Non-float metadata stored in SOA arrays

* metricModel (int)
* shapeType (int)
* curveType (int)
* modeFlags (uint)

---

## Default Values (Deterministic)

If a node does not provide a value:

* rInner = 0
* rOuter = 1
* amp = 1
* curveType = Power
* curveA = 1
* curveB = 0
* curveC = 0
* shapeType = SphereRadial
* metricModel = GRIN (unless explicitly Gordon)
* modeFlags = 0

All defaults must be centralized in one builder config struct to avoid drift.

---

## FieldEntity Emission

For each extracted node, emit:

* fieldType / metricModel
* transform (worldFromLocal, localFromWorld)
* worldBounds (AABB)
* paramOffset/paramLength (into packed float buffer)
* flags

---

## Validation & Debug Extraction Modes

### Required Checks

* rOuter >= rInner
* rOuter > 0
* bounds volume non-zero
* transform invertible (if not, fallback to identity + warn)

### Debug Modes

* print extracted field list with IDs and bounds
* visualize worldBounds
* verify FTLAS candidate count sanity

---

## Success Criteria

Extraction is complete when:

* FieldEntities in SceneSnapshot match all field nodes in the Godot scene
* Ordering is stable and deterministic
* Bounds are conservative
* Params pack uniformly
* Both GRIN and Gordon metric fields are represented identically at the snapshot layer

This spec defines the authoritative scene → field snapshot mapping.
