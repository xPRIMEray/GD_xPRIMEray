# Specification — Curved Ray Chunk Integration

## Purpose

This document defines how curved rays are represented, integrated, and prepared for intersection.

Curved rays are modeled as sequences of macro path chunks that approximate continuous motion while providing conservative spatial envelopes.

This spec defines:

* Ray state representation
* Integration algorithm
* Chunk generation
* Envelope construction
* Error control

---

## Conceptual Model

A curved ray is a parametric trajectory:

```
p(t)
```

We approximate this using discrete chunks:

```
p0 → p1 → p2 → …
```

Each chunk represents a bounded segment of motion.

---

## Ray State Representation

Per-ray state:

```
struct RayState {
    Vector3 position;
    Vector3 direction;
    float t;
}
```

Direction is normalized after each integration step.

---

## Integrator Interface

```
StepResult IntegrateStep(RayState state, float dt)
```

Where:

```
struct StepResult {
    RayState newState;
    float curvatureEstimate;
}
```

Curvature estimate informs envelope size.

---

## Integration Method

Baseline integrator:

Symplectic Euler

```
velocity += accel(position) * dt
position += velocity * dt
```

Acceleration comes from FieldSystem.

Integrator must be:

* Deterministic
* Allocation-free
* Stable under small dt

---

## Chunk Formation

Micro-steps are grouped into macro chunks.

Chunk structure:

```
struct RayChunk {
    Vector3 p0;
    Vector3 p1;
    float radiusBound;
    float t0;
    float t1;
}
```

A chunk spans N micro-steps.

---

## Envelope Construction

The chunk envelope must conservatively bound the true curve.

Minimum requirement:

AABB(p0, p1) expanded by radiusBound

Better approximation:

Capsule from p0 to p1 with radiusBound

---

## Radius Bound Estimation

Radius bound derives from curvature:

```
radiusBound = k * curvatureEstimate * (dt^2)
```

Where k is a safety factor.

Bounds must never underestimate deviation.

Conservative overestimation is acceptable.

---

## Adaptive Chunking

Chunk size may adapt based on:

* Field curvature
* Distance traveled
* Error tolerance

High curvature → smaller chunks.

Low curvature → larger chunks.

---

## Termination Conditions

Integration stops when:

* Hit detected
* Max distance reached
* Step budget exhausted
* Ray exits scene bounds

---

## Intersection Preparation

Chunks are passed to the BVH traversal system.

Traversal operates only on chunk envelopes.

Refinement occurs only when candidates are detected.

---

## Error Guarantees

The integrator must guarantee:

* No missed intersections due to underestimation
* Stable numerical behavior
* Deterministic chunk generation

---

## Performance Goals

Chunk generation must:

* Avoid heap allocation
* Use fixed-capacity buffers
* Minimize branching
* Support vectorization

---

## Debug Instrumentation

Optional debug outputs:

* Chunk visualization
* Curvature heatmaps
* Step statistics
* Envelope accuracy metrics

---

## Extension Points

Future upgrades may include:

* Higher-order integrators
* Analytical curvature bounds
* Path BVH acceleration
* GPU integration kernels

---

## Success Criteria

The chunk system is complete when:

* All rays produce stable chunk sequences
* BVH traversal receives conservative envelopes
* No intersections are missed
* Performance scales with ray count

This system defines the renderer’s motion backbone.
