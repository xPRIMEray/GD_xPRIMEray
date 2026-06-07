# Observatory Methods

Observatory Story sheets are 3x3 diagnostic summaries for xPRIMEray fixtures. They are reporting artifacts only: they do not feed rendering, scheduling, hit selection, shading, resolver decisions, traversal, or adaptive precision.

## How To Read An Observatory Story

Read row-major: panels 1 to 3 across the top row, 4 to 6 across the middle row, and 7 to 9 across the bottom row.

1. Raw visual: what the camera or beauty capture saw.
2. Scene geometry: what objects, receivers, or probe geometry exist.
3. Curvature field: what field or probe context is active.
4. Transport ownership: where rays or samples resolved.
5. Hit/miss map: whether rays found targets.
6. Traversal steps: how much integration work was required.
7. Budget stress: where traversal or precision budget was pressured.
8. Combined diagnostic: the mission-control overview.
9. Curvature signature: the field/probe effect summary.

## How To Interpret Curvature Signature

The Curvature signature panel is a delta view. It compares a current field/probe state against a baseline when that baseline exists. In the curvature sweep, red indicates pixels that required more traversal steps, blue indicates fewer steps, and dark/black indicates no measured change. It is a transport-effort map, not a photograph of the scene.

The `curvature_signature_ladder.png` asset compresses the 0%, 25%, 50%, 75%, and 100% sweep into one README-friendly strip.

## How To Interpret Closure

Closure asks whether evaluated rays/pixels found valid targets under the scene contract. In a sealed hermetic room, misses are expected to be zero. A blank beauty image does not fail closure if hit diagnostics prove all evaluated rays hit; it fails visual-render confirmation instead.

## How To Interpret Budget Stress

Budget stress highlights rays or pixels that reached max-step, overrun-step, precision, or oracle refinement limits. A budget warning does not automatically mean a miss: it may mean the hit was found late or after an overrun warning. Treat it as a map of where transport was expensive or numerically fragile.

## Missing Panels

`MISSING / N.A.` tiles are intentional. They mean no existing artifact matched that Observatory panel for the selected fixture output. The reporting layer does not synthesize fake evidence.

