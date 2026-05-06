# ReferenceTransportOracle Report

Diagnostic-only renderer validation. This packet records best-known renderer-reference transport paths; it is not physical GR validation.

- Samples: 8
- Oracle runs: 16
- Comparisons: 8
- Family rows: 72
- Cost rows: 16
- Runtime ms: 1336
- Oracle replay failures: 0
- Mean decision risk: 2.325014
- Max decision risk: 2.326186

## Epsilon Stability Classes

- unresolved: 8

## Secondary Pathology Tags

- phase_flip: 8
- plateau_oscillation: 8
- trajectory_escape: 8

## Trajectory Family Classes

- stable_family: 72

## Precision Cost

- Samples with at least one stable production step: 0
- Minimum precision means minimum tested production step satisfying topology/epsilon stability, not smallest possible step.

## Guardrail

- Oracle outputs must not feed rendering, scheduling, hit selection, shading, resolver decisions, traversal, or adaptive precision.
