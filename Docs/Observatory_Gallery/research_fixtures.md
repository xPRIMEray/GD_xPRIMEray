# Research Fixtures

The working collection of stress tests, probes, and characterization scenes. These are the instruments used to discover and map the phenomena that later become canonical.

## Fixture Taxonomy (Canonical Presentation)

Fixtures are organized by the primary transport phenomenon they isolate or stress:

**Field-Profile Fixtures** (isolate curvature signatures)
- Fixture 001 — Radial GRIN Baseline
- Fixture 002 — Linear Transport Baseline
- Fixture 003 — Offset / Dual Attractor
- Fixture 004 — Sector / Extended Field
- Fixture 005 — Throat Depth Map (wormhole-specific)

**Topology & Portal Fixtures**
- Overspace Witness Rooms (mouth, throat, exit, checkpoint sequences)
- Wormhole Prototype and Validation Pipelines

**Domain & Ownership Stress**
- Domain Resolver Stress
- Multi-Object Causal Stress
- Unresolved Island / reference-integration probes

**Boundary & Interface**
- Boundary Shell Entry / Policy Compare / Nested Modes
- Hermetic Curved Room variants

**Visual & Observability**
- GRIN Basic Visual (linear, off-axis, straight reference pairs)
- Metric Basic Visual
- Atomic Orbital GRIN Room and Visual Observatory

## How to Read a Research Fixture

Each fixture ships with:
- The `.tscn` scene (reproducible camera, geometry, fields).
- Associated controller or harness script (if any).
- Timestamped run folders in `output/` containing:
  - Contact sheets and hero images.
  - Classification deltas, ladders, heatmaps.
  - Raw telemetry (CSVs, JSON) for quantitative analysis.
- A `README.md` in the output folder that serves as the self-describing exhibit card.

## Gallery of Research Contact Sheets & Diagnostics

Reference the following curated collections (promote the small PNGs to `visuals/` and full runs via GitHub Releases when needed):

- Corner Transport Probe + Reference Geodesic Probe
- Transport Ownership Graph Precision Sweeps
- Domain Telemetry and First-Hit Validation
- Tile-Commit Traversal Comparisons (scheduler effect on observed structure)
- Characterization Ledgers (completeness of fixture test matrices)

**Researcher Path**

1. Pick a fixture from the taxonomy above.
2. Load the `.tscn` in Godot.
3. Run the matching test script or harness with your desired telemetry / scheduler / budget settings.
4. The resulting `output/<timestamp>/` folder + its `README.md` is a ready-to-publish research exhibit.

These fixtures are the raw material from which the Canonical and Benchmark sections are distilled. They remain in the gallery precisely because they are the places where new phenomena are still being discovered.
