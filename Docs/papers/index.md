# Papers

This directory holds paper-style research notes that are more structured than fixture notes and more publication-oriented than implementation docs.

Each paper directory should contain:

- `paper.md` — the current working manuscript
- figure references pointing to deterministic harness artifacts under `output/.../figures/`
- any future paper-local support files that belong to that manuscript alone

Current paper family:

- [Paper 000: Unified Summary of the Wormhole Invariant Trilogy](paper_000_unified_summary/paper.md) — concise series overview framing the trilogy as one invariant-driven rendering program.
- [Paper 001: Proto-Caustic Invariant in Geometry-Aware Wormhole Transport](paper_001_proto_caustic_invariant/paper.md) — defines the destination-side annulus as the positive geometric invariant.
- [Paper 002: Low-Value Sector Budget as a Negative Invariant in Geometry-Aware Wormhole Transport](paper_002_low_value_sector_budget/paper.md) — defines the low-value outer-ring query-share budget as the complementary negative invariant.
- [Paper 003: Coupled Invariants and Stability Phase Space in Geometry-Aware Wormhole Transport](paper_003_coupled_invariants_phase_space/paper.md) — studies the two contracts together and identifies a bounded stable operating region.
- [Figure E: Coupled Phase Space](../../output/wormhole_test/figures/figure_E_phase_space.png) — compact visual summary of the observed under-constrained, stable, and rejected regions.
- [Shared Related-Work and Bibliography Note](shared_related_work.md) — common terminology, notation, and reference buckets for eventual manuscript unification.
- [Shared Figure Captions](figure_captions.md) — concise reusable captions for Figures A–E in a consistent paper-ready voice.
- [Paper Template](./_template/paper_template.md) — reusable manuscript scaffold for future notes in the series.

Conventions:

- keep a metadata header block at the top of each paper
- preserve the common section structure unless there is a strong paper-specific reason to diverge
- keep figure quartet placeholders explicit and stable
- prefer deterministic harness artifacts over ad hoc screenshots
- treat the paper family as arXiv-facing research-note scaffolding, not marketing copy
