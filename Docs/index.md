# GD_xPRIMEray

### Curved Ray Transport Engine for Gradient Index Media  
*A hybrid symbolic–numeric optical propagation framework*

---

## Abstract

GD_xPRIMEray extends the traditional straight-line ray tracing paradigm into a curved-ray propagation system capable of modeling graded refractive index (GRIN) fields, synthetic optical potentials, and non-Euclidean transport domains.

The engine integrates continuous refractive fields using high-order numerical solvers and modular field definitions embedded within Godot 4.x.

This transforms the rendering engine into a controllable physical simulation environment suitable for:

- Advanced optical research
- Gradient-index lens modeling
- Curvature-driven visualization
- Geodesic-like propagation experiments
- Educational and research simulation

---

## Key Capabilities

- Continuous refractive index fields
- RK4 curved ray integration
- Symbolic curvature validation
- Modular field injection architecture
- Visual debug + diagnostic overlays

---

## Documentation

- [Architecture](architecture.md): field injection, transport loop, gating hierarchy
- [Transport Model](transport_model.md): curvature definition, stepping logic, intersection semantics
- [Integrators](integrators.md): RK4 and stability controls
- [Validation Framework](validation.md): baseline comparisons, convergence metrics, regression harness
- [Roadmap](roadmap.md)

---


## Visual Overview

![System Architecture](assets/fig_01_architecture.png)

## Figures
![System Architecture](assets/fig_01_architecture.png)