---
title: Unified Summary of the Wormhole Invariant Trilogy
authors:
date: 2026-04-06
invariant: trilogy_summary
status: draft
related_fixtures: wormhole_prototype
---

# Paper 000: Unified Summary of the Wormhole Invariant Trilogy

![Wormhole DualRealityTransport](../../../output/dual_reality/wormhole_inset_baseline.png)

*Current wormhole DualRealityTransport capture showing the curved main view, straight transport reference panel, and diagnostic overlays.*

## Abstract

This note summarizes a three-paper framework for deterministic, geometry-aware wormhole transport in `GD_xPRIMEray`. Paper 001 defines a proto-caustic invariant that preserves a destination-side annular concentration. Paper 002 defines a low-value sector budget that bounds expenditure in portal-local regions shown to have weak optical yield. Paper 003 studies these two contracts as a coupled system and identifies a bounded stability region rather than a single acceptable operating point. Taken together, the trilogy describes rendering behavior through measured invariants rather than through ad hoc tuning.

## Minimal Motivation

Wormhole rendering in this harness cannot be described adequately by global timing, stochastic convergence, or simple hit counts. Curved transport, remap, and observer-side film formation produce structured optical regions whose preservation matters. At the same time, some portal-local sectors consume substantial query work while contributing little to the maintained image. A satisfactory validation language must therefore say both what optical structure must survive and where recurrent expenditure must remain bounded.

## The Two Invariants

The trilogy is organized around two complementary constraints.

The positive invariant, introduced in Paper 001, preserves a destination-side annulus:

- `layer = 1`
- `radial_bin = 3`

through thresholds on:

- hit density
- hit continuity
- positive-overlap continuity
- radial gradient

The negative invariant, introduced in Paper 002, constrains a low-value outer-ring family:

- `layer = 0`
- `radial_bin = 3`

through a query-share budget:

- `actual_query_share <= maximum_allowed_query_share`

with the present deterministic budget anchored by:

- `baseline_query_share = 0.4011`
- `maximum_allowed_query_share = 0.361`

These constraints are complementary. One preserves signal. The other bounds waste.

## Coupled Phase Space

Paper 003 shows that the two invariants define a bounded operational phase space. The current retained throttle profile:

- `layer = 0`
- `radial_bin = 3`
- `theta bins = {13,14,15,0}`
- `period = 2`

occupies a stable region in which:

- the proto-caustic annulus remains preserved
- the low-value budget passes
- `pass2.query` and `pass2.physics` improve
- `geom_hits` and `final_write_px` remain stable

A stronger profile on the same region with `period = 3` crosses a practical boundary. Although the formal contracts still pass, annular metrics weaken and hit/write drift appears. The trilogy therefore supports a system-level claim: correct behavior is selected within a bounded region rather than obtained by monotonically increasing suppression.

## Figure Family

The trilogy shares a common figure language derived from the deterministic harness:

- Figure A: raw film-buffer render
- Figure B: composed render with research inset
- Figure C: portal-local ring-density map
- Figure D: compact metrics and contract summary
- Figure E: coupled phase-space map

These figures should be read as coordinated projections of one measured structure: observer image, explanatory geometry, portal-local density, compact contract state, and coupled operating region.

## Why This Matters

The trilogy replaces heuristic rendering folklore with explicit behavioral contracts. It shows that wormhole rendering can be validated not only by whether rays survive and write to film, but by whether geometry-aware structure is preserved and low-value expenditure remains bounded. In this sense, invariants define behavior rather than merely annotating it after the fact.

## Short Conclusion

Paper 001 establishes what must exist.  
Paper 002 establishes what must be limited.  
Paper 003 shows that the two constraints define a bounded stability region.  
Together, the trilogy provides a coherent basis for an eventual arXiv-style note on invariant-driven wormhole transport.
