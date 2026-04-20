---
title: xPRIMEray — Research Papers
description: Invariant trilogy for geometry-aware wormhole rendering — arXiv-facing research notes with deterministic harness artifacts
---

# Research Papers

<div style="font-size:0.9rem;opacity:0.65;margin-bottom:1.5rem;">
Series: <strong>xPRIMEray Wormhole Invariant Trilogy</strong> &nbsp;·&nbsp;
Status: Draft (pre-submission) &nbsp;·&nbsp;
DOI: pending
</div>

This directory holds the xPRIMEray paper family: structured research notes positioned as arXiv-facing manuscripts.
Each paper defines, validates, or extends a geometric invariant governing wormhole rendering correctness.

---

## Paper Series

### Paper 000 — Series Overview

**[Unified Summary of the Wormhole Invariant Trilogy](paper_000_unified_summary/paper.md)**

<div class="paper-abstract">
Concise series overview framing the three invariant papers as one coherent rendering-correctness program.
Establishes shared terminology, notation conventions, and the relationship between the positive invariant (proto-caustic annulus),
the negative invariant (low-value sector budget), and their coupled phase-space stability region.
</div>

---

### Paper 001 — Positive Invariant

**[Proto-Caustic Invariant in Geometry-Aware Wormhole Transport](paper_001_proto_caustic_invariant/paper.md)**

<div class="paper-abstract">
Defines the destination-side annulus as the primary positive geometric invariant.
Demonstrates that wormhole transport correctness can be validated by checking whether
destination-side ray intersections form a stable annular structure, independent of scene parameters.
Establishes the hermetic throat fixture as the ground-truth harness for this invariant.
</div>

---

### Paper 002 — Negative Invariant

**[Low-Value Sector Budget as a Negative Invariant in Geometry-Aware Wormhole Transport](paper_002_low_value_sector_budget/paper.md)**

<div class="paper-abstract">
Defines the low-value outer-ring query-share budget as the complementary negative invariant.
Shows that an excessive fraction of rays landing in geometrically unproductive sectors is a reliable
indicator of transport misconfiguration, providing a budget-based rejection criterion dual to the annular contract of Paper 001.
</div>

---

### Paper 003 — Coupled Phase Space

**[Coupled Invariants and Stability Phase Space in Geometry-Aware Wormhole Transport](paper_003_coupled_invariants_phase_space/paper.md)**

<div class="paper-abstract">
Studies the positive and negative invariants jointly and identifies a bounded stable operating region.
Maps the under-constrained, stable, and rejected zones of the two-invariant phase space.
Provides a parameter-space stability diagram (Figure E) usable as a diagnostic tool during scene authoring.
</div>

---

### Paper 004 — Hermetic Throat Validation

**[Hermetic Throat Validation in Geometry-Aware Wormhole Transport](paper_004_hermetic_throat_validation/paper.md)**

<div class="paper-abstract">
Formalizes the hermetic throat fixture as a deterministic validation harness.
Proves that the fixture provides ground-truth coverage of both invariants from Papers 001 and 002,
and establishes observer-ladder protocols for Penrose causal consistency checking.
Includes LaTeX source for journal submission.
</div>

→ [LaTeX source](paper_004_hermetic_throat_validation/main.tex)

---

## Shared Resources

- [Shared Related Work & Bibliography](shared_related_work.md) — common terminology, notation, and reference buckets across all four papers
- [Shared Figure Captions](figure_captions.md) — paper-ready captions for Figures A–E in a consistent voice
- [Paper Template](./\_template/paper_template.md) — reusable manuscript scaffold for future notes

---

## Conventions

- Each paper directory contains `paper.md` (working manuscript) and optional `main.tex` for journal submission.
- Figure references point to deterministic harness artifacts under `Docs/wormhole_test/figures/`.
- Maintain the metadata header block at the top of each `paper.md`.
- Preserve the common section structure unless there is a strong paper-specific reason to diverge.
- Treat this family as **arXiv-facing research-note scaffolding**, not marketing copy.
- BibTeX citation templates are in [Shared Related Work](shared_related_work.md).

---

## Figure Quartet Reference

Each harness run produces five canonical figures:

| ID | File | Description |
|----|------|-------------|
| A | `figure_A_main_render.png` | Raw render output — no overlays |
| B | `figure_B_composed_overlay.png` | Render + research diagnostic overlay |
| C | `figure_C_ring_density.png` | Portal-sector ray density map |
| D | `figure_D_metrics_table.png` | Invariant values + performance summary |
| E | `figure_E_phase_space.png` | Coupled invariant phase-space plot |

Figures are stored in [`Docs/wormhole_test/figures/`](../wormhole_test/figures/).
