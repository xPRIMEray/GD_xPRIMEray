# ReferenceTransportOracle Report

Diagnostic-only renderer validation. This packet records best-known renderer-reference transport paths; it is not physical GR validation.

- Samples: 289
- Oracle runs: 578
- Comparisons: 3468
- Family rows: 2601
- Cost rows: 3757
- Runtime ms: 101569
- Oracle replay failures: 0
- Mean decision risk: 0.000894
- Max decision risk: 0.002594

## Epsilon Stability Classes

- stable: 2649
- unresolved: 819

## Secondary Pathology Tags


## Trajectory Family Classes

- stable_family: 2601

## Precision Cost

- Samples with at least one stable production step: 289
- Minimum precision means minimum tested production step satisfying topology/epsilon stability, not smallest possible step.

## Guardrail

- Oracle outputs must not feed rendering, scheduling, hit selection, shading, resolver decisions, traversal, or adaptive precision.
