# System Architecture

GD_xPRIMEray modifies the classical rendering pipeline by inserting a curvature-aware transport layer between ray generation and shading evaluation.

---

## High-Level Flow

1. Ray Generation
2. Field Evaluation
3. Curvature Derivative Computation
4. RK4 Step Integration
5. Surface Intersection Test
6. Shading / Accumulation

---

## Pipeline Diagram

*Pipeline diagram not yet available.*

---

## Modular Components

| Module | Purpose |
|--------|----------|
| FieldSource3D | Defines spatial refractive index profile |
| FieldGrid3D | Spatial sampling + field lookup |
| CurvedRayIntegrator | RK4 step solver |
| GrinFilmCamera | Injection point for curved transport |
| Diagnostics | Validation + path heatmap |

---

## Design Philosophy

Rather than treat curvature as a post-process distortion, GD_xPRIMEray integrates curvature at the transport layer, preserving physical consistency across interaction events.