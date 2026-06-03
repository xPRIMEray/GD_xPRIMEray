# Hermetic Hit Closure Summary

Hermetic closure validates transport completion within a known scene contract. It does not establish physical correctness. Outputs are diagnostic-only and must not feed the renderer.

## Results

- Cells analyzed: 2
- Failure islands: 2
- Integration escape vectors: 3040
- Phase counts: {'budget_saturated': 1, 'underresolved': 1}

## Cell Table

| cell | step | budget | curvature | traversal | closure % | no-hit | budget no-hit | phase | next action |
|---|---:|---:|---:|---|---:|---:|---:|---|---|
| cells/step_0.015/budget_32/curvature_0/row | 0.015 | 32 | 0 | row | 0.0 | 3600 | 2400 | budget_saturated | increase max traversal/step budget or use adaptive budget scaling |
| cells/step_0.015/budget_700/curvature_0/row | 0.015 | 700 | 0 | row | 48.888889 | 1840 | 0 | underresolved | reduce step size, increase local fixture focus, or inspect closure basins |
