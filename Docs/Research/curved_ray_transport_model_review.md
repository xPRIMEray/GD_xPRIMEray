# Curved Ray Transport Model Review

## Purpose

This document defines a structured research review and ranking of mathematical models for curved-ray transport in **xPRIMEray**. The goal is to identify which approaches provide the highest **efficiency per unit accuracy** for field traversal, given that fixture-based validation is now operational.

---

## Core Framing

There is no single universally optimal method. Efficiency depends on the objective:

* Full path-accurate curved-ray transport
* First-arrival / travel-time approximation
* Long-term structural fidelity (Hamiltonian preservation)
* Global probing / sampling efficiency

This review separates models by **what problem they actually solve**.

---

## Model Ranking Matrix

| Model Family                                                   | Theoretical Efficiency        | Physics Fidelity | Role in xPRIMEray         | Priority |
| -------------------------------------------------------------- | ----------------------------- | ---------------- | ------------------------- | -------- |
| Symplectic Hamiltonian Ray Tracing + Derivative-Aware Stepping | High                          | High             | Primary transport model   | 1        |
| Embedded Adaptive RK (Dormand-Prince style)                    | High                          | Medium-High      | Benchmark baseline        | 2        |
| Fast Marching / Fast Sweeping (Eikonal)                        | Very High (for first-arrival) | Low-Medium       | Planning / guidance field | 3        |
| Trajectory Optimization / Control                              | Medium                        | Medium-High      | Probe policy layer        | 4        |
| Lie-Group / Manifold Integrators                               | Medium                        | High             | Structural refinement     | 5        |
| Neural Adaptive Sampling                                       | Potentially High              | Variable         | Budget allocation layer   | 6        |

---

## Key Insight

> Traditional adaptive stepping reacts to curvature.
>
> Next-generation stepping should react to **curvature AND its derivatives**.

This introduces a predictive rather than reactive traversal model.

---

## Recommended Primary Direction

### 1. Symplectic Hamiltonian Transport + Derivative-Aware Stepping

This is the highest-value next experiment.

Why:

* Preserves Hamiltonian structure of optical transport
* Aligns with GRIN / refractive index field physics
* Allows integration of derivative-based control

### Step Controller Concept

Let:

* `k` = curvature proxy
* `dk` = first derivative along path
* `d2k` = second derivative

Define:

```
difficulty = a*k + b*|dk| + c*|d2k|
step_length ∝ 1 / difficulty
```

Interpretation:

* High curvature, low derivative → smooth bend → moderate steps
* Moderate curvature, high derivative → transition → reduce early
* Low curvature, low derivative → long stride

---

## Secondary Baseline

### 2. Embedded Adaptive RK

Use as a trusted comparison model:

* Provides error-controlled stepping
* Well understood behavior
* Acts as validation reference

Metrics:

* Runtime
* Accepted steps
* Image deviation
* Stability

---

## Supporting Models

### 3. Fast Marching / Fast Sweeping

Not a replacement for ray tracing.

Best use:

* Compute travel-time fields
* Predict high-difficulty regions
* Guide scheduler / sampling priorities

---

### 4. Trajectory Optimization / Control

Interpret ray traversal as a control problem:

* State = ray position/direction
* Control = step size / direction updates

Use for:

* Adaptive refinement policies
* Probe targeting

---

### 5. Lie / Manifold Integrators

Useful when:

* State evolves on constrained geometric spaces
* Strong structure preservation is required

Lower priority for current fixtures.

---

### 6. Neural Adaptive Sampling

Not primary physics engine.

Potential use:

* Sample allocation
* Importance prediction

---

## Experimental Plan

### Experiment 1: Derivative-Aware Controller

Modify current RayBeamRenderer:

* Add curvature history buffer
* Compute first and second derivatives
* Apply smoothed difficulty metric

Evaluate:

* Runtime reduction
* Step count reduction
* Visual stability

---

### Experiment 2: RK Baseline

Run identical fixtures using:

* Embedded adaptive RK stepping

Compare:

* Accuracy vs runtime
* Step efficiency

---

### Experiment 3: Symplectic Integrator

Implement Hamiltonian-consistent stepping:

Compare against RK:

* Long-path stability
* Energy drift
* Visual coherence

---

### Experiment 4: Eikonal Guidance Field

Precompute travel-time field:

Use for:

* Scheduler hints
* Candidate region prioritization

---

## Architectural Synthesis

The system can be divided into three layers:

### 1. Transport Layer (Local Physics)

* Symplectic / RK stepping
* Derivative-aware control

### 2. Field Awareness Layer

* Eikonal / gradient maps
* Difficulty estimation

### 3. Control / Scheduling Layer

* Probe allocation
* Refinement strategy

---

## Final Recommendation

The most efficient next step is:

> Implement **derivative-aware adaptive stepping** within the current transport system, then benchmark against an embedded RK baseline, and finally evaluate a symplectic integrator for long-term structural gains.

---

## One-Line Philosophy

> Curvature tells us where the ray is.
>
> Derivatives tell us where the field is going.

---

## Status

* Fixtures: Ready
* Measurement pipeline: Ready
* Next phase: Model experimentation

---

End of document
