# Release to Production Validation Process

This directory defines the validation process used to promote experimental rendering features into production-ready artifacts.

This process bridges:
- research (physics / rendering correctness)
- engineering (deterministic reproducibility)
- communication (visual storytelling)
- release (public-facing executable and media)

We treat “Release” as:
- software release
- research artifact release
- content/media release

The core unit of validation is a **Camera Path Study**.

For the Phase 2 v0.0-pre instrument validation walkthrough, use
[`GRIN_OBSERVE_V0_PRE_DEMO.md`](GRIN_OBSERVE_V0_PRE_DEMO.md). That demo is
scoped to the GRIN Basic Visual Off-Axis Observe straight/curved pair and
intentionally excludes wormhole and overspace scenes from the primary evidence.

Each study:
- defines a deterministic camera path
- samples fixed stations
- captures multiple diagnostic layers
- produces reproducible artifacts
- generates summary outputs for analysis and publication

This ensures that all released content is:
- reproducible
- interpretable
- aligned with underlying transport behavior
