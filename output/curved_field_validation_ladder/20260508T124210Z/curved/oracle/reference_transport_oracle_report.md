# ReferenceTransportOracle Report

Diagnostic-only renderer validation. This packet records best-known renderer-reference transport paths; it is not physical GR validation.

- Samples: 8
- Oracle runs: 16
- Comparisons: 24
- Family rows: 72
- Cost rows: 32
- Runtime ms: 1236
- Oracle replay failures: 0
- Mean decision risk: 3.603431
- Max decision risk: 6.810292

## Epsilon Stability Classes

- unresolved: 24

## Secondary Pathology Tags

- plateau_oscillation: 24

## Trajectory Family Classes

- stable_family: 72

## Precision Cost

- Samples with at least one stable production step: 0
- Minimum precision means minimum tested production step satisfying topology/epsilon stability, not smallest possible step.

## Guardrail

- Oracle outputs must not feed rendering, scheduling, hit selection, shading, resolver decisions, traversal, or adaptive precision.
