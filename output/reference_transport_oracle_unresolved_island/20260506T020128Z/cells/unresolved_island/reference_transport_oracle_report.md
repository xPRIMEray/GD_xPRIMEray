# ReferenceTransportOracle Report

Diagnostic-only renderer validation. This packet records best-known renderer-reference transport paths; it is not physical GR validation.

- Samples: 25
- Oracle runs: 50
- Comparisons: 75
- Family rows: 225
- Cost rows: 100
- Runtime ms: 3531
- Oracle replay failures: 0
- Mean decision risk: 2.325386
- Max decision risk: 2.328717

## Epsilon Stability Classes

- unresolved: 75

## Secondary Pathology Tags

- phase_flip: 75
- plateau_oscillation: 75
- trajectory_escape: 75

## Trajectory Family Classes

- stable_family: 225

## Precision Cost

- Samples with at least one stable production step: 0
- Minimum precision means minimum tested production step satisfying topology/epsilon stability, not smallest possible step.

## Guardrail

- Oracle outputs must not feed rendering, scheduling, hit selection, shading, resolver decisions, traversal, or adaptive precision.
