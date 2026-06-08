# What the Observatory Measures

An **Observatory Story** is a nine-panel diagnostic contact sheet. Read it left to right: raw render, scene geometry, curvature field, ray ownership, hit/miss closure, traversal cost, budget stress, combined diagnostics, and curvature signature. The goal is to make one fixture readable as both an image and an instrumented measurement.

The Observatory Gallery is visitor-facing: it shows what each fixture measures and where evidence comes from. The deeper Observatory chapters are the 20-minute tour for implementation history, experiments, and design rationale.

The [Observatory Maturity Ladder](./observatory_maturity_ladder.md) labels how strongly an artifact should be trusted. **Maturity is evidence strength, not physical truth.**

## Core Terms

**Transport completion is not physical accuracy.** A run can classify every ray and still not prove the renderer is a physically complete model of light, gravity, or optics.

**Hermetic closure** means every evaluated ray or pixel is classified within a declared scene contract. In a sealed room, the expected contract is simple: every ray should hit a receiver surface. Misses are reported, not hidden.

**Curvature Signature** means a measured difference relative to a 0% curvature baseline. It is a diagnostic map of what changed in traversal behavior when the field was activated.

**Oracle** means reference integration. It is a comparison instrument, not ground truth. Oracle agreement is diagnostic agreement for a fixture and configuration.

**Budget stress** means rays used most or all of their allowed traversal steps. It is a warning about cost or numerical fragility, not an automatic failure. A stressed ray may still find a valid hit.

## What xPRIMEray Does Not Claim

xPRIMEray does not claim that a diagnostic image is proof of physical correctness. It does not claim that an oracle is ground truth. It does not claim that open-target fixtures should reach hermetic hit rates. It does not claim that budget stress is a miss.

The strongest claim in this gallery is narrower and testable: for a named fixture, camera, field, and budget, the system can report whether rays were classified to completion within the scene contract, how much traversal work was required, and how the result differs from a straight 0% baseline.

## Nine-Panel Reading Guide

1. Raw visual: What did the camera see?
2. Scene geometry: What objects exist?
3. Curvature field: What field bends the rays?
4. Transport ownership: Where did each ray end up?
5. Hit/miss map: Did every ray find a target?
6. Traversal steps: How hard was the trip?
7. Budget stress: Which rays nearly ran out of budget?
8. Combined diagnostic: What do all diagnostics show together?
9. Curvature Signature: What changed relative to 0% curvature?
