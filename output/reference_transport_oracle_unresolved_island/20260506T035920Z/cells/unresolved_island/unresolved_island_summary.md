# ReferenceTransportOracle Unresolved-Island Refinement

Diagnostic-only renderer validation. This is a best-known renderer-reference transport comparison, not physical ground truth.

- Samples: 289
- Comparisons: 3468
- Oracle replay failures: 0
- Local continuity vectors in x=32..48, y=27..43: 0
- Local transport shape regions touching patch: 0
- Sealed at 0.00625: true
- Unresolved at 0.003125: 0

## Pixel Classes

- stable: 289

## First Stable Step

- 0.014: 12
- 0.015: 33
- 0.016: 47
- 0.018: 59
- 0.02: 138

## Step Comparisons

- Mean absolute decision-risk delta, 0.00625 vs 0.003125: 0.000189
- Max absolute decision-risk delta, 0.00625 vs 0.003125: 0.000691

## Guardrail

- This analyzer writes diagnostics only. It does not smooth, alter beauty output, or feed any render decision.

## Stopping Rule

- The island is sealed at 0.00625 for the sampled patch. Extra-fine rerun is not required by this packet.
