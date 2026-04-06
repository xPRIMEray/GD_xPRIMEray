# Papers

This directory holds paper-style research notes that are more structured than fixture notes and more publication-oriented than implementation docs.

Each paper directory should contain:

- `paper.md` — the current working manuscript
- figure references pointing to deterministic harness artifacts under `output/.../figures/`
- any future paper-local support files that belong to that manuscript alone

Current paper family:

- [Paper 001: Proto-Caustic Invariant in Geometry-Aware Wormhole Transport](paper_001_proto_caustic_invariant/paper.md)
- [Paper 002: Low-Value Sector Budget as a Negative Invariant in Geometry-Aware Wormhole Transport](paper_002_low_value_sector_budget/paper.md)
- [Paper 003: Coupled Invariants and Stability Phase Space in Geometry-Aware Wormhole Transport](paper_003_coupled_invariants_phase_space/paper.md)
- [Paper Template](./_template/paper_template.md)

Conventions:

- keep a metadata header block at the top of each paper
- preserve the common section structure unless there is a strong paper-specific reason to diverge
- keep figure quartet placeholders explicit and stable
- prefer deterministic harness artifacts over ad hoc screenshots
- treat the paper family as arXiv-facing research-note scaffolding, not marketing copy
